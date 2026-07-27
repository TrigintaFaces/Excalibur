# Excalibur.Dispatch.Transport.IbmMq

IBM MQ transport for Excalibur. Queue-based enterprise messaging with native request/reply over the IBM MQ
managed .NET client (`IBMMQDotnetClient`).

## What this package provides (W2 scaffold)

- `IbmMqOptions` — queue manager, host/port, server-connection channel, queue and reply-to-queue names,
  and receive tuning, validated at startup (`ValidateOnStart`).
- `IIbmMqConnectionProvider` — connects managed-client queue managers from the configured options.
- `AddIbmMqTransport(...)` — registers the connection provider and validated options.

The dispatch sender/receiver that carry messages over IBM MQ build on `IIbmMqConnectionProvider`.

## Usage

```csharp
services.AddIbmMqTransport("ibmmq", o =>
{
    o.QueueManager = "QM1";
    o.Host = "localhost";
    o.Port = 1414;
    o.Channel = "DEV.APP.SVRCONN";
    o.QueueName = "DEV.QUEUE.1";
    // o.ReplyToQueue = "DEV.REPLY.1"; // native request/reply
});
```

Credentials (`UserId`/`Password`) must come from configuration or a secret manager — never commit values.
