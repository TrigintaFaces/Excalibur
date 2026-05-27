---
sidebar_position: 13
title: Pagination
description: Offset-based and cursor-based pagination patterns for paging through large result sets.
---

# Pagination

Excalibur provides two pagination strategies out of the box. Both live in `Excalibur.EventSourcing` and are backend-agnostic — they work with any data store.

| Strategy | Best for | Consistency | Performance at depth |
|---|---|---|---|
| **Offset-based** (`PagedResult<T>`) | Admin UIs, small datasets, jump-to-page | May skip/duplicate on concurrent writes | Degrades (DB scans skipped rows) |
| **Cursor-based** (`CursorPagedResult<T>`) | APIs, infinite scroll, large datasets, real-time feeds | Stable under concurrent writes | Constant (keyset seek) |

## When to Use Which

**Use offset-based** when:
- Users need to jump to page 5 of 20
- The dataset is small enough that deep offsets are not a concern
- You need a traditional page-number UI (e.g., admin dashboards)

**Use cursor-based** when:
- The dataset is large or frequently changing
- Deep pagination is expected (page 100+)
- You are building an API consumed by mobile/SPA clients
- Consistency matters more than jump-to-page

## Installation

Both types are included in the core abstractions package:

```bash
dotnet add package Excalibur.EventSourcing.Abstractions
```

For Elasticsearch cursor helpers:

```bash
dotnet add package Excalibur.Data.ElasticSearch
```

---

## Offset-Based Pagination

### PagedResult&lt;T&gt;

A traditional page-number result with computed metadata:

```csharp
using Excalibur.EventSourcing;

// From a query handler
var items = await repository.GetOrdersAsync(page: 2, pageSize: 25, cancellationToken);
var total = await repository.CountOrdersAsync(cancellationToken);

return new PagedResult<OrderDto>(items, pageNumber: 2, pageSize: 25, totalItems: total);
```

### Properties

| Property | Type | Description |
|---|---|---|
| `Items` | `IList<T>` | Items on the current page |
| `PageNumber` | `int` | Current page (1-based) |
| `PageSize` | `int` | Items per page |
| `TotalItems` | `long` | Total items across all pages |
| `TotalPages` | `int` | Computed: `ceil(TotalItems / PageSize)` |
| `HasNextPage` | `bool` | `PageNumber < TotalPages` |
| `HasPreviousPage` | `bool` | `PageNumber > 1` |
| `IsFirstPage` | `bool` | `PageNumber == 1` |
| `IsLastPage` | `bool` | `PageNumber == TotalPages` |

### Controller Example

```csharp
[HttpGet]
public async Task<PagedResult<OrderDto>> GetOrders(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    CancellationToken cancellationToken = default)
{
    return await dispatcher.DispatchAsync(
        new GetOrdersQuery(page, pageSize),
        cancellationToken);
}
```

### Convenience Features

`PagedResult<T>` supports indexer access and enumeration:

```csharp
var result = new PagedResult<OrderDto>(orders, pageNumber: 1, pageSize: 10, totalItems: 100);

// Direct index access
var first = result[0];

// Enumeration
foreach (var order in result)
{
    Console.WriteLine(order.Id);
}
```

---

## Cursor-Based Pagination

### CursorPagedResult&lt;T&gt;

A continuation-token result following the pattern used by Azure SDKs (`ContinuationToken`) and Google Cloud APIs (`nextPageToken`):

```csharp
using Excalibur.EventSourcing;

return new CursorPagedResult<OrderDto>(items, pageSize: 25, totalRecords: 1000, nextCursor: "eyJ...");
```

### Properties

| Property | Type | Description |
|---|---|---|
| `Items` | `IEnumerable<T>` | Items on the current page |
| `PageSize` | `int` | Items per page |
| `TotalRecords` | `long` | Total records available |
| `TotalPages` | `int` | Computed: `ceil(TotalRecords / PageSize)` |
| `NextCursor` | `string?` | Opaque token for the next page (`null` = last page) |
| `HasMore` | `bool` | `NextCursor is not null` |

### CursorEncoder

The `CursorEncoder` produces opaque, URL-safe Base64url strings from sort values. Consumers never parse cursors — they pass them back unchanged on the next request.

```csharp
using Excalibur.EventSourcing;

// Encode sort values from the last item on the page
string cursor = CursorEncoder.Encode("2026-04-21", 42L, "order-abc");

// Decode on the next request (null/empty/invalid → null = first page)
object?[]? sortValues = CursorEncoder.Decode(cursor);
```

**Supported types:**

| .NET Type | Cursor encoding | Decoded as |
|---|---|---|
| `string` | JSON string | `string` |
| `long`, `int` | JSON number | `long` |
| `double`, `float`, `decimal` | JSON number | `long` or `double` |
| `bool` | JSON boolean | `bool` |
| `null` | JSON null | `null` |
| `DateTimeOffset`, `DateTime` | Unix epoch milliseconds | `long` |
| `DateOnly`, `TimeOnly` | ISO 8601 string | `string` |

**Safety**: Invalid or tampered cursors return `null` from `Decode`, restarting from the beginning rather than throwing. This is intentional — a corrupt cursor should not fail a user's request.

### PageNavigation

The `PageNavigation` enum supports bidirectional cursor pagination:

```csharp
public enum PageNavigation
{
    First = 0,    // No cursor needed
    Previous = 1, // Reverse sort, then reverse items
    Next = 2,     // Forward with cursor
    Last = 3      // Reverse sort from end, then reverse items
}
```

---

## Elasticsearch Integration

The `ElasticSearchCursorHelper` bridges the generic cursor types with Elasticsearch's `search_after` API.

### ElasticSearchCursorHelper

| Method | Description |
|---|---|
| `DecodeCursor(string?)` | Decodes a cursor into `IList<FieldValue>` for `search_after` |
| `EncodeCursor(IReadOnlyCollection<FieldValue>)` | Encodes ES sort values into an opaque cursor |
| `ToCursorResult<T>(SearchResponse<T>, int, bool)` | Builds a `CursorPagedResult<T>` from a search response |

### Full Controller Example

```csharp
using Excalibur.Data.ElasticSearch;
using Excalibur.EventSourcing;

[HttpGet("orders")]
public async Task<CursorPagedResult<OrderSearchProjection>> SearchOrders(
    [FromQuery] string? query,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? cursor = null,
    CancellationToken cancellationToken = default)
{
    // 1. Decode cursor (null on first request)
    var searchAfter = ElasticSearchCursorHelper.DecodeCursor(cursor);

    // 2. Build search request with sort + search_after
    var request = new SearchRequestDescriptor<OrderSearchProjection>()
        .Index("orders")
        .Size(pageSize)
        .Sort(s => s.Field(f => f.CreatedAt, new FieldSort { Order = SortOrder.Desc }))
        .Sort(s => s.Field("_id", new FieldSort { Order = SortOrder.Asc }));

    if (searchAfter is not null)
    {
        request.SearchAfter(searchAfter);
    }

    if (!string.IsNullOrWhiteSpace(query))
    {
        request.Query(q => q.MultiMatch(m => m
            .Query(query)
            .Fields(new[] { "customerName", "status" })));
    }

    // 3. Execute and build result with next-page cursor
    var response = await client.SearchAsync(request, cancellationToken);
    return ElasticSearchCursorHelper.ToCursorResult(response, pageSize);
}
```

### Bidirectional Navigation

For previous-page or last-page requests, reverse the sort order in Elasticsearch and set `reverseItems: true` so items are returned in display order:

```csharp
// Previous page: reverse sort to find the preceding page, then flip back
var result = ElasticSearchCursorHelper.ToCursorResult(response, pageSize, reverseItems: true);
```

### Client Usage

```json
// First request
GET /api/orders?pageSize=20

// Response
{
  "items": [...],
  "pageSize": 20,
  "totalRecords": 1543,
  "totalPages": 78,
  "nextCursor": "WyIyMDI2LTA0LTIxVDEyOjAwOjAwWiIsIm9yZGVyLTEyMyJd",
  "hasMore": true
}

// Next page
GET /api/orders?pageSize=20&cursor=WyIyMDI2LTA0LTIxVDEyOjAwOjAwWiIsIm9yZGVyLTEyMyJd
```

---

## Best Practices

### Always Use a Tiebreaker Sort

When using cursor-based pagination, always include a unique field (like `_id` or a GUID) as the last sort criterion. Without it, items with identical sort values may be skipped or duplicated:

```csharp
// Good: deterministic ordering
.Sort(s => s.Field(f => f.CreatedAt, new FieldSort { Order = SortOrder.Desc }))
.Sort(s => s.Field("_id", new FieldSort { Order = SortOrder.Asc }))

// Bad: non-deterministic when CreatedAt values collide
.Sort(s => s.Field(f => f.CreatedAt, new FieldSort { Order = SortOrder.Desc }))
```

### Treat Cursors as Opaque

Never parse, construct, or modify cursor strings on the client side. The internal encoding (currently Base64url JSON) is an implementation detail that may change between framework versions.

### Handle Missing Cursors Gracefully

Both `CursorEncoder.Decode` and `ElasticSearchCursorHelper.DecodeCursor` return `null` for missing, empty, or invalid cursors — which means "start from the beginning." Your query logic should handle `null` naturally:

```csharp
var searchAfter = ElasticSearchCursorHelper.DecodeCursor(cursor);

if (searchAfter is not null)
{
    request.SearchAfter(searchAfter);
}
// If null, the query simply starts from the beginning — no special handling needed
```

### Prefer Cursor-Based for APIs

If you are building a public or partner API, prefer cursor-based pagination. It is more resilient to concurrent data changes and scales to any dataset size without performance degradation.

## See Also

- [Elasticsearch Provider](../data-providers/elasticsearch.md#cursor-based-pagination) — ES-specific cursor integration details
- [CQRS](../cqrs/index.md) — Using pagination with query handlers
- [Projections](../event-sourcing/projections.md) — Querying read models with pagination
