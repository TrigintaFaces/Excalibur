# Claim Check Pattern Examples

This directory contains comprehensive examples demonstrating the Claim Check pattern implementation in the Excalibur.Dispatch.CloudNativePatterns library.

## Examples Included

### 1. Basic Claim Check Example (`Program.cs`)
The main example application that demonstrates:
- Small message handling (no claim check)
- Large message handling (automatic claim check)
- Chunked message handling for very large payloads
- Compressed message handling
- Performance testing scenarios

### 2. Configuration Examples (`ConfigurationExamples.cs`)
Shows various configuration options:
- Basic configuration with Azurite
- Production Azure configuration
- Advanced chunking configuration
- Compression settings
- Cleanup and retention policies

### 3. Integration Examples (`IntegrationExamples.cs`)
Demonstrates integration with:
- Azure Service Bus
- Message processors
- Distributed systems
- Error handling and retry logic

### 4. Migration Guide (`MigrationGuide.cs`)
Shows how to migrate from:
- Direct blob storage usage
- Custom claim check implementations
- Legacy patterns

## Prerequisites

1. **.NET 8.0 or later**
2. **Azure Storage Emulator (Azurite)** for local development
3. **Azure Storage Account** for production scenarios

## Running the Examples

### Local Development (Azurite)

1. Start Azurite:
   ```bash
   azurite --silent
   ```

2. Run the example:
   ```bash
   dotnet run --project ClaimCheck
   ```

### Production Azure

1. Update the connection string in `appsettings.json`:
   ```json
   {
     "ClaimCheck": {
       "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
       "ContainerName": "claim-checks"
     }
   }
   ```

2. Run the example:
   ```bash
   dotnet run --project ClaimCheck --environment Production
   ```

## Key Concepts Demonstrated

### 1. Automatic Claim Check
Messages exceeding the configured threshold (default 64KB) are automatically stored in blob storage:

```csharp
services.AddClaimCheck(options =>
{
    options.PayloadThreshold = 64 * 1024; // 64KB
});
```

### 2. Chunking for Large Payloads
Very large messages (>1MB) are automatically chunked for parallel upload/download:

```csharp
options.ChunkSize = 1024 * 1024; // 1MB chunks
options.MaxConcurrency = 4; // Parallel chunks
```

### 3. Smart Compression
Payloads are analyzed for compressibility using entropy calculation:

```csharp
options.EnableCompression = true;
options.CompressionThreshold = 1024; // Min size for compression
options.MinCompressionRatio = 0.8; // Min 20% size reduction
```

### 4. Automatic Cleanup
Background service cleans up expired claim checks:

```csharp
options.EnableCleanup = true;
options.CleanupInterval = TimeSpan.FromMinutes(5);
options.RetentionPeriod = TimeSpan.FromHours(24);
```

### 5. Performance Optimizations
- Zero-allocation design using pooled buffers
- Stream-based operations
- Connection pooling
- Parallel chunk processing

## Monitoring and Metrics

The examples include comprehensive metrics:

- Storage operations (store/retrieve/delete)
- Compression statistics
- Throughput measurements
- Error rates and retries

View metrics in the console output or configure OpenTelemetry exporters for production monitoring.

## Troubleshooting

### Common Issues

1. **Azurite Connection Failed**
   - Ensure Azurite is running: `azurite --silent`
   - Check connection string: `UseDevelopmentStorage=true`

2. **Large Message Timeout**
   - Increase timeout in options: `options.OperationTimeout = TimeSpan.FromMinutes(5)`
   - Consider reducing chunk size for slow connections

3. **Memory Usage**
   - Monitor buffer pool usage
   - Adjust max concurrent operations
   - Enable pooling diagnostics

### Performance Tuning

1. **For High Throughput**
   ```csharp
   options.MaxConcurrency = Environment.ProcessorCount;
   options.BufferPoolSize = 100;
   options.ChunkSize = 4 * 1024 * 1024; // 4MB chunks
   ```

2. **For Low Memory**
   ```csharp
   options.MaxConcurrency = 2;
   options.BufferPoolSize = 10;
   options.ChunkSize = 512 * 1024; // 512KB chunks
   ```

3. **For Slow Networks**
   ```csharp
   options.OperationTimeout = TimeSpan.FromMinutes(10);
   options.RetryPolicy = new ExponentialBackoffRetry(3);
   ```

## Next Steps

1. Review the source code for detailed implementation
2. Run the performance benchmarks
3. Integrate into your messaging pipeline
4. Configure monitoring and alerting
5. Test with your actual payload sizes

## Support

For issues or questions:
1. Check the main documentation
2. Review test cases for examples
3. Enable debug logging for troubleshooting
4. Submit issues with reproduction steps
