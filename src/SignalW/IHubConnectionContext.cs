// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace DataSpreads.SignalW
{
    public interface IHubConnectionContext<TClient>
    {
        TClient All { get; }

        TClient Client(string connectionId);

        TClient ExceptClient(string connectionId);

        TClient Group(string groupName);

        TClient User(string userId);

        TClient ExceptUser(string userId);
    }
}
