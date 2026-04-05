# CloudEvents Sample

This sample demonstrates how **Excalibur.Dispatch** integrates with the [CNCF CloudEvents](https://cloudevents.io/) specification (v1.0).

## What Are CloudEvents?

[CloudEvents](https://github.com/cloudevents/spec) is a CNCF specification for describing event data in a common way. It provides a standard envelope format with attributes like `id`, `source`, `type`, `time`, and `specversion` so that events produced by different systems can be consumed uniformly.

## How Excalibur.Dispatch Supports CloudEvents

Excalibur.Dispatch provides CloudEvents support at two levels:

### 1. Pipeline Middleware (`UseCloudEvents()`)

Add `UseCloudEvents()` to the Dispatch pipeline to automatically enrich outgoing `IDispatchEvent` messages with CloudEvents metadata:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseCloudEvents();      // adds CloudEvent middleware
    dispatch.UseValidation();
});
```

Configure options via `IOptions<CloudEventOptions>`:

```csharp
services.Configure<CloudEventOptions>(options =>
{
    options.Mode = CloudEventMode.Structured;           // or CloudEventMode.Binary
    options.DefaultSource = new Uri("urn:my-service");
    options.PreserveEnvelopeProperties = true;
});
```

### 2. Transport-Level Decorators

Each transport package provides CloudEvents serialization for wire-level interoperability:

| Transport | Registration |
|-----------|-------------|
| RabbitMQ | `services.AddRabbitMqCloudEvents()` |
| Apache Kafka | `services.AddKafkaCloudEvents()` |
| Azure Service Bus | `services.AddAzureCloudEvents()` |
| AWS SQS | `services.AddAwsSqsCloudEvents()` |
| Google Pub/Sub | `services.AddGooglePubSubCloudEvents()` |

Transport packages register `ICloudEventMapper<T>`, `ICloudEventEnvelopeConverter`, and `IEnvelopeCloudEventBridge` automatically.

## Running the Sample

```bash
dotnet run --project samples/03-cloud-native/CloudEvents
```

The sample dispatches an `OrderPlacedEvent` through a pipeline configured with `UseCloudEvents()` and prints the resulting CloudEvent attributes.

## Key Types

| Type | Purpose |
|------|---------|
| `CloudEventOptions` | Configuration for mode, source URI, envelope preservation |
| `CloudEventMode` | `Structured` (JSON envelope) or `Binary` (header-mapped) |
| `CloudEventMiddleware` | Pipeline middleware that enriches events with CE metadata |
| `ICloudEventEnvelopeConverter` | Converts between `MessageEnvelope` and `CloudEvent` |
| `IEnvelopeCloudEventBridge` | Bridges envelopes to transport messages via CloudEvents |
| `CloudEventExtensions.ToCloudEvent()` | Manual conversion of `IDispatchEvent` to `CloudEvent` |
