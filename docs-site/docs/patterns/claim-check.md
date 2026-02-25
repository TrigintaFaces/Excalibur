---
sidebar_position: 5
title: Claim Check Pattern
description: Handle large message payloads by storing them externally and passing references
---

# Claim Check Pattern

The Claim Check pattern handles large message payloads that exceed transport size limits. Instead of passing the full payload, the message contains a reference (claim check) to externally stored data.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Patterns
  ```
- Familiarity with [transports](../transports/index.md) and a blob/object storage service for payload storage

## When to Use

- Message payloads exceed broker size limits (e.g., Kafka 1MB, Azure Service Bus 256KB)
- Large attachments need to be passed between services
- Bandwidth costs are a concern for large payloads
- You want to reduce memory pressure on message brokers
- Processing large binary data (images, documents, reports)

## How It Works

```
Producer                    Claim Check Store               Consumer
   |                              |                            |
   | --- Store payload -------->  |                            |
   | <-- Return reference ------  |                            |
   |                              |                            |
   | ---------- Send message with reference ----------------->|
   |                              |                            |
   |                              | <--- Retrieve payload ---- |
   |                              | ------- Return data -----> |
```

## Installation

```bash
# Core patterns (base interfaces and options)
dotnet add package Excalibur.Dispatch.Patterns

# In-memory provider (testing/development)
dotnet add package Excalibur.Dispatch.Patterns.ClaimCheck.InMemory

# Azure Blob provider (production)
dotnet add package Excalibur.Dispatch.Patterns.Azure
```

## Basic Configuration

### In-Memory (Testing/Development)

```csharp
builder.Services.AddInMemoryClaimCheck();
```

### Azure Blob Storage (Production)

```csharp
builder.Services.AddAzureBlobClaimCheck(options =>
{
    options.ConnectionString = blobConnectionString;
    options.ContainerName = "large-messages";
    options.PayloadThreshold = 256_000; // 256 KB
});
```

## IClaimCheckProvider Interface

```csharp
public interface IClaimCheckProvider
{
    /// <summary>
    /// Stores a payload and returns a claim check reference.
    /// </summary>
    Task<ClaimCheckReference> StoreAsync(
        byte[] payload,
        CancellationToken cancellationToken,
        ClaimCheckMetadata? metadata = null);

    /// <summary>
    /// Retrieves a payload using a claim check reference.
    /// </summary>
    Task<byte[]> RetrieveAsync(
        ClaimCheckReference reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a stored payload using its claim check reference.
    /// </summary>
    Task<bool> DeleteAsync(
        ClaimCheckReference reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a payload should use the claim check pattern based on size.
    /// </summary>
    bool ShouldUseClaimCheck(byte[] payload);
}
```

## Usage Examples

### Producer Side

```csharp
public class ReportGeneratorService
{
    private readonly IClaimCheckProvider _claimCheck;
    private readonly IDispatcher _dispatcher;

    public ReportGeneratorService(
        IClaimCheckProvider claimCheck,
        IDispatcher dispatcher)
    {
        _claimCheck = claimCheck;
        _dispatcher = dispatcher;
    }

    public async Task GenerateAndSendReportAsync(
        string reportId,
        byte[] reportData,
        CancellationToken ct)
    {
        ClaimCheckReference? reference = null;

        // Store large payloads externally
        if (_claimCheck.ShouldUseClaimCheck(reportData))
        {
            reference = await _claimCheck.StoreAsync(
                reportData,
                new ClaimCheckMetadata
                {
                    ContentType = "application/pdf",
                    MessageId = reportId,
                    MessageType = "Report"
                },
                ct);
        }

        // Dispatch event with reference (not full payload)
        var action = new ProcessReportAction(
            reportId,
            reference,
            reference is null ? reportData : null);

        await _dispatcher.DispatchAsync(action, ct);
    }
}
```

### Consumer Side

```csharp
public class ProcessReportHandler : IActionHandler<ProcessReportAction>
{
    private readonly IClaimCheckProvider _claimCheck;

    public ProcessReportHandler(IClaimCheckProvider claimCheck)
    {
        _claimCheck = claimCheck;
    }

    public async Task HandleAsync(
        ProcessReportAction action,
        CancellationToken ct)
    {
        // Retrieve payload if stored externally
        byte[] reportData;
        if (action.ClaimCheckReference is not null)
        {
            reportData = await _claimCheck.RetrieveAsync(
                action.ClaimCheckReference,
                ct);

            // Optionally delete after processing
            await _claimCheck.DeleteAsync(action.ClaimCheckReference, ct);
        }
        else
        {
            reportData = action.InlineData!;
        }

        // Process the report data
        await ProcessReportDataAsync(reportData, ct);
    }
}
```

### Action with Claim Check Support

```csharp
public record ProcessReportAction(
    string ReportId,
    ClaimCheckReference? ClaimCheckReference,
    byte[]? InlineData) : IDispatchAction
{
    public bool UsesClaimCheck => ClaimCheckReference is not null;
}
```

## Configuration Options

```csharp
builder.Services.AddInMemoryClaimCheck(options =>
{
    // Size threshold for using claim check (default: 256KB)
    options.PayloadThreshold = 256 * 1024;

    // Compression settings
    options.EnableCompression = true;
    options.CompressionThreshold = 1024; // Min size for compression (1KB)
    options.CompressionLevel = CompressionLevel.Optimal;
    options.MinCompressionRatio = 0.8; // Only keep compressed if 20%+ smaller

    // Retention and cleanup
    options.DefaultTtl = TimeSpan.FromDays(7);
    options.RetentionPeriod = TimeSpan.FromDays(7);
    options.EnableCleanup = true;
    options.CleanupInterval = TimeSpan.FromHours(1);
    options.CleanupBatchSize = 1000;

    // Integrity
    options.ValidateChecksum = true;
    options.EnableChecksumValidation = true;

    // Performance
    options.MaxConcurrency = Environment.ProcessorCount;
    options.ChunkSize = 1024 * 1024; // 1MB chunks
    options.BufferPoolSize = 100;

    // Resilience
    options.MaxRetries = 3;
    options.RetryCount = 3;
    options.RetryDelay = TimeSpan.FromSeconds(1);
    options.OperationTimeout = TimeSpan.FromSeconds(30);

    // Storage organization
    options.ContainerName = "claim-checks";
    options.IdPrefix = "cc-";
    options.BlobNamePrefix = "claims";

    // Advanced
    options.UseHierarchicalStorage = false; // Hot/cold tiers
    options.ColdStorageThreshold = TimeSpan.FromDays(30);
    options.EnableEncryption = false;
    options.EnableMetrics = true;
    options.EnableDetailedMetrics = false;
});
```

## Supported Providers

| Provider | Package | Use Case |
|----------|---------|----------|
| In-Memory | `Excalibur.Dispatch.Patterns.ClaimCheck.InMemory` | Testing, development, single-node |
| Azure Blob | `Excalibur.Dispatch.Patterns.Azure` | Azure cloud production |

## Custom Provider Implementation

```csharp
public class CustomClaimCheckProvider : IClaimCheckProvider
{
    private readonly ClaimCheckOptions _options;
    private readonly IMyStorage _storage;

    public CustomClaimCheckProvider(
        IOptions<ClaimCheckOptions> options,
        IMyStorage storage)
    {
        _options = options.Value;
        _storage = storage;
    }

    public bool ShouldUseClaimCheck(byte[] payload) =>
        payload.Length > _options.PayloadThreshold;

    public async Task<ClaimCheckReference> StoreAsync(
        byte[] payload,
        ClaimCheckMetadata? metadata,
        CancellationToken ct)
    {
        var id = $"{_options.IdPrefix}{Guid.NewGuid()}";

        await _storage.UploadAsync(id, payload, ct);

        return new ClaimCheckReference
        {
            Id = id,
            Size = payload.Length,
            StoredAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(_options.DefaultTtl),
            Metadata = metadata
        };
    }

    public async Task<byte[]> RetrieveAsync(
        ClaimCheckReference reference,
        CancellationToken ct)
    {
        var payload = await _storage.DownloadAsync(reference.Id, ct);

        // Validate size if needed
        if (_options.ValidateChecksum && payload.Length != reference.Size)
        {
            throw new InvalidOperationException($"Payload size mismatch for claim check {reference.Id}");
        }

        return payload;
    }

    public async Task<bool> DeleteAsync(
        ClaimCheckReference reference,
        CancellationToken ct) =>
        await _storage.DeleteAsync(reference.Id, ct);
}
```

## Best Practices

### Size Thresholds by Transport

| Transport | Typical Limit | Recommended Threshold |
|-----------|---------------|----------------------|
| Kafka | 1MB default | 256KB |
| RabbitMQ | 128MB | 1MB |
| Azure Service Bus | 256KB standard, 100MB premium | 200KB |
| AWS SQS | 256KB | 200KB |

### Cleanup Strategy

```csharp
// Automatic cleanup via background service
options.EnableCleanup = true;
options.CleanupInterval = TimeSpan.FromHours(1);

// Manual cleanup after successful processing
await _claimCheck.DeleteAsync(reference, ct);

// Or keep for audit purposes
options.RetentionPeriod = TimeSpan.FromDays(30);
```

### Error Handling

```csharp
try
{
    var data = await _claimCheck.RetrieveAsync(reference, ct);
}
catch (KeyNotFoundException)
{
    // Payload expired or already deleted
    _logger.LogWarning("Claim check {Id} not found", reference.Id);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("size mismatch"))
{
    // Size validation failed - data may be corrupted
    _logger.LogError(ex, "Data integrity failure for {Id}", reference.Id);
}
```

## Registration

Register the claim check pattern using provider-specific extension methods:

```csharp
// In-memory provider (for testing)
builder.Services.AddInMemoryClaimCheck();

// Azure Blob Storage provider (for production)
builder.Services.AddAzureBlobClaimCheck(options =>
{
    options.ConnectionString = blobConnectionString;
    options.ContainerName = "claim-checks";
});
```

Configure claim check options inline:

```csharp
builder.Services.AddInMemoryClaimCheck(options =>
{
    options.PayloadThreshold = 256_000; // Auto-offload payloads above 256KB
    options.EnableCompression = true;
    options.DefaultTtl = TimeSpan.FromDays(7);
});
```

For custom providers, use the generic `AddClaimCheck<TProvider>()`:

```csharp
builder.Services.AddClaimCheck<MyCustomClaimCheckProvider>(options =>
{
    options.PayloadThreshold = 256_000;
});
```

## Related Patterns

- [Outbox Pattern](outbox.md) - Reliable message publishing
- [Inbox Pattern](inbox.md) - Idempotent message processing
- [Dead Letter](dead-letter.md) - Handle failed messages

## See Also

- [Outbox Pattern](outbox.md) -- Combine claim check with transactional outbox for reliable large-payload publishing
- [Message Mapping](../transports/message-mapping.md) -- Configure how messages are serialized and mapped across transports
- [Streaming](streaming.md) -- Stream large datasets instead of batching into single messages
- [Transports Overview](../transports/index.md) -- Transport size limits and configuration that drive claim check thresholds
