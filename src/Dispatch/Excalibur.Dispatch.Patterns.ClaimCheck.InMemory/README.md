# Excalibur.Dispatch.Patterns.ClaimCheck.InMemory

**In-memory Claim Check pattern provider for Dispatch messaging framework.**

Thread-safe, zero-dependency implementation with TTL expiration, compression, and checksum validation. Ideal for testing and local development scenarios.

---

## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration Reference](#configuration-reference)
- [Usage Examples](#usage-examples)
- [Testing Scenarios](#testing-scenarios)
- [Performance Considerations](#performance-considerations)
- [Troubleshooting](#troubleshooting)
- [Comparison with Cloud Providers](#comparison-with-cloud-providers)
- [Contributing](#contributing)

---

## Overview

The **Claim Check pattern** solves the problem of large message payloads in messaging systems by storing the payload externally and including only a reference (claim check) in the message. This pattern is essential when:

- Message brokers have size limits (e.g., Azure Service Bus 256KB, RabbitMQ 128MB)
- Large payloads would degrade messaging performance
- You need to decouple message metadata from payload data

The **InMemory provider** implements this pattern using a thread-safe in-memory store, perfect for:

✅ **Unit and integration testing** without external dependencies
✅ **Local development** and debugging
✅ **Single-node scenarios** where durability across restarts is not required
✅ **Prototyping** and proof-of-concept implementations

⚠️ **Not recommended for production** distributed systems due to memory constraints and lack of durability.

### Key Features

- ✅ **Zero External Dependencies** - No databases, no cloud services, no network calls
- ✅ **Thread-Safe** - ConcurrentDictionary-based storage for safe concurrent access
- ✅ **TTL Expiration** - Automatic cleanup of expired payloads via background service
- ✅ **Compression** - Optional GZip compression with configurable threshold and ratio validation
- ✅ **Checksum Validation** - SHA256 integrity checking to detect corruption
- ✅ **Native AOT Compatible** - PublishAot=true for ahead-of-time compilation
- ✅ **Lazy Deletion** - Expired entries removed on access or via background cleanup

---

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package Excalibur.Dispatch.Patterns.ClaimCheck.InMemory
```

Or via Package Manager Console:

```powershell
Install-Package Excalibur.Dispatch.Patterns.ClaimCheck.InMemory
```

**Requirements:**
- .NET 8.0 or later
- Excalibur.Dispatch.Patterns (automatically included as dependency)

---

## Quick Start

### 1. Register the Provider

Add the InMemory claim check provider to your dependency injection container:

```csharp
using Excalibur.Dispatch.Patterns.ClaimCheck.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Register InMemory claim check provider with default settings
builder.Services.AddInMemoryClaimCheck();

var app = builder.Build();
```

### 2. Use the Provider

Inject `IClaimCheckProvider` into your services and use it to store/retrieve large payloads:

```csharp
public class OrderService
{
    private readonly IClaimCheckProvider _claimCheckProvider;

    public OrderService(IClaimCheckProvider claimCheckProvider)
    {
        _claimCheckProvider = claimCheckProvider;
    }

    public async Task ProcessLargeOrderAsync(byte[] orderPayload)
    {
        // Check if payload should use claim check pattern
        if (_claimCheckProvider.ShouldUseClaimCheck(orderPayload))
        {
            // Store payload and get reference
            var reference = await _claimCheckProvider.StoreAsync(
                orderPayload,
                metadata: new ClaimCheckMetadata
                {
                    MessageType = "OrderCreated",
                    ContentType = "application/json"
                });

            // Send only the reference in the message
            await SendMessageAsync(reference);
        }
        else
        {
            // Payload is small enough, send inline
            await SendMessageAsync(orderPayload);
        }
    }

    public async Task<byte[]> RetrieveLargeOrderAsync(ClaimCheckReference reference)
    {
        // Retrieve the original payload
        return await _claimCheckProvider.RetrieveAsync(reference);
    }

    public async Task DeleteClaimCheckAsync(ClaimCheckReference reference)
    {
        // Delete the stored payload (optional - will auto-expire based on TTL)
        await _claimCheckProvider.DeleteAsync(reference);
    }
}
```

---

## Configuration Reference

### Via Code (Action Delegate)

```csharp
builder.Services.AddInMemoryClaimCheck(options =>
{
    // Payload threshold - payloads >= this size use claim check pattern
    options.PayloadThreshold = 128 * 1024; // 128KB (default: 256KB)

    // TTL - how long payloads are stored before expiration
    options.DefaultTtl = TimeSpan.FromDays(3); // Default: 7 days

    // Compression settings
    options.EnableCompression = true; // Default: true
    options.CompressionThreshold = 2048; // Compress payloads >= 2KB (default: 1KB)
    options.CompressionLevel = CompressionLevel.SmallestSize; // Default: Optimal
    options.MinCompressionRatio = 0.7; // Only keep compressed if <70% of original (default: 0.8)

    // Checksum validation
    options.ValidateChecksum = true; // Default: true (SHA256)

    // Background cleanup
    options.EnableCleanup = true; // Default: true
    options.CleanupInterval = TimeSpan.FromMinutes(30); // Default: 1 hour

    // ID generation
    options.IdPrefix = "claim-"; // Default: "cc-"
    options.ContainerName = "my-claims"; // Default: "claim-checks"
}, enableCleanup: true); // Enable background cleanup service
```

### Via appsettings.json

```json
{
  "ClaimCheck": {
    "PayloadThreshold": 131072,
    "DefaultTtl": "3.00:00:00",
    "EnableCompression": true,
    "CompressionThreshold": 2048,
    "CompressionLevel": "SmallestSize",
    "MinCompressionRatio": 0.7,
    "ValidateChecksum": true,
    "EnableCleanup": true,
    "CleanupInterval": "00:30:00",
    "IdPrefix": "claim-",
    "ContainerName": "my-claims"
  }
}
```

Then register with configuration binding:

```csharp
builder.Services.AddInMemoryClaimCheck(
    builder.Configuration.GetSection("ClaimCheck"));
```

### Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PayloadThreshold` | `long` | 262144 (256KB) | Minimum payload size to trigger claim check pattern |
| `DefaultTtl` | `TimeSpan` | 7 days | How long payloads are retained before expiration |
| `EnableCompression` | `bool` | `true` | Whether to compress payloads |
| `CompressionThreshold` | `long` | 1024 (1KB) | Minimum size for compression (smaller payloads not compressed) |
| `CompressionLevel` | `CompressionLevel` | `Optimal` | GZip compression level (`NoCompression`, `Fastest`, `Optimal`, `SmallestSize`) |
| `MinCompressionRatio` | `double` | 0.8 | Only keep compressed data if compressed size ≤ this ratio of original size |
| `ValidateChecksum` | `bool` | `true` | Compute and validate SHA256 checksums for integrity |
| `EnableCleanup` | `bool` | `true` | Enable background cleanup of expired entries |
| `CleanupInterval` | `TimeSpan` | 1 hour | How often the cleanup service scans for expired entries |
| `IdPrefix` | `string` | `"cc-"` | Prefix for generated claim check IDs |
| `ContainerName` | `string` | `"claim-checks"` | Logical container name (used in reference Location) |
| `BlobNamePrefix` | `string` | `"claims"` | Prefix for blob names in references |

---

## Usage Examples

### Example 1: Basic Store and Retrieve

```csharp
var payload = Encoding.UTF8.GetBytes("Large payload content...");

// Store
var reference = await claimCheckProvider.StoreAsync(payload);
Console.WriteLine($"Stored as: {reference.Id}");
Console.WriteLine($"Location: {reference.Location}");
Console.WriteLine($"Expires: {reference.ExpiresAt}");

// Retrieve
var retrievedPayload = await claimCheckProvider.RetrieveAsync(reference);
var content = Encoding.UTF8.GetString(retrievedPayload);

// Delete (optional)
var deleted = await claimCheckProvider.DeleteAsync(reference);
```

### Example 2: With Metadata

```csharp
var metadata = new ClaimCheckMetadata
{
    MessageId = "order-12345",
    MessageType = "OrderCreated",
    ContentType = "application/json",
    CorrelationId = "correlation-abc",
    Properties = new Dictionary<string, string>
    {
        ["OrderNumber"] = "ORD-2025-001",
        ["CustomerId"] = "CUST-789"
    },
    Tags = new Dictionary<string, string>
    {
        ["Environment"] = "Production",
        ["Region"] = "US-East"
    }
};

var reference = await claimCheckProvider.StoreAsync(payload, metadata);

// Metadata is preserved in the reference
Console.WriteLine($"Message Type: {reference.Metadata?.MessageType}");
Console.WriteLine($"Correlation ID: {reference.Metadata?.CorrelationId}");
```

### Example 3: Compression Example

```csharp
// Large repetitive payload (compresses well)
var largePayload = new byte[100_000];
Array.Fill(largePayload, (byte)'A');

var reference = await claimCheckProvider.StoreAsync(largePayload);

Console.WriteLine($"Original Size: {reference.Size} bytes");
Console.WriteLine($"Compressed: {reference.Metadata?.IsCompressed}");
Console.WriteLine($"Compressed Size: {reference.Metadata?.OriginalSize} bytes");

// Retrieve - automatically decompressed
var retrieved = await claimCheckProvider.RetrieveAsync(reference);
Console.WriteLine($"Retrieved Size: {retrieved.Length} bytes");
```

### Example 4: TTL and Expiration

```csharp
// Store with default TTL (7 days)
var reference = await claimCheckProvider.StoreAsync(payload);
Console.WriteLine($"Expires At: {reference.ExpiresAt}");

// Wait for expiration (simulated with shorter TTL in options)
await Task.Delay(TimeSpan.FromSeconds(10));

try
{
    // This will throw InvalidOperationException if expired
    var retrieved = await claimCheckProvider.RetrieveAsync(reference);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Expired: {ex.Message}");
}
```

### Example 5: Threshold Check

```csharp
var smallPayload = new byte[1024]; // 1KB
var largePayload = new byte[300_000]; // 300KB

var shouldUseSmall = claimCheckProvider.ShouldUseClaimCheck(smallPayload);
var shouldUseLarge = claimCheckProvider.ShouldUseClaimCheck(largePayload);

Console.WriteLine($"1KB payload uses claim check: {shouldUseSmall}"); // false (< 256KB)
Console.WriteLine($"300KB payload uses claim check: {shouldUseLarge}"); // true (> 256KB)
```

---

## Testing Scenarios

The InMemory provider is specifically designed for testing scenarios where you want to avoid external dependencies.

### Unit Test Setup

```csharp
using Excalibur.Dispatch.Patterns.ClaimCheck;
using Excalibur.Dispatch.Patterns.ClaimCheck.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class OrderServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IClaimCheckProvider _claimCheckProvider;

    public OrderServiceTests()
    {
        var services = new ServiceCollection();

        // Register InMemory provider WITHOUT background cleanup (for deterministic tests)
        services.AddInMemoryClaimCheck(options =>
        {
            options.PayloadThreshold = 1024; // Lower threshold for testing
            options.DefaultTtl = TimeSpan.FromMinutes(1);
            options.EnableCompression = true;
        }, enableCleanup: false); // Disable background service in tests

        _serviceProvider = services.BuildServiceProvider();
        _claimCheckProvider = _serviceProvider.GetRequiredService<IClaimCheckProvider>();
    }

    [Fact]
    public async Task StoreAndRetrieve_ShouldSucceed()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("Test payload");

        // Act
        var reference = await _claimCheckProvider.StoreAsync(payload);
        var retrieved = await _claimCheckProvider.RetrieveAsync(reference);

        // Assert
        Assert.Equal(payload, retrieved);
    }

    [Fact]
    public async Task ExpiredPayload_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInMemoryClaimCheck(options =>
        {
            options.DefaultTtl = TimeSpan.FromMilliseconds(100); // Short TTL
        }, enableCleanup: false);

        var provider = services.BuildServiceProvider()
            .GetRequiredService<IClaimCheckProvider>();

        var payload = new byte[100];
        var reference = await provider.StoreAsync(payload);

        // Act - wait for expiration
        await Task.Delay(200);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.RetrieveAsync(reference));
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
```

### Integration Test with Messaging

```csharp
[Fact]
public async Task MessageWithClaimCheck_ShouldPreservePayload()
{
    // Arrange
    var largePayload = new byte[300_000];
    new Random().NextBytes(largePayload);

    // Act - store claim check
    var reference = await _claimCheckProvider.StoreAsync(largePayload);

    // Simulate sending message with reference only
    var message = new OrderMessage
    {
        OrderId = "ORD-001",
        ClaimCheckReference = reference
    };

    // Simulate receiving message
    var receivedMessage = SimulateMessageTransport(message);

    // Retrieve payload using reference
    var retrievedPayload = await _claimCheckProvider
        .RetrieveAsync(receivedMessage.ClaimCheckReference);

    // Assert
    Assert.Equal(largePayload, retrievedPayload);
}
```

---

## Performance Considerations

### Memory Usage

⚠️ **Critical**: The InMemory provider stores all payloads in RAM. Memory usage grows linearly with:

- Number of stored payloads
- Average payload size
- Retention period (TTL)

**Estimated Memory Usage:**

```
Memory (MB) ≈ (Avg Payload Size KB × Number of Payloads) / 1024
```

**Examples:**
- 1,000 payloads × 100KB avg = ~100 MB
- 10,000 payloads × 500KB avg = ~5 GB
- 100,000 payloads × 1MB avg = ~100 GB ⚠️

**Recommendations:**
- ✅ Use for **testing** with small datasets
- ✅ Use for **single-node development** with limited load
- ❌ **Avoid** for production high-throughput scenarios
- ❌ **Avoid** when payload count > 10,000 or total size > 1GB

### Cleanup Performance

The background cleanup service scans all entries periodically. Performance characteristics:

- **Scan Time**: O(n) where n = number of stored entries
- **Snapshot Overhead**: Creates array copy of all keys
- **Memory Spike**: Temporary allocation during snapshot

**Tuning Recommendations:**
- Reduce `CleanupInterval` if memory is constrained (cleanup more frequently)
- Increase `CleanupInterval` if CPU usage is a concern (cleanup less frequently)
- Disable cleanup (`enableCleanup: false`) in tests for deterministic behavior

### Compression Trade-offs

**Benefits:**
- ✅ Reduces memory usage for compressible payloads (JSON, XML, text)
- ✅ Compression ratio check prevents storing poorly-compressed data

**Costs:**
- ❌ CPU overhead for compression/decompression
- ❌ Increased latency for StoreAsync/RetrieveAsync

**When to Enable Compression:**
- ✅ Payloads are text-based (JSON, XML, CSV)
- ✅ Memory is constrained
- ❌ Payloads are already compressed (images, videos, archives)
- ❌ Latency is critical

### Concurrency

The provider uses `ConcurrentDictionary` for thread-safe access:

- **Read Operations** (RetrieveAsync): Lock-free, high concurrency
- **Write Operations** (StoreAsync): Lock-free atomic add
- **Delete Operations** (DeleteAsync): Lock-free atomic remove
- **Cleanup**: Snapshots keys to avoid enumeration issues

**Concurrency Limits**: No explicit limit, but performance degrades with:
- Very high write rates (> 10,000 ops/sec)
- Very large dictionary sizes (> 100,000 entries)

---

## Troubleshooting

### Problem: OutOfMemoryException

**Symptoms:**
```
System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown.
```

**Causes:**
- Too many payloads stored in memory
- Payloads too large
- Background cleanup not running or insufficient

**Solutions:**
1. **Reduce TTL**: Lower `DefaultTtl` to expire entries faster
   ```csharp
   options.DefaultTtl = TimeSpan.FromHours(1); // Instead of 7 days
   ```

2. **Increase cleanup frequency**:
   ```csharp
   options.CleanupInterval = TimeSpan.FromMinutes(15); // Instead of 1 hour
   ```

3. **Lower threshold**: Store fewer payloads in claim checks
   ```csharp
   options.PayloadThreshold = 512 * 1024; // 512KB instead of 256KB
   ```

4. **Enable compression**:
   ```csharp
   options.EnableCompression = true;
   options.CompressionThreshold = 1024; // Compress payloads >= 1KB
   ```

5. **Switch to cloud provider**: For production, use Azure/AWS/GCP providers

---

### Problem: Cleanup Service Not Running

**Symptoms:**
- Memory usage grows indefinitely
- Expired entries not removed
- Logs show "In-memory claim check cleanup is disabled"

**Causes:**
- `EnableCleanup` set to false in options
- `enableCleanup` parameter set to false in registration
- Background service not started (e.g., console app without host)

**Solutions:**
1. **Enable cleanup in options**:
   ```csharp
   options.EnableCleanup = true;
   ```

2. **Enable cleanup in registration**:
   ```csharp
   services.AddInMemoryClaimCheck(enableCleanup: true);
   ```

3. **For console apps**, ensure using `HostBuilder`:
   ```csharp
   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices((context, services) =>
       {
           services.AddInMemoryClaimCheck();
       })
       .Build();

   await host.RunAsync(); // Required for background services
   ```

---

### Problem: Checksum Validation Failed

**Symptoms:**
```
System.InvalidOperationException: Checksum validation failed for claim check 'cc-...'. Payload may be corrupted.
```

**Causes:**
- Memory corruption (extremely rare in managed .NET)
- Payload modified in storage (should not happen with ConcurrentDictionary)
- Bug in compression/decompression logic

**Solutions:**
1. **Disable checksum validation** (if acceptable for your scenario):
   ```csharp
   options.ValidateChecksum = false;
   ```

2. **Report the issue**: This indicates a potential bug in the provider

---

### Problem: Payload Already Expired

**Symptoms:**
```
System.InvalidOperationException: Claim check with ID 'cc-...' has expired.
```

**Causes:**
- TTL too short for your workflow
- Long processing delays between store and retrieve

**Solutions:**
1. **Increase TTL**:
   ```csharp
   options.DefaultTtl = TimeSpan.FromDays(14); // Instead of 7 days
   ```

2. **Process messages faster**: Reduce delay between store and retrieve

3. **Manual cleanup**: Delete references after retrieval to avoid accidental re-retrieval

---

### Problem: High CPU Usage from Cleanup

**Symptoms:**
- High CPU usage even when idle
- Frequent cleanup log messages

**Causes:**
- `CleanupInterval` too short
- Large number of entries to scan

**Solutions:**
1. **Increase cleanup interval**:
   ```csharp
   options.CleanupInterval = TimeSpan.FromHours(2); // Instead of 30 minutes
   ```

2. **Disable cleanup** if not needed:
   ```csharp
   services.AddInMemoryClaimCheck(enableCleanup: false);
   ```

---

## Comparison with Cloud Providers

### When to Use InMemory vs Cloud Providers

| Scenario | InMemory | Azure Blob | AWS S3 | GCP Storage |
|----------|----------|------------|--------|-------------|
| **Unit Testing** | ✅ Perfect | ❌ Overkill | ❌ Overkill | ❌ Overkill |
| **Integration Testing** | ✅ Good (if <1GB) | ✅ Good | ✅ Good | ✅ Good |
| **Local Development** | ✅ Perfect | ⚠️ Requires Azure | ⚠️ Requires AWS | ⚠️ Requires GCP |
| **Single-Node Production** | ⚠️ Limited | ✅ Recommended | ✅ Recommended | ✅ Recommended |
| **Distributed Production** | ❌ Not suitable | ✅ Recommended | ✅ Recommended | ✅ Recommended |
| **Durability Required** | ❌ No persistence | ✅ Durable | ✅ Durable | ✅ Durable |
| **Zero Dependencies** | ✅ Yes | ❌ Requires Azure SDK | ❌ Requires AWS SDK | ❌ Requires GCP SDK |
| **Cost** | ✅ Free (memory) | 💰 Pay per GB | 💰 Pay per GB | 💰 Pay per GB |

### Migration Path: InMemory → Cloud Provider

Switching from InMemory to a cloud provider is straightforward:

**Before (InMemory):**
```csharp
services.AddInMemoryClaimCheck(options =>
{
    options.PayloadThreshold = 256 * 1024;
    options.DefaultTtl = TimeSpan.FromDays(7);
});
```

**After (Azure Blob - FUTURE):**
```csharp
services.AddAzureBlobClaimCheck(options =>
{
    options.ConnectionString = configuration["Azure:Storage:ConnectionString"];
    options.ContainerName = "claim-checks";
    options.PayloadThreshold = 256 * 1024;
    options.DefaultTtl = TimeSpan.FromDays(7);
});
```

The `IClaimCheckProvider` interface remains the same - **no code changes required** in your application logic!

---

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](../../../CONTRIBUTING.md) for guidelines.

### Reporting Issues

If you encounter bugs or have feature requests:

1. Check [existing issues](https://github.com/TrigintaFaces/Excalibur/issues)
2. Create a new issue with:
   - Clear description of the problem
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (.NET version, OS, etc.)

### Pull Requests

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Write tests for your changes (≥95% coverage)
4. Ensure all tests pass (`dotnet test`)
5. Submit a pull request

---

## License

This project is licensed under multiple licenses. See the following files in the project root:

- [LICENSE-EXCALIBUR.txt](..\..\..\licenses\LICENSE-EXCALIBUR.txt) - Excalibur License 1.0
- [LICENSE-AGPL-3.0.txt](..\..\..\licenses\LICENSE-AGPL-3.0.txt) - GNU Affero General Public License v3.0
- [LICENSE-SSPL-1.0.txt](..\..\..\licenses\LICENSE-SSPL-1.0.txt) - Server Side Public License v1.0
- [LICENSE-APACHE-2.0.txt](..\..\..\licenses\LICENSE-APACHE-2.0.txt) - Apache License 2.0

---

## Additional Resources

- [Claim Check Pattern Documentation](https://learn.microsoft.com/en-us/azure/architecture/patterns/claim-check)
- [ClaimCheck Abstractions](../Excalibur.Dispatch.Patterns/README.md)
- [Azure Blob Provider](../Excalibur.Dispatch.Patterns.Azure/README.md) [FUTURE]
- [AWS S3 Provider](../Excalibur.Dispatch.ClaimCheck.AwsS3/README.md) [FUTURE]

---

**Copyright © 2026 The Excalibur Project**


