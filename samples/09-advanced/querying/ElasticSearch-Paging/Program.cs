// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Excalibur.Data.ElasticSearch;
using ElasticSearch_Paging.Domain;
using ElasticSearch_Paging.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ============================================================================
// ElasticSearch Paging Strategies
// ============================================================================
//
// Demonstrates:
//   1. Offset-based paging (From/Size) -- simple but limited to 10,000 hits
//   2. Cursor-based deep pagination (SearchAfter) -- production-recommended
//
// Prerequisites:
//   - Elasticsearch running on http://localhost:9200
//   - docker run -d --name es -p 9200:9200 -e "discovery.type=single-node" \
//       -e "xpack.security.enabled=false" elasticsearch:8.15.0
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddElasticsearchServices(builder.Configuration, registry: null);
builder.Services.AddRepository<ILogRepository, LogRepository>();

var app = builder.Build();

await app.InitializeElasticsearchIndexesAsync().ConfigureAwait(false);

Console.WriteLine("ElasticSearch Paging Strategies");
Console.WriteLine("===============================");
Console.WriteLine();

using var scope = app.Services.CreateScope();
var repo = scope.ServiceProvider.GetRequiredService<ILogRepository>();
var ct = CancellationToken.None;

// --- Seed 50 log entries with varied timestamps and levels ---
Console.WriteLine("Seeding 50 log entries...");

var levels = new[] { "DEBUG", "INFO", "WARN", "ERROR", "FATAL" };
var services = new[] { "order-api", "payment-svc", "inventory-svc", "notification-svc", "gateway" };
var baseTime = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

var entries = new List<LogEntry>();
for (var i = 1; i <= 50; i++)
{
    entries.Add(new LogEntry
    {
        Id = $"log-{i:D3}",
        Timestamp = baseTime.AddMinutes(i * 5),
        Level = levels[i % levels.Length],
        Message = $"Operation {i} completed with status code {200 + (i % 5)}",
        Service = services[i % services.Length],
        TraceId = $"trace-{i / 10 + 1:D3}",
    });
}

await repo.BulkAddOrUpdateAsync(entries, e => e.Id, ct).ConfigureAwait(false);

// Wait for Elasticsearch to refresh the index so all documents are searchable
await Task.Delay(1500, ct).ConfigureAwait(false);
Console.WriteLine("Seeded 50 log entries.");
Console.WriteLine();

// ============================================================================
// Strategy 1: Offset-Based Paging (From/Size)
// ============================================================================
//
// How it works:
//   - From = number of documents to skip
//   - Size = number of documents to return
//   - Equivalent to SQL's OFFSET/FETCH or LIMIT/OFFSET
//
// Limitations:
//   - Elasticsearch enforces a max of 10,000 for (From + Size) by default.
//     Requesting From=9990, Size=20 will fail because 9990+20 > 10,000.
//   - Performance degrades with large From values because ES must fetch and
//     discard all preceding documents on every shard.
//   - Best for: small datasets, admin UIs, or when total results < 10,000.
//
// ============================================================================

Console.WriteLine("=== Strategy 1: Offset-Based Paging (From/Size) ===");
Console.WriteLine();

const int pageSize = 10;

for (var page = 1; page <= 3; page++)
{
    var from = (page - 1) * pageSize;

    var searchDescriptor = new SearchRequestDescriptor<LogEntry>()
        .Index("logs-paging")
        .From(from)
        .Size(pageSize)
        .Sort(s => s.Field(f => f.Timestamp, new FieldSort { Order = SortOrder.Desc }));

    var response = await repo.SearchAsync(searchDescriptor, ct).ConfigureAwait(false);

    Console.WriteLine($"  Page {page} (From={from}, Size={pageSize}) -- {response.Documents.Count} results:");
    foreach (var doc in response.Documents)
    {
        Console.WriteLine($"    [{doc.Level,-5}] {doc.Timestamp:HH:mm} | {doc.Service,-18} | {doc.Message}");
    }
    Console.WriteLine();
}

Console.WriteLine("  NOTE: Offset paging is limited to 10,000 total hits (From + Size <= 10,000).");
Console.WriteLine("        For deeper pagination, use SearchAfter (Strategy 2).");
Console.WriteLine();

// ============================================================================
// Strategy 2: Cursor-Based Paging (SearchAfter)
// ============================================================================
//
// How it works:
//   - Sort by one or more fields. The last field should be a unique tiebreaker
//     (e.g., the document ID) to guarantee a stable sort order.
//   - For the first page, issue a normal search with Sort + Size.
//   - For subsequent pages, pass the sort values from the LAST hit of the
//     previous page as SearchAfter. ES returns the next page starting
//     immediately after that cursor position.
//
// Advantages:
//   - No 10,000 hit limit -- can paginate through millions of documents.
//   - Consistent performance regardless of page depth.
//   - Ideal for: production systems, large datasets, infinite scroll UIs.
//
// Tradeoffs:
//   - Cannot jump to arbitrary page numbers (must traverse sequentially).
//   - Sort values change if documents are inserted/deleted between pages.
//
// ============================================================================

Console.WriteLine("=== Strategy 2: Cursor-Based Paging (SearchAfter) ===");
Console.WriteLine();

IList<FieldValue>? searchAfterValues = null;

for (var page = 1; page <= 3; page++)
{
    var searchDescriptor = new SearchRequestDescriptor<LogEntry>()
        .Index("logs-paging")
        .Size(pageSize)
        .Sort(s => s.Field(f => f.Timestamp, new FieldSort { Order = SortOrder.Desc }))
        .Sort(s => s.Field(f => f.Id, new FieldSort { Order = SortOrder.Asc }));

    // For pages after the first, provide the cursor from the previous page's last hit
    if (searchAfterValues is not null)
    {
        searchDescriptor.SearchAfter(searchAfterValues);
    }

    var response = await repo.SearchAsync(searchDescriptor, ct).ConfigureAwait(false);
    var hits = response.Hits.ToList();

    Console.WriteLine($"  Page {page} (SearchAfter cursor) -- {hits.Count} results:");
    foreach (var hit in hits)
    {
        var doc = hit.Source!;
        Console.WriteLine($"    [{doc.Level,-5}] {doc.Timestamp:HH:mm} | {doc.Service,-18} | {doc.Message}");
    }

    // Capture the sort values from the last hit to use as the cursor for the next page
    if (hits.Count > 0)
    {
        searchAfterValues = hits[^1].Sort?.ToList();
        Console.WriteLine($"    >> Cursor for next page: [{string.Join(", ", searchAfterValues)}]");
    }

    Console.WriteLine();
}

Console.WriteLine("  SearchAfter has NO 10,000 hit limit and performs consistently at any depth.");
Console.WriteLine();

// ============================================================================
// Note on Scroll API
// ============================================================================
//
// The Scroll API was historically used for deep pagination, but it is now
// deprecated for search use cases (Elasticsearch 7.10+). Scroll creates
// a point-in-time snapshot and keeps search context alive on the server,
// which consumes significant resources.
//
// Use Scroll ONLY for:
//   - Bulk data export / reindexing operations
//   - Processing all documents in an index (ETL pipelines)
//
// For search/pagination use cases, always prefer SearchAfter.
//
// ============================================================================

Console.WriteLine("=== Summary ===");
Console.WriteLine();
Console.WriteLine("  Offset (From/Size): Simple, supports random page access, limited to 10k hits.");
Console.WriteLine("  SearchAfter:        Production-grade, no depth limit, sequential access only.");
Console.WriteLine("  Scroll API:         Deprecated for search -- use only for bulk export/reindex.");
Console.WriteLine();
Console.WriteLine("Done!");
