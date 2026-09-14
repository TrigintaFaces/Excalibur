# Excalibur.Dispatch.Transport.Grpc

gRPC transport implementation for the Excalibur Excalibur framework.

## Features

- `ITransportSender` via gRPC unary calls
- `ITransportReceiver` via gRPC unary request/response
- `ITransportSubscriber` via gRPC server streaming
- Configurable channel options, deadlines, and metadata
- GetService() exposes underlying `GrpcChannel` for direct SDK access

## CloudEvents

**Inbound CloudEvents are decoded automatically.** Every receiver on this transport is wrapped by the
framework's decoding decorator, so a message carrying CloudEvents markers arrives with the decoded event
attached to it — the body and properties are untouched, and a message that is not a CloudEvent passes
through unchanged. Every message in the batch is delivered, including a malformed one: it arrives carrying a
decode error instead of a decoded event, never dropped. There is nothing to register and no option to set.

**Outbound CloudEvents are opt-in per message.** Attach the event to the message you were going to send
anyway:

```csharp
await sender.SendAsync(message.WithCloudEvent(cloudEvent), cancellationToken);
```

The event is published in CloudEvents **structured mode** — the whole event becomes a JSON document in the
body under the `application/cloudevents+json` content type. A message you do not call `WithCloudEvent` on
reaches the wire byte-identical, so ordinary traffic is unaffected, and nothing produces a `CloudEvent` for
you. This path needs no trimming or ahead-of-time annotation: the envelope is written with a UTF-8 JSON
writer rather than a reflection-based serializer.

## Usage

```csharp
services.AddGrpcTransport(options =>
{
    options.ServerAddress = "https://localhost:5001";
    options.DeadlineSeconds = 30;
});
```
