// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Spreads.SignalW.Connections;

namespace Spreads.SignalW
{

    // NB TODO here should implement sending to a single user
    // 
    public class DefaultHubLifetimeManager<THub> : HubLifetimeManager<THub>
    {
        private readonly ConnectionList _connections = new ConnectionList();

        public override Task InvokeExceptUserAsync(string userId, MemoryStream payload)
        {
            return InvokeAllWhere(payload, connection => connection.User.Identity.Name != userId);
        }

        public override Task AddGroupAsync(Connection connection, string groupName)
        {
            var groups = connection.Metadata.GetOrAdd("groups", _ => new HashSet<string>());

            lock (groups)
            {
                groups.Add(groupName);
            }

            return TaskCache.CompletedTask;
        }

        public override Task RemoveGroupAsync(Connection connection, string groupName)
        {
            var groups = connection.Metadata.Get<HashSet<string>>("groups");

            lock (groups)
            {
                groups.Remove(groupName);
            }

            return TaskCache.CompletedTask;
        }

        public override Task InvokeAllAsync(MemoryStream payload)
        {
            return InvokeAllWhere(payload, c => true);
        }

        private async Task InvokeAllWhere(MemoryStream payload, Func<Connection, bool> include)
        {
            // TODO list pool like in Roslyn
            var tasks = new List<ValueTask<bool>>(_connections.Count);

            foreach (var connection in _connections)
            {
                if (!include(connection))
                {
                    continue;
                }

                var task = connection.Channel.WriteAsync(payload);
                if (!task.IsCompletedSuccessfully)
                {
                    tasks.Add(task);
                }
            }

            foreach (var task in tasks)
            {
                if (!task.IsCompleted)
                {
                    await task;
                }
            }

            return;
        }

        public override Task InvokeConnectionAsync(string connectionId, MemoryStream payload)
        {
            var connection = _connections[connectionId];
            // TODO ValueTask
            return connection.Channel.WriteAsync(payload).AsTask();
        }

        public override Task InvokeExceptConnectionAsync(string connectionId, MemoryStream payload)
        {
            return InvokeAllWhere(payload, connection => connection.ConnectionId != connectionId);
        }

        public override Task InvokeGroupAsync(string groupName, MemoryStream payload)
        {
            return InvokeAllWhere(payload, connection =>
            {
                var groups = connection.Metadata.Get<HashSet<string>>("groups");
                return groups?.Contains(groupName) == true;
            });
        }

        public override Task InvokeUserAsync(string userId, MemoryStream payload)
        {
            return InvokeAllWhere(payload, connection => connection.User.Identity.Name == userId);
        }

        public override Task OnConnectedAsync(Connection connection)
        {
            _connections.Add(connection);
            return TaskCache.CompletedTask;
        }

        public override Task OnDisconnectedAsync(Connection connection)
        {
            _connections.Remove(connection);
            return TaskCache.CompletedTask;
        }
    }
}
