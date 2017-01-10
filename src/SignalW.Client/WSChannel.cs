using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DataSpreads.SignalW {

    public abstract class Channel {

        public abstract Task<bool> WriteAsync(MemoryStream item);

        public abstract bool TryComplete();

        public abstract Task<MemoryStream> ReadAsync();

        public abstract Task Completion { get; }
    }

    public class WsChannel : Channel {
        private readonly WebSocket _ws;
        private readonly Format _format;
        private TaskCompletionSource<bool> _tcs;
        private CancellationTokenSource _cts;
        private SemaphoreSlim _readSemaphore = new SemaphoreSlim(1, 1);
        private SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);

        public WsChannel(WebSocket ws, Format format) {
            _ws = ws;
            _format = format;
            _tcs = new TaskCompletionSource<bool>();
            _cts = new CancellationTokenSource();
        }

        public override async Task<bool> WriteAsync(MemoryStream item) {
            // According to https://github.com/aspnet/WebSockets/blob/4e2ecf8a63b9fc78175d1ef62bf2b1918b8b3986/src/Microsoft.AspNetCore.WebSockets/Internal/fx/src/System.Net.WebSockets.Client/src/System/Net/WebSockets/ManagedWebSocket.cs#L20
            // Only a single writer/reader at each moment is OK
            // But even otherwise, for multiframe messages we must process the entire stream
            // and do not allow other writers to push their frames before we finished processing the stream.
            // We assume that the memory stream is already complete and do not wait for its end,
            // just iterate over chunks, therefore it's safe for many writer just to wait for the semaphore
            await _writeSemaphore.WaitAsync(_cts.Token);
            try {
                var rms = item as RecyclableMemoryStream;
                if (rms != null) {
                    try {
                        foreach (var chunk in rms.Chunks) {
                            var type = _format == Format.Binary
                                ? WebSocketMessageType.Binary
                                : WebSocketMessageType.Text;
                            await _ws.SendAsync(chunk, type, true, _cts.Token);
                        }
                        return true;
                    } catch {
                        if (!_cts.IsCancellationRequested) {
                            // cancel readers and other writers waiting on the semaphore
                            _cts.Cancel();
                        }
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", CancellationToken.None);
                        _tcs.TrySetResult(true);
                        return false;
                    }
                }
                rms = RecyclableMemoryStreamManager.Instance.GetStream() as RecyclableMemoryStream;
                item.CopyTo(rms);
                // will recurse only once
                return await WriteAsync(rms);
            } finally {
                _writeSemaphore.Release();
            }
        }

        public override bool TryComplete() {
            if (_cts.IsCancellationRequested) return false;
            _cts.Cancel();
            return true;
        }

        public override async Task<MemoryStream> ReadAsync() {
            var blockSize = RecyclableMemoryStreamManager.Instance.BlockSize;
            byte[] buffer = null;
            bool moreThanOneBlock = false;
            // this will create the first chunk with default size
            var ms = (RecyclableMemoryStream)RecyclableMemoryStreamManager.Instance.GetStream("WSChannel.ReadAsync", blockSize);
            WebSocketReceiveResult result;
            await _readSemaphore.WaitAsync(_cts.Token);
            try {
                buffer = ms.blocks[0];
                result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                while (!result.CloseStatus.HasValue && !_cts.IsCancellationRequested) {
                    // we write to the first block directly, to save one copy operation for
                    // small messages (< blockSize), which should be the majorority of cases
                    if (!moreThanOneBlock) {
                        ms.length = result.Count;
                        ms.Position = result.Count;
                    } else {
                        ms.Write(buffer, 0, result.Count);
                    }
                    if (!result.EndOfMessage) {
                        moreThanOneBlock = true;
                        buffer = ArrayPool<byte>.Shared.Rent(blockSize);
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    } else {
                        break;
                    }
                }
                if (result.CloseStatus.HasValue) {
                    _cts.Cancel();
                    await _ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    _tcs.TrySetResult(true);
                    ms.Dispose();
                    ms = null;
                } else if (_cts.IsCancellationRequested) {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", CancellationToken.None);
                    _tcs.TrySetResult(true);
                    ms.Dispose();
                    ms = null;
                }
            } catch {
                if (_cts.IsCancellationRequested) {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", CancellationToken.None);
                    _tcs.TrySetResult(true);
                }
                ms.Dispose();
                ms = null;
            } finally {
                if (moreThanOneBlock) {
                    ArrayPool<byte>.Shared.Return(buffer, false);
                }
                _readSemaphore.Release();
            }
            return ms;
        }

        public override Task Completion => _tcs.Task;
    }
}
