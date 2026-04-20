# Elasticsearch Querying Sample

Demonstrates the Elasticsearch Query DSL through Excalibur's `ElasticRepositoryBase<T>` and `IElasticRepositoryBaseQuery<T>`.

## Prerequisites

- .NET 9.0 SDK
- Elasticsearch 8.x running locally (default: `http://localhost:9200`)

Start Elasticsearch quickly with Docker:

```bash
docker run -d --name elasticsearch \
  -p 9200:9200 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  docker.elastic.co/elasticsearch/elasticsearch:8.15.0
```

## Query Types Demonstrated

| # | Query Type | Description | Use Case |
|---|-----------|-------------|----------|
| 1 | **Term** | Exact match on a keyword field | Filter by category, status, or ID |
| 2 | **Match** | Full-text search on analyzed text fields | Search descriptions, names with tokenization |
| 3 | **Bool** | Combine `must` + `filter` clauses | Category AND price range together |
| 4 | **Range** | Numeric or date range filtering | Products with rating >= 4.0 |
| 5 | **Wildcard** | Pattern matching with `*` and `?` | Names starting with "USB" |
| 6 | **Sorting** | Order results by field value | Sort by price descending |
| 7 | **Aggregations** | Server-side analytics | Average price per category |

## Project Structure

```
ElasticSearch-Querying/
  Domain/
    Product.cs              # Product document model
  Repositories/
    IProductRepository.cs   # Repository interface (CRUD + Query)
    ProductRepository.cs    # ElasticRepositoryBase<Product> with index mappings
  Program.cs                # Console app demonstrating all query types
  appsettings.json          # Elasticsearch connection configuration
```

## Running

```bash
dotnet run
```

## Key Patterns

- **ElasticRepositoryBase<T>** provides `SearchAsync(SearchRequestDescriptor<T>, CancellationToken)` for full query DSL access
- **IElasticRepositoryBaseQuery<T>** is the ISP sub-interface for search-only consumers
- **IElasticRepositoryBase<T>** handles CRUD (AddOrUpdate, Remove, GetById)
- Index mappings are defined in `InitializeIndexAsync` using `CreateIndexRequest` with explicit `TypeMapping`
- Use `keyword` fields for exact match (term, wildcard, aggregations) and `text` fields for full-text search (match)

## Field Mapping Reference

| Field | Elasticsearch Type | Query Types |
|-------|-------------------|-------------|
| `name` | `text` + `keyword` sub-field | Match (text), Wildcard (keyword) |
| `description` | `text` | Match |
| `category` | `keyword` | Term, Aggregations |
| `price` | `float` | Range, Sort |
| `stockQuantity` | `integer` | Range |
| `rating` | `float` | Range |
| `tags` | `keyword` | Term |
| `createdAt` | `date` | Range |
