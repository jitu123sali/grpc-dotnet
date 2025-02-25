# gRPC for .NET Examples

Examples of basic gRPC scenarios with gRPC for .NET.

* [Clients](./Clients) - each example has its own client
* [Server](./Server) - a shared website that hosts all services

If you are brand new to gRPC on .NET a good place to start is the getting started tutorial: [Create a gRPC client and server in ASP.NET Core](https://docs.microsoft.com/aspnet/core/tutorials/grpc/grpc-start)

## Greeter

The greeter shows how to create unary (non-streaming) and server streaming gRPC methods in ASP.NET Core, and call them from a client.

##### Scenarios:

* Unary call
* Server streaming call
* Client canceling a call

## Counter

The counter shows how to create unary (non-streaming) and client streaming gRPC methods in ASP.NET Core, and call them from a client.

##### Scenarios:

* Unary call
* Client streaming call

## Mailer

The mailer shows how to create a bi-directional streaming gRPC method in ASP.NET Core and call it from a client.

##### Scenarios:

* Bi-directional streaming call

## Ticketer

The ticketer shows how to use gRPC with [authentication and authorization in ASP.NET Core](https://docs.microsoft.com/aspnet/core/security). This example has a gRPC method marked with an `[Authorize]` attribute. The client can only call the method if it has been authenticated by the server and passes a valid JWT token with the gRPC call.

##### Scenarios:

* JSON web token authentication
* Send JWT token with call
* Authorization with `[Authorize]` on service

## Reflector

The reflector shows how to host the [gRPC Server Reflection Protocol](https://github.com/grpc/grpc/blob/master/doc/server-reflection.md) service and call its methods from a client.

##### Scenarios:

* Hosting gRPC Server Reflection Protocol service
* Calling service with `Grpc.Reflection` client

## Certifier

The certifier shows how to configure the client and the server to use a [TLS client certificate](https://blogs.msdn.microsoft.com/kaushal/2015/05/27/client-certificate-authentication-part-1/) with a gRPC call. The server is configured to require a client certificate using [ASP.NET Core client certificate authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/certauth).

##### Scenarios:

* Client certificate authentication
* Send client certificate with call
* Receive client certificate in a service
* Authorization with `[Authorize]` on service

## Worker

The worker shows how a [.NET worker service](https://devblogs.microsoft.com/aspnet/net-core-workers-as-windows-services/) can use the gRPC client factory to make gRPC calls.

##### Scenarios:

* Worker service
* Client factory

## Aggregator

The aggregator shows how a to make nested gRPC calls (a gRPC service calling another gRPC service). The gRPC client factory is used in ASP.NET Core to inject a client into services. The gRPC client factory is configured to propagate the context from the original call to the nested call. In this example the cancellation from the client will automatically propagate through to nested gRPC calls.

##### Scenarios:

* Client factory
* Client canceling a call
* Cancellation propagation