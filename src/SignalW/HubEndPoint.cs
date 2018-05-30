// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spreads.SignalW.Connections;
using System;
using System.Threading.Tasks;

namespace Spreads.SignalW
{

    public class HubEndPoint<THub> where THub : Hub, new()
    {
        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly IHubContext<THub> _hubContext;
        private THub _hub;
        // private readonly IServiceScope _scope;

        public HubEndPoint(HubLifetimeManager<THub> lifetimeManager,
                           IHubContext<THub> hubContext)
        {
            _lifetimeManager = lifetimeManager;
            _hubContext = hubContext;
            _hub = Activator.CreateInstance<THub>();
        }

        public async Task OnConnectedAsync(Connection connection)
        {
            // TODO: Dispatch from the caller
            await Task.Yield();
            Exception exception = null;
            try
            {
                await _lifetimeManager.OnConnectedAsync(connection);

                InitializeHub(_hub, connection);
                await _hub.OnConnectedAsync();

                await DispatchMessagesAsync(connection);
            }
            catch (Exception ex)
            {
                // TODO error logger
                // _logger.LogError(0, ex, "Error when processing requests.");
                exception = ex;
                connection.Channel.TryComplete();
            }
            finally
            {
                if (connection.Channel.Completion.Status == TaskStatus.RanToCompletion
                    && connection.Channel.Completion.Result != null)
                {
                    exception = connection.Channel.Completion.Result;
                }

                try
                {
                    await _hub.OnDisconnectedAsync(exception);
                }
                finally
                {
                    _hub.Dispose();
                }

                await _lifetimeManager.OnDisconnectedAsync(connection);
            }
        }

        private async Task DispatchMessagesAsync(Connection connection)
        {
            while (true)
            {
                var task = connection.Channel.ReadAsync();
                var payload = task.IsCompletedSuccessfully ? task.Result : await task;

                // Is there a better way of detecting that a connection was closed?
                if (payload == null)
                {
                    break;
                }

                //if (_logger.IsEnabled(LogLevel.Debug))
                //{
                //    _logger.LogDebug($"Received hub invocation payload length: {payload.Length}");
                //}

                await _hub.OnReceiveAsync(payload);

                payload.Dispose();
            }
        }

        private void InitializeHub(THub hub, Connection connection)
        {
            hub.Clients = _hubContext.Clients;
            hub.Context = new HubCallerContext(connection);
            hub.Groups = new GroupManager<THub>(connection, _lifetimeManager);
        }
    }
}
