#region Copyright notice and license

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
using System.Threading;
using Grpc.AspNetCore.Server.Internal;
using Grpc.Core.Interceptors;
using Moq;
using NUnit.Framework;

namespace Grpc.AspNetCore.Server.Tests
{
    [TestFixture]
    public class DefaultGrpcInterceptorActivatorTests
    {
        public class GrpcInterceptor : Interceptor
        {
            public GrpcInterceptor() { }

            public bool Disposed { get; private set; } = false;
            public void Dispose() => Disposed = true;
        }

        public class GrpcIntArgumentInterceptor : Interceptor
        {
            public GrpcIntArgumentInterceptor() { }

            public GrpcIntArgumentInterceptor(int x)
            {
                X = x;
            }

            public int X { get; }
            public bool Disposed { get; private set; } = false;
            public void Dispose() => Disposed = true;
        }

        public class GrpcIntMutexArgumentInterceptor : Interceptor
        {
            public GrpcIntMutexArgumentInterceptor(int x, Mutex mutex)
            {
                X = x;
                Mutex = mutex;
            }

            public int X { get; }
            public Mutex Mutex { get; }
            public bool Disposed { get; private set; } = false;

            public void Dispose() => Disposed = true;
        }

        public class DisposableGrpcInterceptor : Interceptor, IDisposable
        {
            public bool Disposed { get; private set; } = false;
            public void Dispose() => Disposed = true;
        }

        [Test]
        public void Create_NotResolvedFromServiceProvider_CreatedByActivator()
        {
            // Arrange
            var activator = new DefaultGrpcInterceptorActivator<GrpcInterceptor>();

            // Act
            var handle = activator.Create(Mock.Of<IServiceProvider>(), CreateRegistration<GrpcInterceptor>());

            // Assert
            Assert.NotNull(handle.Instance);
            Assert.IsTrue(handle.Created);
        }

        [Test]
        public void Create_ResolvedFromServiceProvider_NotCreatedByActivator()
        {
            // Arrange
            var interceptor = new GrpcInterceptor();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(GrpcInterceptor)))
                .Returns(interceptor);
            var activator = new DefaultGrpcInterceptorActivator<GrpcInterceptor>();

            // Act
            var handle = activator.Create(mockServiceProvider.Object, CreateRegistration<GrpcInterceptor>());

            // Assert
            Assert.AreSame(handle.Instance, interceptor);
            Assert.IsFalse(handle.Created);
        }

        [Test]
        public void Create_ServiceRegistrationAndExplicitArgs_CreatedByActivator()
        {
            // Arrange
            var interceptor = new GrpcIntArgumentInterceptor();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(GrpcIntArgumentInterceptor)))
                .Returns(interceptor);

            // Act
            var handle = new DefaultGrpcInterceptorActivator<GrpcIntArgumentInterceptor>().Create(
                mockServiceProvider.Object,
                CreateRegistration<GrpcIntArgumentInterceptor>(10));

            // Assert
            Assert.AreNotSame(interceptor, handle.Instance);
            Assert.AreEqual(10, ((GrpcIntArgumentInterceptor)handle.Instance).X);
            Assert.IsTrue(handle.Created);
        }

        [Test]
        public void Create_ExplicitArgsAndServiceArgs_CreatedByActivatorWithServiceArgsResolved()
        {
            var mutex = new Mutex();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(Mutex)))
                .Returns(mutex);

            var handle = new DefaultGrpcInterceptorActivator<GrpcIntMutexArgumentInterceptor>().Create(
                mockServiceProvider.Object,
                CreateRegistration<GrpcIntMutexArgumentInterceptor>(10));

            Assert.AreEqual(10, ((GrpcIntMutexArgumentInterceptor)handle.Instance).X);
            Assert.AreEqual(mutex, ((GrpcIntMutexArgumentInterceptor)handle.Instance).Mutex);
            Assert.IsTrue(handle.Created);
        }

        [Test]
        public void Release_ResolvedFromServiceProvider_DisposeNotCalled()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(DisposableGrpcInterceptor)))
                .Returns(() =>
                {
                    return new DisposableGrpcInterceptor();
                });

            var interceptorActivator = new DefaultGrpcInterceptorActivator<DisposableGrpcInterceptor>();
            var interceptorHandle = interceptorActivator.Create(mockServiceProvider.Object, CreateRegistration<DisposableGrpcInterceptor>());
            var interceptorInstance = (DisposableGrpcInterceptor)interceptorHandle.Instance;

            // Act
            interceptorActivator.Release(interceptorHandle);

            // Assert
            Assert.False(interceptorInstance.Disposed);
        }

        [Test]
        public void Release_CreatedByActivator_DisposeCalled()
        {
            // Arrange
            var interceptorActivator = new DefaultGrpcInterceptorActivator<DisposableGrpcInterceptor>();
            var interceptorHandle = interceptorActivator.Create(Mock.Of<IServiceProvider>(), CreateRegistration<DisposableGrpcInterceptor>());
            var interceptorInstance = (DisposableGrpcInterceptor)interceptorHandle.Instance;

            // Act
            interceptorActivator.Release(interceptorHandle);

            // Assert
            Assert.True(interceptorInstance.Disposed);
        }

        [Test]
        public void Release_MultipleDisposableCreatedByActivator_DisposeCalled()
        {
            // Arrange
            var interceptorRegistration = CreateRegistration<DisposableGrpcInterceptor>();

            var interceptorActivator = new DefaultGrpcInterceptorActivator<DisposableGrpcInterceptor>();
            var interceptorHandle1 = interceptorActivator.Create(Mock.Of<IServiceProvider>(), interceptorRegistration);
            var interceptorHandle2 = interceptorActivator.Create(Mock.Of<IServiceProvider>(), interceptorRegistration);
            var interceptorHandle3 = interceptorActivator.Create(Mock.Of<IServiceProvider>(), interceptorRegistration);

            // Act
            interceptorActivator.Release(interceptorHandle3);
            interceptorActivator.Release(interceptorHandle2);
            interceptorActivator.Release(interceptorHandle1);

            // Assert
            Assert.True(((DisposableGrpcInterceptor)interceptorHandle1.Instance).Disposed);
            Assert.True(((DisposableGrpcInterceptor)interceptorHandle2.Instance).Disposed);
            Assert.True(((DisposableGrpcInterceptor)interceptorHandle3.Instance).Disposed);
        }

        [Test]
        public void Release_NonDisposableCreatedByActivator_DisposeNotCalled()
        {
            // Arrange
            var interceptorActivator = new DefaultGrpcInterceptorActivator<GrpcInterceptor>();
            var interceptorHandle = interceptorActivator.Create(Mock.Of<IServiceProvider>(), CreateRegistration<GrpcInterceptor>());
            var interceptorInstance = (GrpcInterceptor)interceptorHandle.Instance;

            // Act
            interceptorActivator.Release(interceptorHandle);

            // Assert
            Assert.False(interceptorInstance.Disposed);
        }

        [Test]
        public void Release_NullInterceptor_ThrowError()
        {
            // Arrange
            var activator = new DefaultGrpcInterceptorActivator<GrpcInterceptor>();

            // Act
            var ex = Assert.Throws<ArgumentException>(() => activator.Release(new GrpcActivatorHandle<Interceptor>(null!, true, state: null)));

            // Assert
            Assert.AreEqual("interceptor", ex.ParamName);
        }

        private static InterceptorRegistration CreateRegistration<TInterceptor>(params object[] args)
        {
            return new InterceptorRegistration(typeof(TInterceptor), args ?? Array.Empty<object>());
        }
    }
}
