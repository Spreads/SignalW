// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using DataSpreads.SignalW.Connections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DataSpreads.SignalW {
    public static class SignalWAppBuilderExtensions {
        public static IApplicationBuilder UseSignalW(this IApplicationBuilder app, Action<HubRouteBuilder> configure) {
            app.UseWebSockets(new WebSocketOptions());
            configure(new HubRouteBuilder(app));
            return app;
        }
    }

    public class HubRouteBuilder {
        private readonly IApplicationBuilder _app;

        public HubRouteBuilder(IApplicationBuilder app) {
            _app = app;
        }

        public void MapHub<THub>(string path, Format format) where THub : Hub {

            _app.UseRouter(rb => {
                rb.MapRoute($"{path}", (RequestDelegate)(async context => {
                    //var identity = (ClaimsIdentity)context.User.Identity;
                    //var email = identity.IsAuthenticated ? identity.Claims.First(c => c.Type == ClaimTypes.Email).Value : "no email";
                    if (context.WebSockets.IsWebSocketRequest) {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        var connectionId =
                            context.Request.Query.ContainsKey("connectionId")
                            ? (string)context.Request.Query["connectionId"]
                            : Guid.NewGuid().ToString();
                        var channel = new WsChannel(webSocket, format);

                        var connection = new Connection {
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
