# OpenTelemetry Sample

This sample demonstrates how to use **OpenTelemetry** with **Excalibur.Dispatch** for distributed tracing, metrics, and observability.

## Features

- **Distributed Tracing** - Track requests across services
- **Dispatch Instrumentation** - Trace message processing
- **Custom Spans** - Add business context to traces
- **Jaeger Integration** - Visualize traces
- **Metrics Collection** - Core and transport metrics

## Prerequisites

### Start Jaeger

```bash
# Using docker-compose
docker-compose up -d

# Or directly with Docker
docker run -d --name jaeger \
  -p 6831:6831/udp \
  -p 16686:16686 \
  -p 4317:4317 \
  -p 4318:4318 \
  jaegertracing/all-in-one:1.52
```

## Running the Sample

```bash
# Build
dotnet build

# Run
dotnet run
```

Access the endpoints:
- **API**: http://localhost:5000
- **Jaeger UI**: http://localhost:16686

## Testing

### Create an Order

```bash
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-001", "customerId": "CUST-100", "amount": 99.99}'
```

### View Traces

1. Open Jaeger UI: http://localhost:16686
2. Select service: `dispatch-otel-sample`
3. Click "Find Traces"
4. Click a trace to see the span hierarchy

## Trace Structure

```
ProcessOrder (custom span)
├── dispatch.message.process (Dispatch span)
│   └── HandleOrderProcessed (handler span)
│       ├── ValidateOrder
│       ├── PersistOrder
│       └── SendNotification
```

## Code Examples

### Adding Custom Spans

```csharp
using var activity = DispatchActivitySource.Source.StartActivity("MyOperation");
activity?.SetTag("custom.tag", "value");
activity?.AddEvent(new ActivityEvent("ImportantEvent"));

try
{
    // Do work
    activity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.RecordException(ex);
    throw;
}
```

### Configuring OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("my-service", "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(DispatchActivitySource.Name)  // Add Dispatch spans
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddDispatchMetrics()     // Add Dispatch core metrics
        .AddTransportMetrics()    // Add transport metrics
        .AddOtlpExporter());
```

### Correlation Context

```csharp
// Add trace context to Dispatch context
var context = DispatchContextInitializer.CreateDefaultContext();

if (Activity.Current != null)
{
    context.Properties["TraceId"] = Activity.Current.TraceId.ToString();
    context.Properties["SpanId"] = Activity.Current.SpanId.ToString();
}

await dispatcher.DispatchAsync(myEvent, context);
```

## Metrics

### Dispatch Core Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `dispatch.messages.processed` | Counter | Total messages processed |
| `dispatch.messages.duration` | Histogram | Processing duration |
| `dispatch.messages.published` | Counter | Total messages published |
| `dispatch.messages.failed` | Counter | Failed message count |
| `dispatch.sessions.active` | Gauge | Active sessions |

### Transport Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `dispatch.transport.messages_sent_total` | Counter | Messages sent |
| `dispatch.transport.messages_received_total` | Counter | Messages received |
| `dispatch.transport.errors_total` | Counter | Transport errors |
| `dispatch.transport.send_duration_ms` | Histogram | Send latency |

## Configuration

### OTLP Endpoint

```json
{
  "Otel": {
    "Endpoint": "http://localhost:4317"
  }
}
```

Or via environment:

```bash
export Otel__Endpoint="http://otel-collector:4317"
```

### Common Exporters

| Exporter | Package | Use Case |
|----------|---------|----------|
| Console | OpenTelemetry.Exporter.Console | Development/debugging |
| OTLP | OpenTelemetry.Exporter.OpenTelemetryProtocol | Production (Jaeger, etc.) |
| Zipkin | OpenTelemetry.Exporter.Zipkin | Zipkin backend |
| Application Insights | Azure.Monitor.OpenTelemetry.Exporter | Azure |

## Best Practices

### DO

- Use meaningful span names (verb + noun)
- Add relevant tags for filtering/searching
- Record exceptions on error spans
- Use events for significant milestones
- Filter out health check endpoints

### DON'T

- Create spans for every method call
- Add sensitive data as tags
- Forget to set span status
- Ignore parent-child relationships

## Troubleshooting

### No Traces in Jaeger

1. Verify Jaeger is running:
   ```bash
   curl http://localhost:14269/
   ```

2. Check OTLP endpoint in config

3. Look for export errors in console output

### Missing Dispatch Spans

1. Verify `AddSource(DispatchActivitySource.Name)` is configured
2. Check that handlers are properly registered

## Related Samples

- [Health Checks Sample](../HealthChecks/) - Kubernetes readiness/liveness
- [Audit Logging Sample](../../06-security/AuditLogging/) - Compliance logging

## Learn More

- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Jaeger](https://www.jaegertracing.io/)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)
