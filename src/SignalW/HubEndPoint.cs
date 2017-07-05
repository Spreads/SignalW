// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spreads.SignalW.Connections;

namespace Spreads.SignalW {

    public class HubEndPoint<THub> : HubEndPoint<THub, IClientProxy> where THub : Hub<IClientProxy> {

        public HubEndPoint(HubLifetimeManager<THub> lifetimeManager,
                           IHubContext<THub> hubContext,
                           ILogger<HubEndPoint<THub>> logger,
                           IServiceScopeFactory serviceScopeFactory) : base(lifetimeManager, hubContext, logger, serviceScopeFactory) {
        }
    }

    public class HubEndPoint<THub, TClient> where THub : Hub<TClient> {
        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly IHubContext<THub, TClient> _hubContext;
        private readonly ILogger<HubEndPoint<THub, TClient>> _logger;
        private THub _hub;
        private readonly IServiceScope _scope;
        private IHubActivator<THub, TClient> _hubActivator;

        public HubEndPoint(HubLifetimeManager<THub> lifetimeManager,
                           IHubContext<THub, TClient> hubContext,
                           ILogger<HubEndPoint<THub, TClient>> logger,
                           IServiceScopeFactory serviceScopeFactory) {
            _lifetimeManager = lifetimeManager;
            _hubContext = hubContext;
            _logger = logger;
            _scope = serviceScopeFactory.CreateScope();
            _hubActivator = _scope.ServiceProvider.GetRequiredService<IHubActivator<THub, TClient>>();
            _hub = _hubActivator.Create();
        }

        public async Task OnConnectedAsync(Connection connection) {
            // TODO: Dispatch from the caller
            await Task.Yield();
            Exception exception = null;
            try {
                await _lifetimeManager.OnConnectedAsync(connection);

                InitializeHub(_hub, connection);
                await _hub.OnConnectedAsync();

                await DispatchMessagesAsync(connection);
            } catch (Exception ex) {
                _logger.LogError(0, ex, "Error when processing requests.");
                exception = ex;
                connection.Channel.TryComplete();
            } finally {
                if (connection.Channel.Completion.Status == TaskStatus.RanToCompletion
                    && connection.Channel.Completion.Result != null) {
                    exception = connection.Channel.Completion.Result;
                }

                try {
                    await _hub.OnDisconnectedAsync(exception);
                } finally {
                    _hubActivator.Release(_hub);
                    _scope.Dispose();
                }

                await _lifetimeManager.OnDisconnectedAsync(connection);
            }
        }

        private async Task DispatchMessagesAsync(Connection connection) {
            while (true) {
                var payload = await connection.Channel.ReadAsync();

                // Is there a better way of detecting that a connection was closed?
                if (payload == null) {
                    break;
                }

                if (_logger.IsEnabled(LogLevel.Debug)) {
                    _logger.LogDebug($"Received hub invocation payload length: {payload.Length}");
                }

                await _hub.OnReceiveAsync(payload);

                payload.Dispose();
            }
        }

        private void InitializeHub(THub hub, Connection connection) {
            hub.Clients = _hubContext.Clients;
            hub.Context = new HubCallerContext(connection);
            hub.Groups = new GroupManager<THub>(connection, _lifetimeManager);
        }
    }
}
