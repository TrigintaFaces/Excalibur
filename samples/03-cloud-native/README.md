# Cloud-Native Samples

Cloud-native patterns and integrations for modern distributed systems.

## Samples

| Sample | Description | Patterns |
|--------|-------------|----------|
| [CloudNativePatterns.Examples](CloudNativePatterns.Examples/) | Claim check pattern and cloud-native messaging | Claim Check |

## Key Patterns

### Claim Check Pattern

The claim check pattern reduces message size by storing large payloads externally and passing only a reference:

```csharp
// Instead of sending large payload directly
public record LargeOrderMessage(List<OrderItem> Items);  // Could be MBs

// Use claim check
public record OrderClaimCheck(string ClaimId, Uri StorageLocation);  // Just a reference
```

**When to use:**
- Message payloads exceed broker limits (RabbitMQ: ~128KB, Kafka: configurable)
- Large binary attachments (images, documents)
- Reducing network bandwidth
- Audit trail requirements for payload storage

### Configuration Examples

The CloudNativePatterns.Examples sample includes:

- Multi-tenant configuration
- Custom naming strategies
- Logging and metrics middleware
- Migration guides from inline payloads

## Running the Samples

```bash
# CloudNativePatterns.Examples
dotnet run --project samples/03-cloud-native/CloudNativePatterns.Examples/ClaimCheck
```

## Future Additions (Planned)

| Sample | Status | Description |
|--------|--------|-------------|
| HealthChecks | Planned | Kubernetes-ready health check patterns |
| ConfigMaps | Planned | Configuration from ConfigMaps/Secrets |
| OpenTelemetry | Planned | Distributed tracing setup |

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Observability` | OpenTelemetry integration |
| `Excalibur.Dispatch.Hosting.AspNetCore` | ASP.NET Core integration |

## What's Next?

- [05-serverless/](../05-serverless/) - Serverless hosting samples
- [07-observability/](../07-observability/) - Observability samples

---

*Category: Cloud-Native | Sprint 428*
