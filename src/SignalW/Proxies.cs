// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Spreads.SignalW.Connections;

namespace Spreads.SignalW {

    public class UserProxy<THub> : IClientProxy {
        private readonly string _userId;
        private readonly HubLifetimeManager<THub> _lifetimeManager;

        public UserProxy(HubLifetimeManager<THub> lifetimeManager, string userId) {
            _lifetimeManager = lifetimeManager;
            _userId = userId;
        }

        public ValueTask InvokeAsync(MemoryStream payload) {
            return _lifetimeManager.InvokeUserAsync(_userId, payload);
        }
    }

    public class ExceptUserProxy<THub> : IClientProxy {
        private readonly string _userId;
        private readonly HubLifetimeManager<THub> _lifetimeManager;

        public ExceptUserProxy(HubLifetimeManager<THub> lifetimeManager, string userId) {
            _lifetimeManager = lifetimeManager;
            _userId = userId;
        }

        public ValueTask InvokeAsync(MemoryStream payload) {
            return _lifetimeManager.InvokeExceptUserAsync(_userId, payload);
        }
    }

    public class GroupProxy<THub> : IClientProxy {
        private readonly string _groupName;
        private readonly HubLifetimeManager<THub> _lifetimeManager;

        public GroupProxy(HubLifetimeManager<THub> lifetimeManager, string groupName) {
            _lifetimeManager = lifetimeManager;
            _groupName = groupName;
        }

        public ValueTask InvokeAsync(MemoryStream payload) {
            return _lifetimeManager.InvokeGroupAsync(_groupName, payload);
        }
    }

    public class AllClientProxy<THub> : IClientProxy {
        private readonly HubLifetimeManager<THub> _lifetimeManager;

        public AllClientProxy(HubLifetimeManager<THub> lifetimeManager) {
            _lifetimeManager = lifetimeManager;
        }

        public ValueTask InvokeAsync(MemoryStream payload) {
            return _lifetimeManager.InvokeAllAsync(payload);
        }
    }

    public class SingleClientProxy<THub> : IClientProxy {
        private readonly string _connectionId;
        private readonly HubLifetimeManager<THub> _lifetimeManager;

        public SingleClientProxy(HubLifetimeManager<THub> lifetimeManager, string connectionId) {
            _lifetimeManager = lifetimeManager;
            _connectionId = connectionId;
        }

        public ValueTask InvokeAsync(MemoryStream payload) {
            return _lifetimeManager.InvokeConnectionAsync(_connectionId, payload);
        }
    }

    public class ExceptClientProxy<THub> : IClientProxy {
        private readonly string _connectionId;
        private readonly HubLifetimeManager<THub> _lifetimeManager;

        public ExceptClientProxy(HubLifetimeManager<THub> lifetimeManager, string connectionId) {
            _lifetimeManager = lifetimeManager;
            _connectionId = connectionId;
        }

        public ValueTask InvokeAsync(MemoryStream payload) {
            return _lifetimeManager.InvokeExceptConnectionAsync(_connectionId, payload);
        }
    }

    public class GroupManager<THub> : IGroupManager {
        private readonly Connection _connection;
        private readonly HubLifetimeManager<THub> _lifetimeManager;

        public GroupManager(Connection connection, HubLifetimeManager<THub> lifetimeManager) {
            _connection = connection;
            _lifetimeManager = lifetimeManager;
        }

        public ValueTask AddAsync(string groupName) {
            return _lifetimeManager.AddGroupAsync(_connection, groupName);
        }

        public ValueTask RemoveAsync(string groupName) {
            return _lifetimeManager.RemoveGroupAsync(_connection, groupName);
        }
    }
}
