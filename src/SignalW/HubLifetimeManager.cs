// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Spreads.SignalW.Connections;

namespace Spreads.SignalW
{
    public abstract class HubLifetimeManager<THub>
    {
        public abstract Task OnConnectedAsync(Connection connection);

        public abstract Task OnDisconnectedAsync(Connection connection);

        public abstract ValueTask InvokeAllAsync(MemoryStream payload);

        public abstract ValueTask InvokeConnectionAsync(string connectionId, MemoryStream payload);

        public abstract ValueTask InvokeExceptConnectionAsync(string connectionId, MemoryStream payload);

        public abstract ValueTask InvokeGroupAsync(string groupName, MemoryStream payload);

        public abstract ValueTask InvokeUserAsync(string userId, MemoryStream payload);

        public abstract ValueTask InvokeExceptUserAsync(string userId, MemoryStream payload);

        public abstract ValueTask AddGroupAsync(Connection connection, string groupName);

        public abstract ValueTask RemoveGroupAsync(Connection connection, string groupName);
    }

}
