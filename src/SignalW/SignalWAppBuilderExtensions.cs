// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Spreads.SignalW.Client;
using Spreads.SignalW.Connections;
using Spreads.Utils;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Spreads.SignalW
{
    public static class SignalWAppBuilderExtensions
    {
        public static IApplicationBuilder UseSignalW(this IApplicationBuilder app, Action<HubRouteBuilder> configure)
        {
            app.UseWebSockets(new WebSocketOptions());
            configure(new HubRouteBuilder(app));
            return app;
        }
    }

    public class HubRouteBuilder
    {
        private readonly IApplicationBuilder _app;

        public HubRouteBuilder(IApplicationBuilder app)
        {
            _app = app;
        }

        private int count;
        private long maxElapsed;

        protected async Task ProcessAsync(WebSocket websocket)
        {
            while (true)
            {
                // using (Benchmark.Run("10k messages", 100000, true))
                {
                    while (true)
                    {
                        count++;
#if NETCOREAPP2_1
                        // Bufferless async wait (well it's like 14 bytes)
                        var t = websocket.ReceiveAsync(Memory<byte>.Empty, default);
                        var result = t.IsCompleted ? t.Result : await t;

                        // If we got a close then send the close frame back
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            websocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", default).Wait();
                            return;
                        }
                        else
                        {
                            // Rent 4K
                            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
                            try
                            {
                                var memory = buffer.AsMemory();
                                // This should always be synchronous
                                var task = websocket.ReceiveAsync(memory, default);

                                Debug.Assert(task.IsCompleted);

                                result = task.GetAwaiter().GetResult();
                                await websocket.SendAsync(memory.Slice(0, result.Count), result.MessageType, result.EndOfMessage, default);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }
#endif
                        if (count % 50000 == 0)
                        {
                            var sw = new Stopwatch();
                            sw.Start();
                            GC.Collect(0, GCCollectionMode.Optimized, false);
                            sw.Stop();
                            var micros = (sw.ElapsedTicks * 1000000) / Stopwatch.Frequency;
                            if (micros > maxElapsed & count > 1000000)
                            {
                                maxElapsed = micros;
                            }
                            Console.WriteLine("GC time: " + micros + " ; max: " + maxElapsed);
                            Console.WriteLine(GC.CollectionCount(0) + " | " + GC.CollectionCount(1) + " | " +
                                              GC.CollectionCount(2));
                            Console.WriteLine(GC.GetTotalMemory(false));
                        }
                    }
                }
                // Benchmark.Dump();

            }
        }

        public void MapHub<THub>(string path, Format format) where THub : Hub, new()
        {
            _app.UseRouter(rb =>
            {
                Console.WriteLine("debug me here");
                rb.MapRoute($"{path}", (RequestDelegate)(async context =>
                {
                    //var identity = (ClaimsIdentity)context.User.Identity;
                    //var email = identity.IsAuthenticated ? identity.Claims.First(c => c.Type == ClaimTypes.Email).Value : "no email";
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        var connectionId =
                            context.Request.Query.ContainsKey("connectionId")
                            ? (string)context.Request.Query["connectionId"]
                            : Guid.NewGuid().ToString();

                        //await ProcessAsync(webSocket);
                        //return;

                        var channel = new WsChannel(webSocket, format);

                        var connection = new Connection
                        {
                            User = context.User,
                            Channel = channel,
                            ConnectionId = connectionId
                        };
                        connection.Metadata.Format = format;
                        // TODO check existing connection by ID, User must match on reconnect

                        // now pass this connection to a method that starts processing
                        var endpoint = context.RequestServices.GetRequiredService<HubEndPoint<THub>>();
                        var processingTask = endpoint.OnConnectedAsync(connection);

                        await Task.WhenAll(processingTask, connection.Channel.Completion);

                        return;
                    }
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                }));
            });
        }
    }
}
