# Streaming Handlers Sample

This sample demonstrates all four streaming handler patterns in Dispatch, enabling memory-efficient processing of large data sets and long-running operations.

## What This Sample Demonstrates

| Handler Type | Pattern | Demo |
|--------------|---------|------|
| `IStreamingDocumentHandler<TDocument, TOutput>` | Document → Stream | CSV parsing |
| `IStreamConsumerHandler<TDocument>` | Stream → Sink | Batch import |
| `IStreamTransformHandler<TInput, TOutput>` | Stream → Stream | Data enrichment |
| `IProgressDocumentHandler<TDocument>` | Progress reporting | Report export |

## Prerequisites

- .NET 9.0 SDK or later

## Running the Sample

```bash
cd samples/09-advanced/StreamingHandlers
dotnet run
```

## Expected Output

```
=== Streaming Handlers Sample ===

--- Demo 1: Output Streaming (Document -> Stream) ---
Parsing CSV document into individual rows...
  Row: Id=1, Name=Alice, Email=alice@example.com
  Row: Id=2, Name=Bob, Email=bob@example.com
  Row: Id=3, Name=Charlie, Email=charlie@example.com
  Row: Id=4, Name=Diana, Email=diana@example.com
  Row: Id=5, Name=Eve, Email=eve@example.com
Output streaming complete!

--- Demo 2: Input Streaming (Stream -> Sink) ---
Consuming stream with batch processing...
  Flushing batch of 5 items: [User1, User2, User3, User4, User5]
  Flushing batch of 5 items: [User6, User7, User8, User9, User10]
  Flushing batch of 5 items: [User11, User12, User13, User14, User15]
Input streaming complete!

--- Demo 3: Stream Transform (Stream -> Stream) ---
Enriching data records with additional information...
  Enriched: Customer1 -> Score: 723, Tier: Standard
  Enriched: Customer2 -> Score: 812, Tier: Premium
  ...
Stream transform complete!

--- Demo 4: Progress Reporting ---
Exporting report with progress updates...
  [                    ]   0.0% - Initializing export
  [==                  ]  10.0% - Export initialized
  [===                 ]  18.0% - Rendering page 1 of 10
  ...
  [====================] 100.0% - Export complete
Progress reporting complete!

=== All demos completed ===
```

## Code Highlights

### 1. Output Streaming (IStreamingDocumentHandler)

Document-to-stream transformation using `yield return`:

```csharp
public class CsvStreamingHandler : IStreamingDocumentHandler<CsvDocument, DataRow>
{
    public async IAsyncEnumerable<DataRow> HandleAsync(
        CsvDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var line in document.Content.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return ParseRow(line);
        }
    }
}
```

### 2. Input Streaming (IStreamConsumerHandler)

Stream consumption with batching:

```csharp
public class BatchImportHandler : IStreamConsumerHandler<DataRow>
{
    public async Task HandleAsync(
        IAsyncEnumerable<DataRow> documents,
        CancellationToken cancellationToken)
    {
        var batch = new List<DataRow>();

        await foreach (var row in documents.WithCancellation(cancellationToken))
        {
            batch.Add(row);
            if (batch.Count >= 1000)
            {
                await FlushBatchAsync(batch, cancellationToken);
                batch.Clear();
            }
        }
    }
}
```

### 3. Stream Transform (IStreamTransformHandler)

Stream-to-stream transformation:

```csharp
public class RecordEnricher : IStreamTransformHandler<DataRow, EnrichedRow>
{
    public async IAsyncEnumerable<EnrichedRow> HandleAsync(
        IAsyncEnumerable<DataRow> input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in input.WithCancellation(cancellationToken))
        {
            var enriched = await EnrichAsync(row, cancellationToken);
            yield return enriched;
        }
    }
}
```

### 4. Progress Reporting (IProgressDocumentHandler)

Long-running operations with progress:

```csharp
public class ReportExportHandler : IProgressDocumentHandler<ReportDocument>
{
    public async Task HandleAsync(
        ReportDocument document,
        IProgress<DocumentProgress> progress,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < document.PageCount; i++)
        {
            await RenderPageAsync(i, cancellationToken);

            progress.Report(DocumentProgress.FromItems(
                itemsProcessed: i + 1,
                totalItems: document.PageCount,
                currentPhase: $"Rendering page {i + 1}"));
        }

        progress.Report(DocumentProgress.Completed(document.PageCount));
    }
}
```

## Key Concepts

### Backpressure

Streaming handlers provide natural backpressure. The consumer controls the pace:

```csharp
// Consumer pulls items one at a time
await foreach (var item in handler.HandleAsync(document, ct))
{
    // Producer waits here until consumer requests next item
    await ProcessItemAsync(item, ct);
}
```

### Memory Efficiency

Process data larger than available memory by never materializing the full dataset:

| Pattern | Memory | Example |
|---------|--------|---------|
| Load all | O(n) | `items.ToList()` |
| Streaming | O(1) | `await foreach` |

### Cancellation

All handlers support cancellation via `CancellationToken`:

```csharp
public async IAsyncEnumerable<T> HandleAsync(
    TDocument document,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    // Check periodically
    cancellationToken.ThrowIfCancellationRequested();
}
```

## When to Use Each Handler

| Scenario | Handler |
|----------|---------|
| Parse large file into records | `IStreamingDocumentHandler` |
| Batch import from stream | `IStreamConsumerHandler` |
| Enrich/transform data pipeline | `IStreamTransformHandler` |
| Long-running export with UI | `IProgressDocumentHandler` |

## Related Samples

- [01-getting-started/GettingStarted](../../01-getting-started/GettingStarted/) - Basic handler patterns
- [04-reliability/OutboxPattern](../../04-reliability/OutboxPattern/) - Reliable message processing

---

*Category: Advanced | Sprint 436 - Streaming Document Handler Epic*
