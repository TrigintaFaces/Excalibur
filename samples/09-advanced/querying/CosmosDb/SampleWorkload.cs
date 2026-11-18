// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

internal static class SampleWorkload
{
    internal static async Task VerifyConnectionAsync(CosmosDbPersistenceProvider provider, CancellationToken ct)
    {
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
            Console.WriteLine("     docker run -d --name cosmosdb -p 8081:8081 -p 1234:1234 -p 8080:8080 \\");
            Console.WriteLine("       mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-EN20260706 --protocol https");
            Console.WriteLine();
            Console.WriteLine("   --protocol https is required: the emulator defaults to HTTP and the .NET SDK");
            Console.WriteLine("   does not support HTTP mode against it.");
            // The boolean probe deliberately hides its underlying error. This public initialization
            // attempt propagates its actual SDK/configuration/cancellation failure for diagnosis.
            // A recovered second attempt does not turn the failed probe into sample success.
            await provider.InitializeAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException("Failed to connect to Cosmos DB.");
        }

        Console.WriteLine($"   Provider available: {provider.IsAvailable}");
        Console.WriteLine($"   Supports change feed: {provider.SupportsChangeFeed}");
        Console.WriteLine($"   Supports multi-region writes: {provider.SupportsMultiRegionWrites}");
        Console.WriteLine();
    }

    internal static async Task VerifyDocumentsAsync(CosmosDbPersistenceProvider provider, string[] ownedIds, PartitionKey partitionKey, CancellationToken ct)
    {
        // ---------------------------------------------------------------------------
        // 3. CRUD Operations
        // ---------------------------------------------------------------------------

        // -- CREATE --
        Console.WriteLine("2. Creating documents...");
        var product1 = new Product(ownedIds[0], "Mechanical Keyboard", "electronics", 149.99m, 50);
        var createResult1 = await provider.CreateAsync(product1, partitionKey, ct).ConfigureAwait(false);
        RequireSuccess(createResult1, "Create " + product1.Id);
        Console.WriteLine($"   Created '{product1.Name}': success={createResult1.Success}, RU={createResult1.RequestCharge:F2}");

        var product2 = new Product(ownedIds[1], "Wireless Mouse", "electronics", 79.99m, 120);
        var createResult2 = await provider.CreateAsync(product2, partitionKey, ct).ConfigureAwait(false);
        RequireSuccess(createResult2, "Create " + product2.Id);
        Console.WriteLine($"   Created '{product2.Name}': success={createResult2.Success}, RU={createResult2.RequestCharge:F2}");

        var product3 = new Product(ownedIds[2], "USB-C Hub", "electronics", 49.99m, 200);
        var createResult3 = await provider.CreateAsync(product3, partitionKey, ct).ConfigureAwait(false);
        RequireSuccess(createResult3, "Create " + product3.Id);
        Console.WriteLine($"   Created '{product3.Name}': success={createResult3.Success}, RU={createResult3.RequestCharge:F2}");
        Console.WriteLine();

        // -- GET BY ID --
        Console.WriteLine("3. Reading document by ID...");
        var fetched = await provider.GetByIdAsync<Product>(ownedIds[0], partitionKey, null, ct).ConfigureAwait(false);
        if (fetched != product1)
        {
            throw new InvalidOperationException("Persisted product does not match the created product.");
        }

        Console.WriteLine($"   Found: {fetched.Name}, Price=${fetched.Price}, Stock={fetched.Stock}");

        Console.WriteLine();

        // -- QUERY --
        Console.WriteLine("4. Querying documents (price > $50)...");
        var queried = new List<Product>();
        string? continuation = null;
        do
        {
            var queryResult = await provider.QueryAsync<Product>(
                new CloudQueryRequest
                {
                    QueryText = "SELECT * FROM c WHERE c.price > @minPrice AND c.id IN (@first, @second, @third)",
                    PartitionKey = partitionKey,
                    Parameters = new Dictionary<string, object>
                    {
                        ["minPrice"] = 50.0, ["first"] = ownedIds[0], ["second"] = ownedIds[1], ["third"] = ownedIds[2],
                    },
                    ContinuationToken = continuation,
                }, ct).ConfigureAwait(false);
            queried.AddRange(queryResult.Documents);
            continuation = queryResult.ContinuationToken;
        } while (continuation != null);

        if (queried.Count != 2 || !queried.Contains(product1) || !queried.Contains(product2))
        {
            throw new InvalidOperationException("Query did not return exactly the two expected persisted products.");
        }

        foreach (var doc in queried)
        {
            Console.WriteLine($"   - {doc.Name}: ${doc.Price}");
        }

        Console.WriteLine();

        // -- DELETE --
        Console.WriteLine($"5. Deleting document '{ownedIds[2]}'...");
        var deleteResult = await provider.DeleteAsync(ownedIds[2], partitionKey, null, ct).ConfigureAwait(false);
        RequireSuccess(deleteResult, "Delete " + ownedIds[2]);
        if (await provider.GetByIdAsync<Product>(ownedIds[2], partitionKey, null, ct).ConfigureAwait(false) != null)
        {
            throw new InvalidOperationException("Deleted product remains readable.");
        }

        Console.WriteLine($"   Deleted: success={deleteResult.Success}, RU={deleteResult.RequestCharge:F2}");
        Console.WriteLine();
    }

    internal static async Task VerifyBatchAsync(CosmosDbPersistenceProvider provider, string[] ownedIds, PartitionKey partitionKey, CancellationToken ct)
    {
        // ---------------------------------------------------------------------------
        // 4. Batch Execution -- transactional batch within a single partition
        // ---------------------------------------------------------------------------
        Console.WriteLine("6. Executing transactional batch...");
        var batchProduct1 = new Product(ownedIds[3], "Monitor Stand", "electronics", 39.99m, 75);
        var batchProduct2 = new Product(ownedIds[4], "Desk Lamp", "electronics", 29.99m, 150);
        var batchOps = new ICloudBatchOperation[]
        {
            new CloudBatchCreateOperation(ownedIds[3], batchProduct1),
            new CloudBatchCreateOperation(ownedIds[4], batchProduct2),
            new CloudBatchDeleteOperation(ownedIds[1]),
        };

        var batchResult = await provider.ExecuteBatchAsync(partitionKey, batchOps, ct).ConfigureAwait(false);
        if (!batchResult.Success || batchResult.OperationResults.Count != batchOps.Length || batchResult.OperationResults.Any(result => !result.Success))
        {
            throw new InvalidOperationException($"Transactional batch failed: {batchResult.ErrorMessage}");
        }

        if (await provider.GetByIdAsync<Product>(ownedIds[3], partitionKey, null, ct).ConfigureAwait(false) != batchProduct1 ||
            await provider.GetByIdAsync<Product>(ownedIds[4], partitionKey, null, ct).ConfigureAwait(false) != batchProduct2 ||
            await provider.GetByIdAsync<Product>(ownedIds[1], partitionKey, null, ct).ConfigureAwait(false) != null)
        {
            throw new InvalidOperationException("Transactional batch persisted state does not match its operations.");
        }

        Console.WriteLine($"   Batch success: {batchResult.Success}, RU={batchResult.RequestCharge:F2}");
        Console.WriteLine($"   Operations completed: {batchResult.OperationResults.Count}");
        foreach (var opResult in batchResult.OperationResults)
        {
            Console.WriteLine($"   - Status: {opResult.StatusCode}, Success: {opResult.Success}");
        }

        Console.WriteLine();
    }

    internal static async Task InspectAsync(CosmosDbPersistenceProvider provider, IServiceProvider services, string containerName, CancellationToken ct)
    {
        // ---------------------------------------------------------------------------
        // 5. Collection Info and Document Store Statistics
        // ---------------------------------------------------------------------------
        Console.WriteLine("7. Retrieving collection info for 'Items'...");
        var collectionInfo = await provider.GetCollectionInfoAsync(containerName, ct).ConfigureAwait(false);
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
        var healthCheckService = services.GetRequiredService<HealthCheckService>();
        var healthReport = await healthCheckService.CheckHealthAsync(ct).ConfigureAwait(false);
        if (healthReport.Status != HealthStatus.Healthy)
        {
            throw new InvalidOperationException($"Cosmos DB health check was {healthReport.Status}.");
        }
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

        var metrics = await provider.GetMetricsAsync(ct).ConfigureAwait(false);
        Console.WriteLine("    Provider metrics:");
        foreach (var kvp in metrics)
        {
            Console.WriteLine($"      {kvp.Key}: {kvp.Value}");
        }

        Console.WriteLine();
    }

    internal static void RequireSuccess(CloudOperationResult result, string operation)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException($"{operation} failed ({result.StatusCode}): {result.ErrorMessage}");
        }
    }
}
