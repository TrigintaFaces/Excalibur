// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.CosmosDb;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

// ============================================================================
// Azure Cosmos DB Sample
// ============================================================================
//
// Demonstrates ALL Excalibur CosmosDB capabilities:
//   1. DI registration with AddExcaliburCosmosDb(Action<ICosmosDbDataBuilder>)
//   2. Connection testing via TestConnectionAsync()
//   3. CRUD operations: Create, GetById, Query, Delete
//   4. Transactional batch execution
//   5. Collection info and document store statistics
//   6. Health check registration and verification
//   7. Multi-region configuration options
//
// Prerequisites:
//   - Azure Cosmos DB Emulator running on https://localhost:8081
//   - Or Docker:
//       docker run -d --name cosmosdb -p 8081:8081 -p 10250-10255:10250-10255 \
//         mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// 1. DI Registration -- AddExcaliburCosmosDb with fluent builder
// ---------------------------------------------------------------------------
builder.Services.AddExcaliburCosmosDb(cosmos =>
{
    // Connection: use emulator defaults (from appsettings.json or inline)
    cosmos.Endpoint(
        "https://localhost:8081",
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==")
        .DatabaseName("ExcaliburSample")
        .ContainerName("Items");
});

// Advanced options via Configure<CosmosDbOptions> post-configure
builder.Services.Configure<CosmosDbOptions>(options =>
{
    options.Name = "SampleProvider";
    options.DefaultPartitionKeyPath = "/category";

    // 7. Multi-region awareness -- configure preferred regions for geo-redundancy
    //    (The emulator uses a single region; in production, list your Azure regions.)
    options.Client.PreferredRegions = ["West US", "East US"];
    options.Client.UseDirectMode = true;
    options.Client.Resilience.MaxRetryAttempts = 5;
    options.Client.Resilience.MaxRetryWaitTimeInSeconds = 30;
    options.Client.Resilience.RequestTimeoutInSeconds = 15;

    options.AllowBulkExecution = false;
    options.MaxConnectionsPerEndpoint = 50;
    options.EnableTcpConnectionEndpointRediscovery = true;
});

// ---------------------------------------------------------------------------
// 6. Health Check registration
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCosmosDb(name: "cosmosdb-sample", tags: ["ready", "cosmosdb"]);

var app = builder.Build();

Console.WriteLine("Azure Cosmos DB -- Excalibur Data Provider Sample");
Console.WriteLine("==================================================");
Console.WriteLine();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var ct = cts.Token;

var provider = app.Services.GetRequiredService<CosmosDbPersistenceProvider>();

// ---------------------------------------------------------------------------
// 2. Connection Test
// ---------------------------------------------------------------------------
Console.WriteLine("1. Testing connection...");
var connected = await provider.TestConnectionAsync(ct).ConfigureAwait(false);
Console.WriteLine($"   Connected: {connected}");

if (!connected)
{
    Console.WriteLine();
    Console.WriteLine("   Could not connect to Cosmos DB. Ensure the emulator is running:");
    Console.WriteLine("     docker run -d --name cosmosdb -p 8081:8081 -p 10250-10255:10250-10255 \\");
    Console.WriteLine("       mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest");
    return;
}

Console.WriteLine($"   Provider available: {provider.IsAvailable}");
Console.WriteLine($"   Supports change feed: {provider.SupportsChangeFeed}");
Console.WriteLine($"   Supports multi-region writes: {provider.SupportsMultiRegionWrites}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 3. CRUD Operations
// ---------------------------------------------------------------------------

// -- CREATE --
Console.WriteLine("2. Creating documents...");
var partitionKey = new PartitionKey("electronics");

var product1 = new Product("prod-001", "Mechanical Keyboard", "electronics", 149.99m, 50);
var createResult1 = await provider.CreateAsync(product1, partitionKey, ct).ConfigureAwait(false);
Console.WriteLine($"   Created '{product1.Name}': success={createResult1.Success}, RU={createResult1.RequestCharge:F2}");

var product2 = new Product("prod-002", "Wireless Mouse", "electronics", 79.99m, 120);
var createResult2 = await provider.CreateAsync(product2, partitionKey, ct).ConfigureAwait(false);
Console.WriteLine($"   Created '{product2.Name}': success={createResult2.Success}, RU={createResult2.RequestCharge:F2}");

var product3 = new Product("prod-003", "USB-C Hub", "electronics", 49.99m, 200);
var createResult3 = await provider.CreateAsync(product3, partitionKey, ct).ConfigureAwait(false);
Console.WriteLine($"   Created '{product3.Name}': success={createResult3.Success}, RU={createResult3.RequestCharge:F2}");
Console.WriteLine();

// -- GET BY ID --
Console.WriteLine("3. Reading document by ID...");
var fetched = await provider.GetByIdAsync<Product>("prod-001", partitionKey, null, ct).ConfigureAwait(false);
if (fetched != null)
{
    Console.WriteLine($"   Found: {fetched.Name}, Price=${fetched.Price}, Stock={fetched.Stock}");
}

Console.WriteLine();

// -- QUERY --
Console.WriteLine("4. Querying documents (price > $50)...");
var queryResult = await provider.QueryAsync<Product>(
    "SELECT * FROM c WHERE c.price > @minPrice",
    partitionKey,
    new Dictionary<string, object> { ["minPrice"] = 50.0 },
    null,
    ct).ConfigureAwait(false);

Console.WriteLine($"   Found {queryResult.Documents.Count} document(s), RU={queryResult.RequestCharge:F2}");
foreach (var doc in queryResult.Documents)
{
    Console.WriteLine($"   - {doc.Name}: ${doc.Price}");
}

Console.WriteLine();

// -- DELETE --
Console.WriteLine("5. Deleting document 'prod-003'...");
var deleteResult = await provider.DeleteAsync("prod-003", partitionKey, null, ct).ConfigureAwait(false);
Console.WriteLine($"   Deleted: success={deleteResult.Success}, RU={deleteResult.RequestCharge:F2}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 4. Batch Execution -- transactional batch within a single partition
// ---------------------------------------------------------------------------
Console.WriteLine("6. Executing transactional batch...");
var batchOps = new ICloudBatchOperation[]
{
    new CloudBatchCreateOperation("prod-010", new Product("prod-010", "Monitor Stand", "electronics", 39.99m, 75)),
    new CloudBatchCreateOperation("prod-011", new Product("prod-011", "Desk Lamp", "electronics", 29.99m, 150)),
    new CloudBatchDeleteOperation("prod-002"),
};

var batchResult = await provider.ExecuteBatchAsync(partitionKey, batchOps, ct).ConfigureAwait(false);
Console.WriteLine($"   Batch success: {batchResult.Success}, RU={batchResult.RequestCharge:F2}");
Console.WriteLine($"   Operations completed: {batchResult.OperationResults.Count}");
foreach (var opResult in batchResult.OperationResults)
{
    Console.WriteLine($"   - Status: {opResult.StatusCode}, Success: {opResult.Success}");
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// 5. Collection Info and Document Store Statistics
// ---------------------------------------------------------------------------
Console.WriteLine("7. Retrieving collection info for 'Items'...");
var collectionInfo = await provider.GetCollectionInfoAsync("Items", ct).ConfigureAwait(false);
foreach (var kvp in collectionInfo)
{
    Console.WriteLine($"   {kvp.Key}: {kvp.Value}");
}

Console.WriteLine();

Console.WriteLine("8. Retrieving document store statistics...");
var stats = await provider.GetDocumentStoreStatisticsAsync(ct).ConfigureAwait(false);
foreach (var kvp in stats)
{
    Console.WriteLine($"   {kvp.Key}: {kvp.Value}");
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// 6. Health Check verification
// ---------------------------------------------------------------------------
Console.WriteLine("9. Running health check...");
var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();
var healthReport = await healthCheckService.CheckHealthAsync(ct).ConfigureAwait(false);
Console.WriteLine($"   Overall status: {healthReport.Status}");
foreach (var entry in healthReport.Entries)
{
    Console.WriteLine($"   [{entry.Key}] Status: {entry.Value.Status}, Description: {entry.Value.Description}");
    if (entry.Value.Data.Count > 0)
    {
        foreach (var data in entry.Value.Data)
        {
            Console.WriteLine($"      {data.Key}: {data.Value}");
        }
    }
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// Additional: Provider capabilities via GetService
// ---------------------------------------------------------------------------
Console.WriteLine("10. Provider capabilities (via GetService)...");
Console.WriteLine($"    Supported operations: {string.Join(", ", provider.GetSupportedOperationTypes())}");
Console.WriteLine($"    Document store type: {provider.DocumentStoreType}");
Console.WriteLine($"    Cloud provider: {provider.CloudProvider}");
Console.WriteLine($"    Provider type: {provider.ProviderType}");

var poolStats = await provider.GetConnectionPoolStatsAsync(ct).ConfigureAwait(false);
if (poolStats != null)
{
    Console.WriteLine("    Connection pool:");
    foreach (var kvp in poolStats)
    {
        Console.WriteLine($"      {kvp.Key}: {kvp.Value}");
    }
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// Cleanup
// ---------------------------------------------------------------------------
Console.WriteLine("11. Cleaning up...");
_ = await provider.DeleteAsync("prod-001", partitionKey, null, ct).ConfigureAwait(false);
_ = await provider.DeleteAsync("prod-010", partitionKey, null, ct).ConfigureAwait(false);
_ = await provider.DeleteAsync("prod-011", partitionKey, null, ct).ConfigureAwait(false);
Console.WriteLine("    Cleanup complete.");
Console.WriteLine();

await provider.DisposeAsync().ConfigureAwait(false);

Console.WriteLine("Done. All Cosmos DB capabilities demonstrated successfully.");

// ============================================================================
// Document model
// ============================================================================

/// <summary>
/// Sample product document for Cosmos DB.
/// </summary>
/// <remarks>
/// The <c>id</c> property (lowercase) is required by Cosmos DB convention.
/// The <c>category</c> property matches the partition key path "/category".
/// </remarks>
sealed record Product(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("stock")] int Stock);
