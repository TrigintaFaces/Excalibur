---
sidebar_position: 9
title: Streaming
description: Process data streams efficiently with Chunk<T>, StreamingDocument, and IStreamingDocumentHandler
---

# Streaming Patterns

Dispatch provides helper types for memory-efficient streaming scenarios where data flows through the system as asynchronous sequences rather than complete collections.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [actions and handlers](../core-concepts/actions-and-handlers.md) and `IAsyncEnumerable<T>`

## When to Use Streaming

| Scenario | Solution |
|----------|----------|
| Large files (CSV, JSON, XML) | `IStreamingDocumentHandler<TDocument, TOutput>` |
| Positional awareness in streams | `Chunk<T>` with `WithChunkInfo()` |
| Documents with stream metadata | Derive from `StreamingDocument` |
| Single-item to stream conversion | `AsSingleChunk<T>()` |

## Chunk&lt;T&gt;

`Chunk<T>` is a lightweight `readonly record struct` that wraps streamed data with positional metadata. It enables handlers to know where they are in a stream without buffering the entire sequence.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Data` | `T` | The data payload for this chunk |
| `Index` | `long` | Zero-based position in the stream |
| `IsFirst` | `bool` | Whether this is the first chunk |
| `IsLast` | `bool` | Whether this is the last chunk |
| `IsMiddle` | `bool` | Neither first nor last (computed) |
| `IsSingle` | `bool` | Both first and last (computed) |

### Basic Usage

```csharp
using Excalibur.Dispatch.Abstractions.Streaming;

await foreach (var chunk in stream.WithChunkInfo())
{
    if (chunk.IsFirst)
    {
        // Initialize resources, write headers, etc.
        Console.WriteLine("Starting stream processing...");
    }

    // Process the data
    await ProcessAsync(chunk.Data, chunk.Index);

    if (chunk.IsLast)
    {
        // Finalize, flush buffers, write footers
        Console.WriteLine($"Completed: {chunk.Index + 1} items processed");
    }
}
```

### Use Cases

#### First/Last Detection for Batch Boundaries

```csharp
public async IAsyncEnumerable<ProcessedRecord> ProcessBatchAsync(
    IAsyncEnumerable<RawRecord> records,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    SqlTransaction? transaction = null;

    await foreach (var chunk in records.WithChunkInfo(cancellationToken))
    {
        if (chunk.IsFirst)
        {
            // Start transaction on first item
            transaction = await _connection.BeginTransactionAsync(cancellationToken);
        }

        var processed = await TransformAsync(chunk.Data, cancellationToken);
        yield return processed;

        if (chunk.IsLast && transaction is not null)
        {
            // Commit on last item
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
```

#### Progress Tracking

```csharp
public async Task ProcessWithProgressAsync(
    IAsyncEnumerable<DataItem> items,
    long expectedCount,
    IProgress<double> progress,
    CancellationToken cancellationToken)
{
    await foreach (var chunk in items.WithChunkInfo(cancellationToken))
    {
        await ProcessItemAsync(chunk.Data, cancellationToken);

        // Report progress based on position
        var percentage = (double)(chunk.Index + 1) / expectedCount * 100;
        progress.Report(percentage);
    }
}
```

#### Writing Structured Output

```csharp
public async Task WriteJsonArrayAsync(
    IAsyncEnumerable<OrderDto> orders,
    Stream output,
    CancellationToken cancellationToken)
{
    await using var writer = new Utf8JsonWriter(output);

    await foreach (var chunk in orders.WithChunkInfo(cancellationToken))
    {
        if (chunk.IsFirst)
        {
            writer.WriteStartArray();
        }

        JsonSerializer.Serialize(writer, chunk.Data);

        if (chunk.IsLast)
        {
            writer.WriteEndArray();
            await writer.FlushAsync(cancellationToken);
        }
    }
}
```

### Performance Characteristics

- **Stack-allocated**: As a `readonly record struct`, `Chunk<T>` avoids heap allocations
- **Minimal overhead**: Only one-item lookahead to determine `IsLast`
- **Memory efficient**: Only two elements held in memory at any time

## StreamingDocument

`StreamingDocument` is an abstract base record for documents that participate in streaming workflows. It provides standard metadata for correlating and ordering documents within a stream.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `StreamId` | `string` | Unique identifier for the stream |
| `SequenceNumber` | `long` | Zero-based position in the stream |
| `CorrelationId` | `string?` | Optional correlation ID for tracing |
| `Timestamp` | `DateTimeOffset` | When the document was created |
| `IsEndOfStream` | `bool` | Whether this is the terminal document |

### Creating Derived Types

```csharp
using Excalibur.Dispatch.Abstractions.Streaming;

// Domain-specific streaming document
public record ImportRecord(
    string StreamId,
    long SequenceNumber,
    string CustomerId,
    decimal Amount,
    DateTimeOffset TransactionDate) : StreamingDocument(StreamId, SequenceNumber);

// Usage
var record = new ImportRecord(
    StreamId: "import-batch-001",
    SequenceNumber: 42,
    CustomerId: "CUST-123",
    Amount: 150.00m,
    TransactionDate: DateTimeOffset.UtcNow)
{
    CorrelationId = "trace-abc-123",
    IsEndOfStream = false
};
```

### Use Cases

| Scenario | Example |
|----------|---------|
| Batch import/export | Each row in a CSV import is a `StreamingDocument` |
| Event replay | Replaying events from a stream with position tracking |
| Change data capture | CDC events with stream correlation |
| Pipeline processing | Documents flowing through multiple handlers |

## Extension Methods

The `AsyncEnumerableChunkExtensions` class provides methods for working with `IAsyncEnumerable<T>` streams.

### WithChunkInfo

Wraps each element with positional metadata:

```csharp
IAsyncEnumerable<string> lines = ReadLinesAsync(file);

// Transform to chunks with position info
IAsyncEnumerable<Chunk<string>> chunks = lines.WithChunkInfo();

await foreach (var chunk in chunks)
{
    Console.WriteLine($"Line {chunk.Index}: {chunk.Data}");
}
```

### AsSingleChunk

Creates a single-element chunked stream from a value:

```csharp
// When you have a single result but need to return IAsyncEnumerable<Chunk<T>>
var summary = new ReportSummary { /* ... */ };

await foreach (var chunk in summary.AsSingleChunk())
{
    // chunk.IsFirst == true
    // chunk.IsLast == true
    // chunk.IsSingle == true
    await ProcessSummaryAsync(chunk.Data);
}
```

This is useful when integrating single-item results with streaming APIs that expect `IAsyncEnumerable<Chunk<T>>`.

## IStreamingDocumentHandler

The `IStreamingDocumentHandler<TDocument, TOutput>` interface defines handlers that produce streams from documents.

### Interface Definition

```csharp
public interface IStreamingDocumentHandler<in TDocument, out TOutput>
    where TDocument : IDispatchDocument
{
    IAsyncEnumerable<TOutput> HandleAsync(
        TDocument document,
        CancellationToken cancellationToken);
}
```

### Implementation Example

```csharp
public class CsvImportHandler : IStreamingDocumentHandler<CsvDocument, ImportRecord>
{
    public async IAsyncEnumerable<ImportRecord> HandleAsync(
        CsvDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long sequenceNumber = 0;

        await foreach (var line in document.ReadLinesAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fields = line.Split(',');

            yield return new ImportRecord(
                StreamId: document.BatchId,
                SequenceNumber: sequenceNumber++,
                CustomerId: fields[0],
                Amount: decimal.Parse(fields[1]),
                TransactionDate: DateTimeOffset.Parse(fields[2]));
        }
    }
}
```

### Registration

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    // CsvImportHandler is automatically registered
});
```

### Combining with Chunk&lt;T&gt;

Add positional metadata to streaming handler output:

```csharp
public class EnrichedCsvHandler : IStreamingDocumentHandler<CsvDocument, Chunk<ImportRecord>>
{
    private readonly CsvImportHandler _inner;

    public EnrichedCsvHandler(CsvImportHandler inner) => _inner = inner;

    public IAsyncEnumerable<Chunk<ImportRecord>> HandleAsync(
        CsvDocument document,
        CancellationToken cancellationToken)
    {
        return _inner.HandleAsync(document, cancellationToken)
            .WithChunkInfo(cancellationToken);
    }
}
```

## Best Practices

### Memory Efficiency

```csharp
// Good: Stream processing without buffering
await foreach (var chunk in source.WithChunkInfo(cancellationToken))
{
    await ProcessAsync(chunk.Data);
}

// Avoid: Materializing entire stream defeats the purpose
var allChunks = await source.WithChunkInfo().ToListAsync(); // Don't do this
```

### Cancellation

```csharp
public async IAsyncEnumerable<Chunk<T>> ProcessAsync<T>(
    IAsyncEnumerable<T> source,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (var chunk in source.WithChunkInfo(cancellationToken))
    {
        // Check cancellation between operations
        cancellationToken.ThrowIfCancellationRequested();

        yield return chunk;
    }
}
```

### ConfigureAwait in Libraries

When implementing streaming handlers in library code:

```csharp
public async IAsyncEnumerable<TOutput> HandleAsync(
    TDocument document,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await using var reader = await OpenReaderAsync().ConfigureAwait(false);

    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
        yield return MapToOutput(reader.Current);
    }
}
```

## See Also

- [Patterns Overview](./index.md) - All messaging and integration patterns
- [Kafka Transport](../transports/kafka.md) - Kafka streaming transport integration
- [Performance Overview](../performance/index.md) - Performance optimization strategies for high-throughput scenarios

## Related Documentation

- [Actions and Handlers](../core-concepts/actions-and-handlers.md) - Handler types overview
- [Dependency Injection](../core-concepts/dependency-injection.md) - Handler registration
- [Pipeline](../pipeline/) - Message processing pipeline
