// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Spreads.SignalW
{
    public interface IHubContext<THub, TClient>
    {
        IHubConnectionContext<TClient> Clients { get; }
    }

    public interface IHubContext<THub> : IHubContext<THub, IClientProxy>
    {
    }
}
