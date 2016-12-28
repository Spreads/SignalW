// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using DataSpreads.SignalW.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataSpreads.SignalW {

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

        public HubEndPoint(HubLifetimeManager<THub> lifetimeManager,
                           IHubContext<THub, TClient> hubContext,
                           ILogger<HubEndPoint<THub, TClient>> logger,
                           IServiceScopeFactory serviceScopeFactory) {
            _lifetimeManager = lifetimeManager;
            _hubContext = hubContext;
            _logger = logger;
            _scope = serviceScopeFactory.CreateScope();
        }

        public async Task OnConnectedAsync(Connection connection) {
            // TODO: Dispatch from the caller
            await Task.Yield();
            var created = false;
            try {
                await _lifetimeManager.OnConnectedAsync(connection);

                _hub = CreateHub(_scope.ServiceProvider, connection, out created);

                await _hub.OnConnectedAsync();

                await DispatchMessagesAsync(connection);
            } finally {
                await _hub.OnDisconnectedAsync();

                if (created) {
                    _hub.Dispose();
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
            }
        }

        private THub CreateHub(IServiceProvider provider, Connection connection, out bool created) {
            var hub = provider.GetService<THub>();
            created = false;

            if (hub == null) {
                hub = ActivatorUtilities.CreateInstance<THub>(provider);
                created = true;
            }

            hub.Clients = _hubContext.Clients;
            hub.Context = new HubCallerContext(connection);
            hub.Groups = new GroupManager<THub>(connection, _lifetimeManager);

            return hub;
        }
    }
}
