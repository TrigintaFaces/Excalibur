# ElasticSearch Paging Strategies

Demonstrates offset-based paging, cursor-based deep pagination (SearchAfter), and explains when to use each approach.

## Prerequisites

Elasticsearch running locally:

```bash
docker run -d --name es -p 9200:9200 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  elasticsearch:8.15.0
```

## Run

```bash
dotnet run
```

## Paging Strategies

### 1. Offset Paging (From/Size)

Uses `From` (skip) and `Size` (take) parameters, equivalent to SQL `OFFSET/FETCH`.

**When to use:**
- Small datasets (< 10,000 total results)
- Admin UIs with numbered page navigation
- Scenarios where random page access is needed (jump to page 5)

**Limitations:**
- Elasticsearch enforces `From + Size <= 10,000` by default (`index.max_result_window`)
- Performance degrades with large `From` values -- every shard must fetch and discard all preceding documents
- Not suitable for infinite scroll or deep pagination

### 2. Cursor-Based Paging (SearchAfter)

Uses sort values from the last hit of the previous page as a cursor for the next page. Requires a deterministic sort order -- always include a unique tiebreaker field (e.g., document ID) as the last sort field.

**When to use:**
- Production systems with large datasets
- Infinite scroll UIs
- Any scenario requiring pagination beyond 10,000 results
- Real-time data where consistent performance matters

**Tradeoffs:**
- Cannot jump to arbitrary page numbers (sequential access only)
- Results may shift if documents are inserted or deleted between pages

### 3. Scroll API (Deprecated for Search)

The Scroll API creates a point-in-time snapshot for traversing large result sets. It is **deprecated for search use cases** as of Elasticsearch 7.10+.

**Only use Scroll for:**
- Bulk data export
- Reindexing operations
- ETL pipelines processing all documents in an index

**Why not for search:**
- Consumes server resources (keeps search context alive)
- Not designed for real-time user-facing pagination
- SearchAfter is strictly superior for search pagination

## Key Concepts

| Strategy | Max Depth | Random Access | Performance at Depth | Use Case |
|----------|-----------|--------------|---------------------|----------|
| From/Size | 10,000 | Yes | Degrades | Admin UIs, small datasets |
| SearchAfter | Unlimited | No | Constant | Production, infinite scroll |
| Scroll | Unlimited | No | Constant | Bulk export only |

## Excalibur Integration

This sample uses `ElasticRepositoryBase<T>.SearchAsync()` which accepts a `SearchRequestDescriptor<T>` directly. Both paging strategies work through the same repository method -- the only difference is how you configure the search descriptor.
