// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.MongoDB;
using Excalibur.Data.MongoDB.Aggregation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

// ============================================================================
// MongoDB Data Provider Sample
// ============================================================================
//
// Demonstrates ALL Excalibur.Data.MongoDB capabilities:
//   1. DI registration via AddExcaliburMongoDb
//   2. CRUD operations via MongoDbPersistenceProvider
//   3. Aggregation pipeline with MongoAggregationBuilder
//   4. Transaction support via ITransactionScope
//   5. Connection pooling configuration
//   6. Health check via IPersistenceProviderHealth
//
// Prerequisites:
//   - MongoDB running on localhost:27017
//   - docker run -d --name mongo -p 27017:27017 mongo:7
//
// For transaction support (replica set required):
//   - docker run -d --name mongo -p 27017:27017 mongo:7 --replSet rs0
//   - docker exec mongo mongosh --eval "rs.initiate()"
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// ── 1. DI Registration ─────────────────────────────────────────────────────
// Register MongoDB services via the builder API.
// ConnectionString and DatabaseName are configured through fluent methods.
builder.Services.AddExcaliburMongoDb(mongo =>
{
    mongo.ConnectionString(builder.Configuration["MongoDB:ConnectionString"]
                           ?? "mongodb://localhost:27017");
    mongo.DatabaseName(builder.Configuration["MongoDB:DatabaseName"]
                       ?? "ExcaliburSample");
});

var app = builder.Build();

Console.WriteLine("Excalibur.Data.MongoDB Sample");
Console.WriteLine("=============================");
Console.WriteLine();

// Resolve the provider (registered as keyed "mongodb" and "default")
var provider = app.Services.GetRequiredKeyedService<IPersistenceProvider>("mongodb")
    as MongoDbPersistenceProvider
    ?? throw new InvalidOperationException("MongoDbPersistenceProvider not registered.");

var ct = CancellationToken.None;

// ── 6. Health Check ─────────────────────────────────────────────────────────
Console.WriteLine("1. Health Check (TestConnectionAsync)");
Console.WriteLine("-------------------------------------");
var healthy = await provider.TestConnectionAsync(ct).ConfigureAwait(false);
Console.WriteLine($"   Connection healthy: {healthy}");

if (!healthy)
{
    Console.WriteLine();
    Console.WriteLine("   MongoDB is not reachable. Please start it with:");
    Console.WriteLine("     docker run -d --name mongo -p 27017:27017 mongo:7");
    Console.WriteLine();
    Console.WriteLine("   Exiting.");
    return;
}

// Also demonstrate IPersistenceProviderHealth via GetService
var healthService = provider.GetService(typeof(IPersistenceProviderHealth)) as IPersistenceProviderHealth;
if (healthService is not null)
{
    var poolStats = await healthService.GetConnectionPoolStatsAsync(ct).ConfigureAwait(false);
    Console.WriteLine("   Connection pool stats:");
    if (poolStats is not null)
    {
        foreach (var kvp in poolStats)
        {
            Console.WriteLine($"     {kvp.Key}: {kvp.Value}");
        }
    }
}

Console.WriteLine();

// ── 2. CRUD Operations ─────────────────────────────────────────────────────
Console.WriteLine("2. CRUD Operations (direct MongoDB driver via provider)");
Console.WriteLine("-------------------------------------------------------");

var collection = provider.GetCollection<BsonDocument>("products");

// Insert
Console.WriteLine("   Inserting documents...");
var products = new List<BsonDocument>
{
    new()
    {
        { "name", "Mechanical Keyboard" },
        { "category", "Electronics" },
        { "price", 149.99 },
        { "stock", 50 },
        { "tags", new BsonArray(new[] { "peripherals", "gaming" }) },
    },
    new()
    {
        { "name", "Standing Desk" },
        { "category", "Furniture" },
        { "price", 599.00 },
        { "stock", 15 },
        { "tags", new BsonArray(new[] { "office", "ergonomic" }) },
    },
    new()
    {
        { "name", "USB-C Hub" },
        { "category", "Electronics" },
        { "price", 49.99 },
        { "stock", 200 },
        { "tags", new BsonArray(new[] { "peripherals", "connectivity" }) },
    },
    new()
    {
        { "name", "Monitor Light Bar" },
        { "category", "Electronics" },
        { "price", 89.99 },
        { "stock", 75 },
        { "tags", new BsonArray(new[] { "lighting", "office" }) },
    },
    new()
    {
        { "name", "Ergonomic Chair" },
        { "category", "Furniture" },
        { "price", 449.00 },
        { "stock", 20 },
        { "tags", new BsonArray(new[] { "office", "ergonomic" }) },
    },
};

// Drop and recreate to ensure clean state
await collection.Database.DropCollectionAsync("products", ct).ConfigureAwait(false);
await collection.InsertManyAsync(products, cancellationToken: ct).ConfigureAwait(false);
Console.WriteLine($"   Inserted {products.Count} products.");

// Read
Console.WriteLine();
Console.WriteLine("   Finding electronics products...");
var filter = Builders<BsonDocument>.Filter.Eq("category", "Electronics");
var electronics = await collection.Find(filter).ToListAsync(ct).ConfigureAwait(false);
foreach (var doc in electronics)
{
    Console.WriteLine($"     - {doc["name"]}: ${doc["price"]}");
}

// Update
Console.WriteLine();
Console.WriteLine("   Updating keyboard price...");
var updateFilter = Builders<BsonDocument>.Filter.Eq("name", "Mechanical Keyboard");
var update = Builders<BsonDocument>.Update.Set("price", 129.99);
var updateResult = await collection.UpdateOneAsync(updateFilter, update, cancellationToken: ct).ConfigureAwait(false);
Console.WriteLine($"   Modified count: {updateResult.ModifiedCount}");

// Delete
Console.WriteLine();
Console.WriteLine("   Deleting 'Monitor Light Bar'...");
var deleteFilter = Builders<BsonDocument>.Filter.Eq("name", "Monitor Light Bar");
var deleteResult = await collection.DeleteOneAsync(deleteFilter, ct).ConfigureAwait(false);
Console.WriteLine($"   Deleted count: {deleteResult.DeletedCount}");

// Verify remaining count
var remaining = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: ct).ConfigureAwait(false);
Console.WriteLine($"   Remaining products: {remaining}");

Console.WriteLine();

// ── 3. Aggregation Pipeline ────────────────────────────────────────────────
Console.WriteLine("3. Aggregation Pipeline (MongoAggregationBuilder)");
Console.WriteLine("-------------------------------------------------");

// Re-insert to have consistent data for aggregation
await collection.Database.DropCollectionAsync("products", ct).ConfigureAwait(false);
await collection.InsertManyAsync(products, cancellationToken: ct).ConfigureAwait(false);

// Build an aggregation pipeline: group products by category, compute
// the average price and total stock per category, then sort by avg price.
Console.WriteLine("   Pipeline: $match -> $group -> $sort -> $limit");
Console.WriteLine();

var aggregationOptions = new MongoAggregationOptions
{
    AllowDiskUse = true,
    MaxTime = TimeSpan.FromSeconds(15),
    BatchSize = 500,
};

var pipeline = new MongoAggregationBuilder<BsonDocument>(collection, aggregationOptions)
    .Match(new BsonDocument("stock", new BsonDocument("$gt", 0)))
    .Group(new BsonDocument
    {
        { "_id", "$category" },
        { "avgPrice", new BsonDocument("$avg", "$price") },
        { "totalStock", new BsonDocument("$sum", "$stock") },
        { "count", new BsonDocument("$sum", 1) },
    })
    .Sort(new BsonDocument("avgPrice", -1))
    .Limit(10)
    .Build();

// Inspect the built stages
Console.WriteLine("   Pipeline stages:");
foreach (var stage in pipeline.GetStages())
{
    Console.WriteLine($"     {stage}");
}

Console.WriteLine();

// Execute the aggregation
var aggResults = await pipeline.ExecuteAsync<BsonDocument>(ct).ConfigureAwait(false);
Console.WriteLine("   Results (category summaries):");
foreach (var result in aggResults)
{
    Console.WriteLine($"     Category: {result["_id"],-15} " +
                      $"Avg Price: ${result["avgPrice"].ToDouble():F2,-10} " +
                      $"Total Stock: {result["totalStock"],-6} " +
                      $"Count: {result["count"]}");
}

Console.WriteLine();

// ── 4. Transaction Support ─────────────────────────────────────────────────
Console.WriteLine("4. Transaction Support (ITransactionScope)");
Console.WriteLine("------------------------------------------");

// Transaction support via IPersistenceProviderTransaction
var txService = provider.GetService(typeof(IPersistenceProviderTransaction)) as IPersistenceProviderTransaction;
if (txService is not null)
{
    Console.WriteLine("   IPersistenceProviderTransaction is available.");
    Console.WriteLine("   Creating a transaction scope...");

    await using var scope = txService.CreateTransactionScope();
    Console.WriteLine($"   Transaction ID: {scope.TransactionId}");
    Console.WriteLine($"   Status: {scope.Status}");
    Console.WriteLine($"   Isolation Level: {scope.IsolationLevel}");

    // NOTE: Actual commit/rollback requires a MongoDB replica set.
    // In a single-node Docker setup without --replSet, transactions
    // are not supported. The scope is still created for demonstration.
    Console.WriteLine();
    Console.WriteLine("   To use transactions, start MongoDB as a replica set:");
    Console.WriteLine("     docker run -d --name mongo -p 27017:27017 mongo:7 --replSet rs0");
    Console.WriteLine("     docker exec mongo mongosh --eval \"rs.initiate()\"");
}
else
{
    Console.WriteLine("   Transaction support not available (provider may not be fully initialized).");
}

Console.WriteLine();

// ── Database Statistics ─────────────────────────────────────────────────────
Console.WriteLine("5. Database & Collection Statistics");
Console.WriteLine("-----------------------------------");

var dbStats = await provider.GetDocumentStoreStatisticsAsync(ct).ConfigureAwait(false);
Console.WriteLine("   Database statistics:");
foreach (var kvp in dbStats)
{
    Console.WriteLine($"     {kvp.Key}: {kvp.Value}");
}

Console.WriteLine();
var collectionInfo = await provider.GetCollectionInfoAsync("products", ct).ConfigureAwait(false);
Console.WriteLine("   Collection 'products' info:");
foreach (var kvp in collectionInfo)
{
    Console.WriteLine($"     {kvp.Key}: {kvp.Value}");
}

Console.WriteLine();

// ── Provider Metrics ────────────────────────────────────────────────────────
Console.WriteLine("6. Provider Metrics");
Console.WriteLine("-------------------");
var metrics = await provider.GetMetricsAsync(ct).ConfigureAwait(false);
foreach (var kvp in metrics)
{
    Console.WriteLine($"   {kvp.Key}: {kvp.Value}");
}

// Cleanup
Console.WriteLine();
Console.WriteLine("Cleaning up...");
await collection.Database.DropCollectionAsync("products", ct).ConfigureAwait(false);
Console.WriteLine("Done! All MongoDB capabilities demonstrated.");
