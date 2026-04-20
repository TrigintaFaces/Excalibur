// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// Elasticsearch Querying Sample
// ============================================================================
// Demonstrates the Elasticsearch Query DSL through Excalibur's
// ElasticRepositoryBase<T>:
//   1. Term query     - exact keyword match
//   2. Match query    - full-text search
//   3. Bool query     - combining must + filter clauses
//   4. Range query    - numeric/date range filtering
//   5. Wildcard query - pattern matching
//   6. Sorting        - ordering results by field
//   7. Aggregations   - server-side analytics (avg price per category)
// ============================================================================

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Excalibur.Data.ElasticSearch;

using ElasticSearchQuerying.Domain;
using ElasticSearchQuerying.Repositories;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ---------------------------------------------------------------------------
// 1. Build host and configure services
// ---------------------------------------------------------------------------
var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: true);

// Register Elasticsearch client
var nodeUri = builder.Configuration["Elasticsearch:NodeUris:0"] ?? "http://localhost:9200";
var settings = new ElasticsearchClientSettings(new Uri(nodeUri))
    .DefaultIndex("products-querying");
builder.Services.AddSingleton(new ElasticsearchClient(settings));

// Register repository
builder.Services.AddSingleton<ProductRepository>();
builder.Services.AddSingleton<IProductRepository>(sp => sp.GetRequiredService<ProductRepository>());
builder.Services.AddSingleton<IInitializeElasticIndex>(sp => sp.GetRequiredService<ProductRepository>());

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var repo = host.Services.GetRequiredService<ProductRepository>();
var ct = CancellationToken.None;

// ---------------------------------------------------------------------------
// 2. Initialize index and seed data
// ---------------------------------------------------------------------------
logger.LogInformation("Initializing Elasticsearch index...");
await repo.InitializeIndexAsync(ct).ConfigureAwait(false);
await repo.InitializeAsync(ct).ConfigureAwait(false);

logger.LogInformation("Seeding product data...");
var products = CreateSeedProducts();
foreach (var product in products)
{
    await repo.AddOrUpdateAsync(product.Id, product, ct).ConfigureAwait(false);
}

// Allow Elasticsearch near-real-time refresh so seeded documents become searchable
await Task.Delay(1000, ct).ConfigureAwait(false);
logger.LogInformation("Seeded {Count} products.\n", products.Count);

// ---------------------------------------------------------------------------
// 3. Term Query -- exact match on keyword field
// ---------------------------------------------------------------------------
PrintHeader("Term Query: category = 'Electronics'");

var termRequest = new SearchRequestDescriptor<Product>()
    .Index("products-querying")
    .Query(q => q
        .Term(t => t
            .Field("category")
            .Value("Electronics")));

var termResults = await repo.SearchAsync(termRequest, ct).ConfigureAwait(false);
PrintProducts(termResults);

// ---------------------------------------------------------------------------
// 4. Match Query -- full-text search on analyzed field
// ---------------------------------------------------------------------------
PrintHeader("Match Query: description contains 'ergonomic'");

var matchRequest = new SearchRequestDescriptor<Product>()
    .Index("products-querying")
    .Query(q => q
        .Match(m => m
            .Field("description")
            .Query("ergonomic")));

var matchResults = await repo.SearchAsync(matchRequest, ct).ConfigureAwait(false);
PrintProducts(matchResults);

// ---------------------------------------------------------------------------
// 5. Bool Query -- combine must + filter (category + price range)
// ---------------------------------------------------------------------------
PrintHeader("Bool Query: category='Electronics' AND price between 50-200");

var boolRequest = new SearchRequestDescriptor<Product>()
    .Index("products-querying")
    .Query(q => q
        .Bool(b => b
            .Must(m => m
                .Term(t => t
                    .Field("category")
                    .Value("Electronics")))
            .Filter(f => f
                .Range(r => r
                    .NumberRange(nr => nr
                        .Field("price")
                        .Gte(50)
                        .Lte(200))))));

var boolResults = await repo.SearchAsync(boolRequest, ct).ConfigureAwait(false);
PrintProducts(boolResults);

// ---------------------------------------------------------------------------
// 6. Range Query -- products with rating >= 4.0
// ---------------------------------------------------------------------------
PrintHeader("Range Query: rating >= 4.0");

var rangeRequest = new SearchRequestDescriptor<Product>()
    .Index("products-querying")
    .Query(q => q
        .Range(r => r
            .NumberRange(nr => nr
                .Field("rating")
                .Gte(4.0))));

var rangeResults = await repo.SearchAsync(rangeRequest, ct).ConfigureAwait(false);
PrintProducts(rangeResults);

// ---------------------------------------------------------------------------
// 7. Wildcard Query -- names matching "USB*"
// ---------------------------------------------------------------------------
PrintHeader("Wildcard Query: name.keyword matches 'USB*'");

var wildcardRequest = new SearchRequestDescriptor<Product>()
    .Index("products-querying")
    .Query(q => q
        .Wildcard(w => w
            .Field("name.keyword")
            .Value("USB*")));

var wildcardResults = await repo.SearchAsync(wildcardRequest, ct).ConfigureAwait(false);
PrintProducts(wildcardResults);

// ---------------------------------------------------------------------------
// 8. Sorting -- all products sorted by price descending
// ---------------------------------------------------------------------------
PrintHeader("Sorting: all products by price descending");

var sortRequest = new SearchRequestDescriptor<Product>()
    .Index("products-querying")
    .Size(10)
    .Query(q => q.MatchAll(new MatchAllQuery()))
    .Sort(so => so
        .Field("price", f => f.Order(SortOrder.Desc)));

var sortResults = await repo.SearchAsync(sortRequest, ct).ConfigureAwait(false);
PrintProducts(sortResults);

// ---------------------------------------------------------------------------
// 9. Aggregations -- average price per category
// ---------------------------------------------------------------------------
PrintHeader("Aggregations: average price per category");

var aggRequest = new SearchRequestDescriptor<Product>()
    .Index("products-querying")
    .Size(0) // Only aggregations, no documents
    .Query(q => q.MatchAll(new MatchAllQuery()))
    .Aggregations(aggs => aggs
        .Add("by_category", agg => agg
            .Terms(t => t
                .Field("category"))
            .Aggregations(subAggs => subAggs
                .Add("avg_price", subAgg => subAgg
                    .Avg(avg => avg
                        .Field("price"))))));

var aggResults = await repo.SearchAsync(aggRequest, ct).ConfigureAwait(false);
PrintAggregations(aggResults);

logger.LogInformation("\nAll queries completed successfully.");

// ============================================================================
// Helper Methods
// ============================================================================

static List<Product> CreateSeedProducts() =>
[
    new Product
    {
        Id = "prod-001", Name = "Wireless Mouse", Description = "Ergonomic wireless mouse with adjustable DPI",
        Category = "Electronics", Price = 29.99m, StockQuantity = 150, Rating = 4.5,
        Tags = ["wireless", "mouse", "ergonomic"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-30)
    },
    new Product
    {
        Id = "prod-002", Name = "Mechanical Keyboard", Description = "RGB mechanical keyboard with Cherry MX switches",
        Category = "Electronics", Price = 129.99m, StockQuantity = 75, Rating = 4.8,
        Tags = ["keyboard", "mechanical", "rgb"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-25)
    },
    new Product
    {
        Id = "prod-003", Name = "USB Hub 7-Port", Description = "USB 3.0 hub with 7 ports and power adapter",
        Category = "Electronics", Price = 34.99m, StockQuantity = 200, Rating = 4.2,
        Tags = ["usb", "hub", "accessories"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-20)
    },
    new Product
    {
        Id = "prod-004", Name = "Standing Desk", Description = "Ergonomic height-adjustable standing desk",
        Category = "Furniture", Price = 499.99m, StockQuantity = 30, Rating = 4.7,
        Tags = ["desk", "ergonomic", "standing"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-15)
    },
    new Product
    {
        Id = "prod-005", Name = "Monitor Arm", Description = "Dual monitor arm with cable management",
        Category = "Furniture", Price = 89.99m, StockQuantity = 60, Rating = 4.3,
        Tags = ["monitor", "arm", "ergonomic"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-12)
    },
    new Product
    {
        Id = "prod-006", Name = "USB-C Docking Station", Description = "Universal USB-C dock with dual HDMI output",
        Category = "Electronics", Price = 179.99m, StockQuantity = 45, Rating = 4.1,
        Tags = ["usb-c", "dock", "hdmi"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
    },
    new Product
    {
        Id = "prod-007", Name = "Ergonomic Chair", Description = "Ergonomic office chair with lumbar support and mesh back",
        Category = "Furniture", Price = 349.99m, StockQuantity = 25, Rating = 4.6,
        Tags = ["chair", "ergonomic", "office"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-8)
    },
    new Product
    {
        Id = "prod-008", Name = "Webcam HD", Description = "1080p HD webcam with autofocus and noise-cancelling mic",
        Category = "Electronics", Price = 69.99m, StockQuantity = 110, Rating = 3.9,
        Tags = ["webcam", "hd", "video"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-5)
    },
    new Product
    {
        Id = "prod-009", Name = "Desk Lamp LED", Description = "Adjustable LED desk lamp with color temperature control",
        Category = "Lighting", Price = 44.99m, StockQuantity = 90, Rating = 4.4,
        Tags = ["lamp", "led", "desk"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
    },
    new Product
    {
        Id = "prod-010", Name = "Cable Management Kit", Description = "Complete cable management solution with clips and sleeves",
        Category = "Accessories", Price = 19.99m, StockQuantity = 300, Rating = 4.0,
        Tags = ["cables", "management", "organization"], CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
    }
];

static void PrintHeader(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 60));
    Console.WriteLine($"  {title}");
    Console.WriteLine(new string('=', 60));
}

static void PrintProducts(SearchResponse<Product> response)
{
    Console.WriteLine($"  Hits: {response.Documents.Count}");
    Console.WriteLine();
    foreach (var doc in response.Documents)
    {
        Console.WriteLine($"  [{doc.Id}] {doc.Name,-30} ${doc.Price,8:F2}  Rating: {doc.Rating:F1}  Category: {doc.Category}");
    }

    if (response.Documents.Count == 0)
    {
        Console.WriteLine("  (no results)");
    }
}

static void PrintAggregations(SearchResponse<Product> response)
{
    if (response.Aggregations is null)
    {
        Console.WriteLine("  (no aggregations returned)");
        return;
    }

    var categoryBuckets = response.Aggregations.GetStringTerms("by_category");
    if (categoryBuckets?.Buckets is not null)
    {
        Console.WriteLine("  {0,-20} {1,12}  {2,6}", "Category", "Avg Price", "Count");
        Console.WriteLine("  {0,-20} {1,12}  {2,6}", new string('-', 20), new string('-', 12), new string('-', 6));

        foreach (var bucket in categoryBuckets.Buckets)
        {
            var avgPrice = bucket.Aggregations.GetAverage("avg_price");
            Console.WriteLine($"  {bucket.Key.Value,-20} ${avgPrice?.Value ?? 0,11:F2}  {bucket.DocCount,6}");
        }
    }
}
