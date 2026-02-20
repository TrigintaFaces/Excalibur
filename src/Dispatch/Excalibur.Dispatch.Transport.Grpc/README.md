# Excalibur.Dispatch.Transport.Grpc

gRPC transport implementation for the Excalibur Dispatch messaging framework.

## Features

- `ITransportSender` via gRPC unary calls
- `ITransportReceiver` via gRPC unary request/response
- `ITransportSubscriber` via gRPC server streaming
- Configurable channel options, deadlines, and metadata
- GetService() exposes underlying `GrpcChannel` for direct SDK access

## Usage

```csharp
services.AddGrpcTransport(options =>
{
    options.ServerAddress = "https://localhost:5001";
    options.DeadlineSeconds = 30;
});
```
