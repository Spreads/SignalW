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
        public abstract ValueTask WriteAsync(MemoryStream item, bool disposeItem = false);

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

        //private readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(1, 1);
        //private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);

        public WsChannel(WebSocket ws, Format format)
        {
            _ws = ws;
            _format = format;
            _tcs = new TaskCompletionSource<Exception>();
            _cts = new CancellationTokenSource();
        }

        public override ValueTask WriteAsync(MemoryStream item, bool disposeItem = false)
        {
            item.Position = 0;
            if (!(item is RecyclableMemoryStream rms)) // no supposed case
            {
                rms = RecyclableMemoryStreamManager.Default.GetStream(null, checked((int)item.Length));
                item.CopyTo(rms);
                if (disposeItem)
                {
                    item.Dispose();
                }
            }
            try
            {
                if (!_writeSemaphore.Wait(0))
                {
                    return ContinueWriteAsync(rms, true);
                }

                // TODO for now assume struct enums are free and do not need dispose, maybe refacor RMS later
                using (var e = rms.Chunks.GetEnumerator())
                {
                    var type = _format == Format.Binary
                        ? WebSocketMessageType.Binary
                        : WebSocketMessageType.Text;
                    ArraySegment<byte> chunk;
                    if (!e.MoveNext())
                    {
                        throw new ArgumentException("Item is empty");
                    }

                    chunk = e.Current;

                    var endOfMessage = !e.MoveNext();

                    if (!endOfMessage) // mutipart async
                    {
                        return ContinueWriteAsync(rms, false);
                    }

#if NETCOREAPP2_1
                    var result = _ws.SendAsync((ReadOnlyMemory<byte>)chunk, type, true, _cts.Token);
#else
                    var result = new ValueTask(_ws.SendAsync(chunk, type, true, _cts.Token));
#endif
                    _writeSemaphore.Release();
                    return result;
                }
            }
            catch (Exception ex)
            {
                _writeSemaphore.Release();
                return CloseAsync(ex);
            }
            finally
            {
                if (disposeItem)
                {
                    rms.Dispose();
                }
            }
        }

        private async ValueTask ContinueWriteAsync(RecyclableMemoryStream rms, bool doAwaitSemaphore)
        {
            try
            {
                if (doAwaitSemaphore)
                {
                    await _writeSemaphore.WaitAsync(_cts.Token);
                }

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
                            await _ws.SendAsync((ReadOnlyMemory<byte>) chunk, type, endOfMessage, _cts.Token);

#else
                        await _ws.SendAsync(chunk, type, endOfMessage, _cts.Token);
#endif
                            if (endOfMessage)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await CloseAsync(ex);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        private async ValueTask CloseAsync(Exception ex)
        {
            // Write not finished, Completion indicates why (null - cancelled)

            if (_cts.IsCancellationRequested)
            {
                _tcs.TrySetResult(null);
            }
            else
            {
                _cts.Cancel();
                _tcs.TrySetResult(ex);
            }
            // https://tools.ietf.org/html/rfc6455#section-5.5.1
            // always needed, even when we received Close
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure",
                CancellationToken.None);
        }

        public override ValueTask<bool> TryComplete()
        {
            if (_cts.IsCancellationRequested)
            {
                _tcs.TrySetResult(null);
                return new ValueTask<bool>(false);
            }
            _cts.Cancel();
            return new ValueTask<bool>(true);
        }

        public override ValueTask<MemoryStream> ReadAsync()
        {
            RecyclableMemoryStream rms = null;
            try
            {
                if (!_readSemaphore.Wait(0))
                {
                    return ContinueReadAsync(null, true, null);
                }
#if NETCOREAPP2_1
                if (_ws.State != WebSocketState.Open)
                {
                    Console.WriteLine("catch me");
                }
                // Do a 0 byte read so that idle connections don't allocate a buffer when waiting for a read
                var t = _ws.ReceiveAsync(Memory<byte>.Empty, CancellationToken.None);

                if (!t.IsCompletedSuccessfully)
                {
                    return ContinueReadAsync(null, false, t.AsTask());
                }

                // this will create the first chunk with default size

                rms = RecyclableMemoryStreamManager.Default.GetStream();

                var result = t.Result;
                if (result.MessageType != WebSocketMessageType.Close)
                {
#pragma warning disable 618
                    // Hidden access to internals... maybe fix someday or just migrate to Pipes/Memory Sequences
                    var firstChunk = rms.Chunks.RawChunks[0];
#pragma warning restore 618

                    if (_ws.State != WebSocketState.Open)
                    {
                        Console.WriteLine("catch me");
                    }
                    // we have data, must be able to read
                    result = _ws.ReceiveAsync((Memory<byte>)firstChunk, _cts.Token).GetAwaiter().GetResult();
                    rms.SetLength(result.Count);

                    if (result.EndOfMessage)
                    {
                        _readSemaphore.Release();
                        return new ValueTask<MemoryStream>(rms);
                    }

                    return ContinueReadAsync(rms, false, null);
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _cts.Cancel();
                }
#else
                return ContinueReadAsync(null, false, null);
#endif

#pragma warning disable 4014
                CloseAsync(null);
#pragma warning restore 4014
            }
            catch (Exception ex)
            {
#pragma warning disable 4014
                CloseAsync(ex);
#pragma warning restore 4014
            }
            finally
            {
                // 
            }
            rms?.Dispose();
            return new ValueTask<MemoryStream>((RecyclableMemoryStream)null);
        }

        private async ValueTask<MemoryStream> ContinueReadAsync(RecyclableMemoryStream rms, bool doAwaitSemaphore, Task pending)
        {
            if (doAwaitSemaphore) { await _readSemaphore.WaitAsync(_cts.Token); }

            if (pending != null)
            {
                // we know it had empty buffer
                await pending;
            }

            // we have rms with length set to zero of first sync result
            rms = rms ?? RecyclableMemoryStreamManager.Default.GetStream();

            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
#if NETCOREAPP2_1
                if (_ws.State != WebSocketState.Open)
                {
                    Console.WriteLine("catch me");
                }
                // Do a 0 byte read so that idle connections don't allocate a buffer when waiting for a read
                var result = await _ws.ReceiveAsync(Memory<byte>.Empty, CancellationToken.None);
                if (result.MessageType != WebSocketMessageType.Close)
                {
                    if (_ws.State != WebSocketState.Open)
                    {
                        Console.WriteLine("catch me");
                    }
                    result = _ws.ReceiveAsync((Memory<byte>)buffer, _cts.Token).GetAwaiter().GetResult();
                }
#else
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
#endif
                while (result.MessageType != WebSocketMessageType.Close && !_cts.IsCancellationRequested)
                {
                    rms.Write(buffer, 0, result.Count);

                    if (!result.EndOfMessage)
                    {
#if NETCOREAPP2_1
                        result = await _ws.ReceiveAsync((Memory<byte>)buffer, _cts.Token);
                        if (_ws.State != WebSocketState.Open)
                        {
                            Console.WriteLine("catch me");
                        }
#else
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
#endif
                    }
                    else
                    {
                        return rms;
                    }
                }

                // closing now
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _cts.Cancel();
                }

                await CloseAsync(null);
            }
            catch (Exception ex)
            {
                await CloseAsync(ex);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                _readSemaphore.Release();
            }
            rms.Dispose();
            return null;
        }

        public override Task<Exception> Completion => _tcs.Task;
    }
}
