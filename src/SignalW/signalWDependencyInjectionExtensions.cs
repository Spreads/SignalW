// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Spreads.SignalW {

    public static class SignalWDependencyInjectionExtensions {

        public static ISignalWBuilder AddSignalW(this IServiceCollection services) {
            services.AddSingleton(typeof(HubLifetimeManager<>), typeof(DefaultHubLifetimeManager<>));
            services.AddSingleton(typeof(IHubContext<>), typeof(HubContext<>));
            services.AddScoped(typeof(HubEndPoint<>), typeof(HubEndPoint<>));
            services.AddScoped(typeof(IHubActivator<,>), typeof(DefaultHubActivator<,>));
            services.AddRouting();
            
            return new SignalWBuilder(services);
        }

        public static ISignalWBuilder AddSignalWOptions(this ISignalWBuilder builder) {
            return builder;
        }
    }
}
