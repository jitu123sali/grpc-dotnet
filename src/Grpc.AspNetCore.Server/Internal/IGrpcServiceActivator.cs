﻿#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;

namespace Grpc.AspNetCore.Server.Internal
{
    /// <summary>
    /// A <typeparamref name="TGrpcService"/> activator abstraction.
    /// </summary>
    /// <typeparam name="TGrpcService">The service type.</typeparam>
    internal interface IGrpcServiceActivator<TGrpcService> where TGrpcService : class
    {
        /// <summary>
        /// Creates a service.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>The created service.</returns>
        GrpcActivatorHandle<TGrpcService> Create(IServiceProvider serviceProvider);

        /// <summary>
        /// Releases the specified service.
        /// </summary>
        /// <param name="service">The service to release.</param>
        void Release(in GrpcActivatorHandle<TGrpcService> service);
    }
}
