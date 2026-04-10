// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.DynamoDb;
using Excalibur.Data.DynamoDb.Caching;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ============================================================================
// DynamoDB Data Provider Sample
// ============================================================================
//
// Demonstrates ALL framework DynamoDB capabilities:
//   1. DI registration with AddDynamoDb (action-based and config-based)
//   2. CRUD operations via ICloudNativePersistenceProvider
//   3. Consistent reads configuration
//   4. DynamoDB Streams awareness (EnableStreams, StreamViewType)
//   5. DAX caching registration via AddDynamoDbDaxCaching
//   6. Health check registration
//   7. Connection options for local development (DynamoDB Local)
//
// Prerequisites:
//   - DynamoDB Local running on http://localhost:8000
//   - docker run -d --name dynamodb-local -p 8000:8000 amazon/dynamodb-local
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// 1. DI Registration -- AddDynamoDb with action-based configuration
// ---------------------------------------------------------------------------
// Option A: Configure via action delegate (shown here)
builder.Services.AddDynamoDb(options =>
{
    options.Name = "SampleProvider";
    options.DefaultTableName = "ExcaliburSample";
    options.DefaultPartitionKeyAttribute = "pk";
    options.DefaultSortKeyAttribute = "sk";

    // 3. Consistent reads -- all reads use strong consistency by default
    options.UseConsistentReads = true;

    // 4. DynamoDB Streams awareness -- configure for change data capture
    options.EnableStreams = true;
    options.StreamViewType = "NEW_AND_OLD_IMAGES"; // NEW_IMAGE, OLD_IMAGE, NEW_AND_OLD_IMAGES, KEYS_ONLY

    // 7. Connection options -- point to DynamoDB Local for development
    options.Connection.ServiceUrl = "http://localhost:8000";
    options.Connection.Region = "us-east-1";
    options.Connection.MaxRetryAttempts = 3;
    options.Connection.TimeoutInSeconds = 30;
});

// Option B: Configure via IConfiguration (bind from appsettings.json)
// builder.Services.AddDynamoDb(builder.Configuration.GetSection("DynamoDb"));

// Option C: Configure via named section
// builder.Services.AddDynamoDb(builder.Configuration, "DynamoDb");

// ---------------------------------------------------------------------------
// 5. DAX Caching -- register DynamoDB Accelerator caching layer
// ---------------------------------------------------------------------------
builder.Services.AddDynamoDbDaxCaching(options =>
{
    options.ClusterEndpoint = "dax://my-cluster.us-east-1.dax-clusters.amazonaws.com";
    options.CacheItemTtl = TimeSpan.FromMinutes(5);
    options.ReadConsistency = DaxReadConsistency.Eventual;
    options.ConnectionTimeout = TimeSpan.FromSeconds(5);
    options.RequestTimeout = TimeSpan.FromSeconds(10);
});

// ---------------------------------------------------------------------------
// 6. Health Check -- register DynamoDB health check endpoint
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .Add(new HealthCheckRegistration(
        name: "dynamodb",
        factory: sp => (IHealthCheck)sp.GetRequiredService<IPersistenceProviderHealth>(),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "aws", "dynamodb"]));

builder.Services.AddLogging(logging => logging.AddConsole());

var app = builder.Build();

Console.WriteLine("DynamoDB Data Provider Sample");
Console.WriteLine("=============================");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Resolve the persistence provider and initialize
// ---------------------------------------------------------------------------
var provider = app.Services.GetRequiredService<DynamoDbPersistenceProvider>();
var ct = CancellationToken.None;

Console.WriteLine("Initializing DynamoDB provider...");
try
{
    await provider.InitializeAsync(ct).ConfigureAwait(false);
    Console.WriteLine($"  Provider '{provider.Name}' initialized successfully.");
    Console.WriteLine($"  Document store type: {provider.DocumentStoreType}");
    Console.WriteLine($"  Cloud provider: {provider.CloudProvider}");
    Console.WriteLine($"  Supports multi-region writes: {provider.SupportsMultiRegionWrites}");
    Console.WriteLine($"  Supports change feed (Streams): {provider.SupportsChangeFeed}");
    Console.WriteLine($"  Connection: {provider.ConnectionString}");
}
catch (Exception ex)
{
    Console.WriteLine($"  [!] Could not connect to DynamoDB Local: {ex.Message}");
    Console.WriteLine("  [!] Ensure DynamoDB Local is running:");
    Console.WriteLine("      docker run -d --name dynamodb-local -p 8000:8000 amazon/dynamodb-local");
    Console.WriteLine();
    Console.WriteLine("Continuing with demonstration of API surface...");
    Console.WriteLine();
    DemonstrateApiSurface();
    return;
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// 2. CRUD Operations via the cloud-native persistence provider
// ---------------------------------------------------------------------------
var partitionKey = new PartitionKey("samples");

// --- CREATE ---
Console.WriteLine("1. Creating a document...");
var createResult = await provider.CreateAsync(
    new SampleProduct("prod-001", "Mechanical Keyboard", "Electronics", 149.99m, 50),
    partitionKey,
    ct).ConfigureAwait(false);
Console.WriteLine($"   Success: {createResult.Success}, Status: {createResult.StatusCode}, " +
                  $"Capacity: {createResult.RequestCharge} WCU");

// --- READ (with consistent read from options) ---
Console.WriteLine();
Console.WriteLine("2. Reading document (consistent read enabled via options)...");
var document = await provider.GetByIdAsync<SampleProduct>(
    "prod-001",
    partitionKey,
    consistencyOptions: null, // Uses UseConsistentReads=true from options
    ct).ConfigureAwait(false);
Console.WriteLine($"   Found: {document?.Name} - ${document?.Price}");

// --- READ (with explicit strong consistency) ---
Console.WriteLine();
Console.WriteLine("3. Reading document (explicit strong consistency)...");
var strongRead = await provider.GetByIdAsync<SampleProduct>(
    "prod-001",
    partitionKey,
    ConsistencyOptions.Strong,
    ct).ConfigureAwait(false);
Console.WriteLine($"   Found: {strongRead?.Name} - ${strongRead?.Price}");

// --- UPDATE ---
Console.WriteLine();
Console.WriteLine("4. Updating document...");
var updatedProduct = new SampleProduct("prod-001", "Mechanical Keyboard RGB", "Electronics", 169.99m, 45);
var updateResult = await provider.UpdateAsync(
    updatedProduct,
    partitionKey,
    etag: null,
    ct).ConfigureAwait(false);
Console.WriteLine($"   Success: {updateResult.Success}, Status: {updateResult.StatusCode}, " +
                  $"Capacity: {updateResult.RequestCharge} WCU");

// --- DELETE ---
Console.WriteLine();
Console.WriteLine("5. Deleting document...");
var deleteResult = await provider.DeleteAsync(
    "prod-001",
    partitionKey,
    etag: null,
    ct).ConfigureAwait(false);
Console.WriteLine($"   Success: {deleteResult.Success}, Status: {deleteResult.StatusCode}, " +
                  $"Capacity: {deleteResult.RequestCharge} WCU");

// --- VERIFY DELETE ---
Console.WriteLine();
Console.WriteLine("6. Verifying deletion...");
var deleted = await provider.GetByIdAsync<SampleProduct>(
    "prod-001",
    partitionKey,
    consistencyOptions: null,
    ct).ConfigureAwait(false);
Console.WriteLine($"   Document exists: {deleted is not null}");

// ---------------------------------------------------------------------------
// Provider info and statistics
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("7. Provider statistics...");
var stats = await provider.GetDocumentStoreStatisticsAsync(ct).ConfigureAwait(false);
foreach (var stat in stats)
{
    Console.WriteLine($"   {stat.Key}: {stat.Value}");
}

Console.WriteLine();
Console.WriteLine("8. Supported operations...");
foreach (var op in provider.GetSupportedOperationTypes())
{
    Console.WriteLine($"   - {op}");
}

// ---------------------------------------------------------------------------
// Health check
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("9. Running health check...");
var healthService = app.Services.GetRequiredService<HealthCheckService>();
var healthReport = await healthService.CheckHealthAsync(ct).ConfigureAwait(false);
var healthResult = healthReport.Entries.GetValueOrDefault("dynamodb");
Console.WriteLine($"   Status: {healthResult.Status}");
Console.WriteLine($"   Description: {healthResult.Description}");

// ---------------------------------------------------------------------------
// DAX cache provider
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("10. DAX cache provider...");
var daxProvider = app.Services.GetService<IDaxCacheProvider>();
Console.WriteLine($"    DAX cache registered: {daxProvider is not null}");

// ---------------------------------------------------------------------------
// GetService escape hatch for advanced sub-interfaces
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("11. GetService escape hatch...");
var queryOps = provider.GetService(typeof(ICloudNativePersistenceQueryOperations));
Console.WriteLine($"    Query operations available: {queryOps is not null}");
var batchOps = provider.GetService(typeof(ICloudNativePersistenceBatchOperations));
Console.WriteLine($"    Batch operations available: {batchOps is not null}");
var changeFeed = provider.GetService(typeof(ICloudNativePersistenceChangeFeed));
Console.WriteLine($"    Change feed (Streams) available: {changeFeed is not null}");

Console.WriteLine();
Console.WriteLine("Done! All DynamoDB capabilities demonstrated.");

// ---------------------------------------------------------------------------
// Cleanup
// ---------------------------------------------------------------------------
await provider.DisposeAsync().ConfigureAwait(false);
return;

// ============================================================================
// Fallback: demonstrate API surface when DynamoDB Local is not available
// ============================================================================
static void DemonstrateApiSurface()
{
    Console.WriteLine("DynamoDB API Surface Overview");
    Console.WriteLine("-----------------------------");
    Console.WriteLine();
    Console.WriteLine("DI Registration:");
    Console.WriteLine("  services.AddDynamoDb(options => { ... })           // Action-based");
    Console.WriteLine("  services.AddDynamoDb(configuration)                // IConfiguration-based");
    Console.WriteLine("  services.AddDynamoDb(configuration, \"DynamoDb\")    // Named section");
    Console.WriteLine("  services.AddDynamoDbWithClient(options => { ... }) // Existing IAmazonDynamoDB");
    Console.WriteLine();
    Console.WriteLine("DAX Caching:");
    Console.WriteLine("  services.AddDynamoDbDaxCaching(options => { ... }) // DAX Accelerator cache");
    Console.WriteLine();
    Console.WriteLine("CRUD Operations (ICloudNativePersistenceProvider):");
    Console.WriteLine("  GetByIdAsync<T>(id, partitionKey, consistency, ct)");
    Console.WriteLine("  CreateAsync<T>(document, partitionKey, ct)");
    Console.WriteLine("  UpdateAsync<T>(document, partitionKey, etag, ct)");
    Console.WriteLine("  DeleteAsync(id, partitionKey, etag, ct)");
    Console.WriteLine("  QueryAsync<T>(query, partitionKey, params, consistency, ct)");
    Console.WriteLine("  ExecuteBatchAsync(partitionKey, operations, ct)");
    Console.WriteLine();
    Console.WriteLine("Streams (Change Feed):");
    Console.WriteLine("  CreateChangeFeedSubscriptionAsync<T>(container, options, ct)");
    Console.WriteLine();
    Console.WriteLine("Health & Diagnostics:");
    Console.WriteLine("  TestConnectionAsync(ct)");
    Console.WriteLine("  GetDocumentStoreStatisticsAsync(ct)");
    Console.WriteLine("  GetMetricsAsync(ct)");
    Console.WriteLine("  GetConnectionPoolStatsAsync(ct)");
    Console.WriteLine();
    Console.WriteLine("DynamoDbOptions Properties:");
    Console.WriteLine("  Name, DefaultTableName, DefaultPartitionKeyAttribute, DefaultSortKeyAttribute");
    Console.WriteLine("  UseConsistentReads, EnableStreams, StreamViewType");
    Console.WriteLine("  Connection.ServiceUrl, Connection.Region, Connection.AccessKey, Connection.SecretKey");
    Console.WriteLine("  Connection.MaxRetryAttempts, Connection.TimeoutInSeconds");
    Console.WriteLine("  Connection.ReadCapacityUnits, Connection.WriteCapacityUnits");
}

// ============================================================================
// Sample document type
// ============================================================================

/// <summary>
/// A sample product document for demonstration purposes.
/// </summary>
internal sealed record SampleProduct(
    string Id,
    string Name,
    string Category,
    decimal Price,
    int StockQuantity);
