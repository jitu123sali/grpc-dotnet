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
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grpc.AspNetCore.FunctionalTests.Infrastructure
{
    public class GrpcTestFixture<TStartup> : IDisposable where TStartup : class
    {
        private readonly InProcessTestServer _server;

        public GrpcTestFixture(Action<IServiceCollection>? initialConfigureServices = null)
        {
            LoggerFactory = new LoggerFactory();

            Action<IServiceCollection> configureServices = services =>
            {
                // Registers a service for tests to add new methods
                services.AddSingleton<DynamicGrpcServiceRegistry>();
            };

            _server = new InProcessTestServer<TStartup>(services =>
            {
                initialConfigureServices?.Invoke(services);
                configureServices(services);
            });

            _server.StartServer();

            DynamicGrpc = _server.Host!.Services.GetRequiredService<DynamicGrpcServiceRegistry>();

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            Client = new HttpClient();
            Client.DefaultRequestVersion = new Version(2, 0);
            Client.BaseAddress = new Uri(_server.Url!);
        }

        public ILoggerFactory LoggerFactory { get; }
        public DynamicGrpcServiceRegistry DynamicGrpc { get; }

        public HttpClient Client { get; }

        internal event Action<LogRecord> ServerLogged
        {
            add => _server.ServerLogged += value;
            remove => _server.ServerLogged -= value;
        }

        public void Dispose()
        {
            Client.Dispose();
            _server.Dispose();
        }
    }
}
