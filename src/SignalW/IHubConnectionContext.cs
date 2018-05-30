// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Spreads.SignalW
{
    public interface IHubConnectionContext
    {
        IClientProxy All { get; }

        IClientProxy Client(string connectionId);

        IClientProxy ExceptClient(string connectionId);

        IClientProxy Group(string groupName);

        IClientProxy User(string userId);

        IClientProxy ExceptUser(string userId);
    }
}
