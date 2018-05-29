using Spreads.Buffers;
using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.SignalW.Client
{
    public abstract class Channel
    {
        public abstract ValueTask WriteAsync(MemoryStream item, bool closeStream = false);

        public abstract ValueTask<bool> TryComplete();

        public abstract ValueTask<MemoryStream> ReadAsync();

        public abstract Task<Exception> Completion { get; }
    }

    public class WsChannel : Channel
    {
        private readonly WebSocket _ws;
        private readonly Format _format;
        private TaskCompletionSource<Exception> _tcs;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);

        public WsChannel(WebSocket ws, Format format)
        {
            _ws = ws;
            _format = format;
            _tcs = new TaskCompletionSource<Exception>();
            _cts = new CancellationTokenSource();
        }

        public override async ValueTask WriteAsync(MemoryStream item, bool closeStream = false)
        {
#if !NETCOREAPP2_1
            throw new NotSupportedException();
#endif
            // According to https://github.com/aspnet/WebSockets/blob/4e2ecf8a63b9fc78175d1ef62bf2b1918b8b3986/src/Microsoft.AspNetCore.WebSockets/Internal/fx/src/System.Net.WebSockets.Client/src/System/Net/WebSockets/ManagedWebSocket.cs#L20
            // Only a single writer/reader at each moment is OK
            // But even otherwise, for multiframe messages we must process the entire stream
            // and do not allow other writers to push their frames before we finished processing the stream.
            // We assume that the memory stream is already complete and do not wait for its end,
            // just iterate over chunks, therefore it's safe for many writer just to wait for the semaphore

            // TODO review SigR has slower path not to wait for
            if (!_writeSemaphore.Wait(0))
            {
                await _writeSemaphore.WaitAsync(_cts.Token);
            }

            try
            {
                if (!(item is RecyclableMemoryStream rms))
                {
                    rms = RecyclableMemoryStreamManager.Default.GetStream(null, checked((int)item.Length));
                    item.CopyTo(rms);
                    if (closeStream)
                    {
                        item.Close();
                    }
                }

                try
                {
                    using (var e = rms.Chunks.GetEnumerator())
                    {
                        var type = _format == Format.Binary
                            ? WebSocketMessageType.Binary
                            : WebSocketMessageType.Text;
                        ArraySegment<byte> chunk;
                        if (e.MoveNext())
                        {
                            while (true)
                            {
                                chunk = e.Current;
                                var endOfMessage = !e.MoveNext();
#if NETCOREAPP2_1
                                var t = _ws.SendAsync((ReadOnlyMemory<byte>)chunk, type, endOfMessage, _cts.Token);

                                if (!t.IsCompletedSuccessfully)
                                {
                                    await t;
                                }
#else

                                await _ws.SendAsync(chunk, type, endOfMessage, _cts.Token);
#endif
                                if (endOfMessage) { break; }
                            }
                        }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    if (!_cts.IsCancellationRequested)
                    {
                        // cancel readers and other writers waiting on the semaphore
                        _cts.Cancel();
                        _tcs.TrySetResult(ex);
                    }
                    else
                    {
                        await
                            _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure",
                                CancellationToken.None);
                        _tcs.TrySetResult(null);
                    }
                    // Write not finished, Completion indicates why (null - cancelled)
                    return;
                }
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public override async ValueTask<bool> TryComplete()
        {
            // TODO disconnect, now just hangs e.g. when not auth
            if (_cts.IsCancellationRequested)
            {
                return false;
            }
            _cts.Cancel();
            await _writeSemaphore.WaitAsync();
            await _readSemaphore.WaitAsync();
            return true;
        }

        public ValueTask<MemoryStream> ReadAsync2()
        {
            var blockSize = RecyclableMemoryStreamManager.Default.BlockSize;
            byte[] buffer = null;
            var moreThanOneBlock = true; // TODO first block optimization
            // this will create the first chunk with default size
            var ms = RecyclableMemoryStreamManager.Default.GetStream("WSChannel.ReadAsync", blockSize);

            //if (!_readSemaphore.Wait(0))
            //{
            //    await _readSemaphore.WaitAsync(_cts.Token);
            //}

            try
            {
                // TODO first block optimization
                buffer = ArrayPool<byte>.Shared.Rent(blockSize); //ms.blocks[0];
#if NETCOREAPP2_1
                // Do a 0 byte read so that idle connections don't allocate a buffer when waiting for a read
                var t = _ws.ReceiveAsync(Memory<byte>.Empty, CancellationToken.None);
                var result = t.Result;
                if (result.MessageType != WebSocketMessageType.Close)
                {
                    ValueTask<ValueWebSocketReceiveResult> task;
                    task = _ws.ReceiveAsync((Memory<byte>)buffer, _cts.Token);

                    result = task.Result;
                }
#else
                var result = _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token).Result;
#endif

                while (result.MessageType != WebSocketMessageType.Close && !_cts.IsCancellationRequested)
                {
                    // we write to the first block directly, to save one copy operation for
                    // small messages (< blockSize), which should be the majorority of cases
                    if (!moreThanOneBlock)
                    {
                        //ms.length = result.Count;
                        //ms.Position = result.Count;
                    }
                    else
                    {
                        ms.Write(buffer, 0, result.Count);
                    }
                    if (!result.EndOfMessage)
                    {
                        //moreThanOneBlock = true;
                        //buffer = ArrayPool<byte>.Shared.Rent(blockSize);
#if NETCOREAPP2_1
                        var task = _ws.ReceiveAsync((Memory<byte>)buffer, _cts.Token);
                        result = task.Result;

#else
                        result = _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token).Result;
#endif
                    }
                    else
                    {
                        break;
                    }
                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _cts.Cancel();
                    // TODO remove the line, the socket is already closed
                    //await _ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    _tcs.TrySetResult(null);
                    ms.Dispose();
                    ms = null;
                }
            }
            catch (Exception ex)
            {
                ms.Dispose();
                ms = null;
            }
            finally
            {
                if (moreThanOneBlock)
                {
                    ArrayPool<byte>.Shared.Return(buffer, false);
                }
                // _readSemaphore.Release();
            }
            return new ValueTask<MemoryStream>(ms);
        }

        // TODO See extensions/WS transpprt
        public async ValueTask<MemoryStream> ReadAsync1()
        {
            var blockSize = RecyclableMemoryStreamManager.Default.BlockSize;
            byte[] buffer = null;
            var moreThanOneBlock = true; // TODO first block optimization
            // this will create the first chunk with default size
            var ms = RecyclableMemoryStreamManager.Default.GetStream("WSChannel.ReadAsync", blockSize);

            if (!_readSemaphore.Wait(0))
            {
                await _readSemaphore.WaitAsync(_cts.Token);
            }

            try
            {
                // TODO first block optimization
                buffer = ArrayPool<byte>.Shared.Rent(blockSize); //ms.blocks[0];
#if NETCOREAPP2_1
                // Do a 0 byte read so that idle connections don't allocate a buffer when waiting for a read
                var t = _ws.ReceiveAsync(Memory<byte>.Empty, CancellationToken.None);
                var result = await t;
                if (result.MessageType != WebSocketMessageType.Close)
                {
                    var task = _ws.ReceiveAsync((Memory<byte>)buffer, _cts.Token);
                    if (task.IsCompletedSuccessfully)
                    {
                        result = task.Result;
                    }
                    else
                    {
                        result = await task;
                    }
                }
#else
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
#endif

                while (result.MessageType != WebSocketMessageType.Close && !_cts.IsCancellationRequested)
                {
                    // we write to the first block directly, to save one copy operation for
                    // small messages (< blockSize), which should be the majorority of cases
                    if (!moreThanOneBlock)
                    {
                        //ms.length = result.Count;
                        //ms.Position = result.Count;
                    }
                    else
                    {
                        ms.Write(buffer, 0, result.Count);
                    }
                    if (!result.EndOfMessage)
                    {
                        //moreThanOneBlock = true;
                        //buffer = ArrayPool<byte>.Shared.Rent(blockSize);
#if NETCOREAPP2_1
                        var task = _ws.ReceiveAsync((Memory<byte>)buffer, _cts.Token);
                        result = task.IsCompletedSuccessfully ? task.Result : await task;
#else
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
#endif
                    }
                    else
                    {
                        break;
                    }
                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _cts.Cancel();
                    // TODO remove the line, the socket is already closed
                    //await _ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    _tcs.TrySetResult(null);
                    ms.Dispose();
                    ms = null;
                }
                else if (_cts.IsCancellationRequested)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", CancellationToken.None);
                    _tcs.TrySetResult(null);
                    ms.Dispose();
                    ms = null;
                }
            }
            catch (Exception ex)
            {
                if (_cts.IsCancellationRequested)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", CancellationToken.None);
                    _tcs.TrySetResult(null);
                }
                else
                {
                    _tcs.TrySetResult(ex);
                }
                ms.Dispose();
                ms = null;
            }
            finally
            {
                if (moreThanOneBlock)
                {
                    ArrayPool<byte>.Shared.Return(buffer, false);
                }
                _readSemaphore.Release();
            }
            return ms;
        }

        public override async ValueTask<MemoryStream> ReadAsync()
        {
            var len = 0;
            var blockSize = RecyclableMemoryStreamManager.Default.BlockSize;
            byte[] buffer = null;
            var moreThanOneBlock = true; // TODO first block optimization
            // this will create the first chunk with default size
            // var ms = RecyclableMemoryStreamManager.Default.GetStream("WSChannel.ReadAsync", blockSize);

            if (!_readSemaphore.Wait(0))
            {
                await _readSemaphore.WaitAsync(_cts.Token);
            }

            try
            {
                // TODO first block optimization
                buffer = ArrayPool<byte>.Shared.Rent(blockSize); //ms.blocks[0];

#if NETCOREAPP2_1
                // Do a 0 byte read so that idle connections don't allocate a buffer when waiting for a read
                var t = _ws.ReceiveAsync(Memory<byte>.Empty, CancellationToken.None);
                ValueWebSocketReceiveResult result;
                if (t.IsCompleted)
                { result = t.Result; }
                else
                { result = await t; }

                if (result.MessageType != WebSocketMessageType.Close)
                {
                    var task = _ws.ReceiveAsync((Memory<byte>)buffer, _cts.Token);
                    if (task.IsCompletedSuccessfully)
                    {
                        result = task.Result;
                    }
                    else
                    {
                        result = await task;
                    }

                    len = result.Count;
                }
#else
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
#endif

//                while (result.MessageType != WebSocketMessageType.Close && !_cts.IsCancellationRequested)
//                {
//                    // we write to the first block directly, to save one copy operation for
//                    // small messages (< blockSize), which should be the majorority of cases
//                    if (!moreThanOneBlock)
//                    {
//                        //ms.length = result.Count;
//                        //ms.Position = result.Count;
//                    }
//                    else
//                    {
//                        ms.Write(buffer, 0, result.Count);
//                    }
//                    if (!result.EndOfMessage)
//                    {
//                        //moreThanOneBlock = true;
//                        //buffer = ArrayPool<byte>.Shared.Rent(blockSize);
//#if NETCOREAPP2_1
//                        var task = _ws.ReceiveAsync((Memory<byte>)buffer, _cts.Token);
//                        result = task.IsCompletedSuccessfully ? task.Result : await task;
//#else
//                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
//#endif
//                    }
//                    else
//                    {
//                        break;
//                    }
//                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _cts.Cancel();
                    // TODO remove the line, the socket is already closed
                    //await _ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    _tcs.TrySetResult(null);
                    //ms.Dispose();
                    //ms = null;
                }
                else if (_cts.IsCancellationRequested)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", CancellationToken.None);
                    _tcs.TrySetResult(null);
                    //ms.Dispose();
                    //ms = null;
                }
            }
            catch (Exception ex)
            {
                if (_cts.IsCancellationRequested)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", CancellationToken.None);
                    _tcs.TrySetResult(null);
                }
                else
                {
                    _tcs.TrySetResult(ex);
                }
                //ms.Dispose();
                //ms = null;
            }
            finally
            {
                if (moreThanOneBlock)
                {
                    ArrayPool<byte>.Shared.Return(buffer, false);
                }
                _readSemaphore.Release();
            }
            return RecyclableMemoryStreamManager.Default.GetStream(null, buffer, 0, len);
        }

        //private ValueTask<ValueWebSocketReceiveResult> ProcessTask(Task<ValueWebSocketReceiveResult> t)
        //{
        //    return new ValueTask<ValueWebSocketReceiveResult>(t);
        //}

        public override Task<Exception> Completion => _tcs.Task;
    }
}
