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
    options.DispatchExtensionPrefix = "dispatch";        // prefix for Dispatch extension attributes
});
```

### 2. Transport-Level Decorators

Each transport package provides CloudEvents serialization for wire-level interoperability:

| Transport | Registration |
|-----------|-------------|
| RabbitMQ | `services.AddCloudEventsForRabbitMq()` |
| Apache Kafka | `services.AddCloudEventsForKafka()` |
| Azure Service Bus | `services.AddCloudEventsForServiceBus()` |
| Azure Event Hubs | `services.AddCloudEventsForEventHubs()` |
| AWS SQS | `services.AddCloudEventsForSqs()` |
| AWS SNS | `services.AddCloudEventsForSns()` |
| AWS EventBridge | `services.AddCloudEventsForEventBridge()` |
| Google Pub/Sub | `services.AddCloudEventsForPubSub()` |
| MQTT | `services.AddCloudEventsForMqtt()` |

Transport packages register `ICloudEventEncoder<T>`, `ICloudEventEnvelopeConverter`, and `IEnvelopeCloudEventBridge` automatically.

When using the EventBridge message bus, publication requires a positive per-entry
`PutEvents` acknowledgement; HTTP 200 can still contain rejected entries. Established
transient entry failures propagate to the configured retry owner, while permanent or
unsupported acknowledgements do not silently complete an outbox message. This sample
prints pipeline attributes locally and does not exercise EventBridge ingestion or
target rules.

CloudEvents use their configured JSON representation. The native EventBridge bus
instead wraps pluggable serializer bytes as `{"payload":"BASE64"}`; native target
consumers decode that field before deserialization. An accepted event ID does not
guarantee downstream delivery or deduplication. See the [EventBridge acknowledgement
and payload guidance](https://docs.excalibur-dispatch.dev/docs/transports/aws-sqs#eventbridge-ingestion-acknowledgements).

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
