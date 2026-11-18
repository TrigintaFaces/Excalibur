// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var app = SampleComposition.Build(args);
Console.WriteLine("Azure Cosmos DB -- Excalibur Data Provider Sample");
Console.WriteLine("==================================================");
Console.WriteLine();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var ct = cts.Token;
var provider = app.Services.GetRequiredService<CosmosDbPersistenceProvider>();
var containerName = app.Services.GetRequiredService<IOptions<CosmosDbOptions>>().Value.DefaultContainerName ?? "Items";
var runId = Guid.NewGuid().ToString("N");
var partitionKey = new PartitionKey("electronics");
var ownedIds = new[] { $"prod-{runId}-001", $"prod-{runId}-002", $"prod-{runId}-003", $"prod-{runId}-010", $"prod-{runId}-011" };
Exception? primaryFailure = null;
AggregateException? cleanupFailureToThrow = null;

try
{
    await SampleWorkload.VerifyConnectionAsync(provider, ct).ConfigureAwait(false);
    await SampleWorkload.VerifyDocumentsAsync(provider, ownedIds, partitionKey, ct).ConfigureAwait(false);
    await SampleWorkload.VerifyBatchAsync(provider, ownedIds, partitionKey, ct).ConfigureAwait(false);
    await SampleWorkload.InspectAsync(provider, app.Services, containerName, ct).ConfigureAwait(false);
}
catch (Exception exception)
{
    primaryFailure = exception;
    throw;
}
finally
{
    var cleanupFailure = await SampleCleanup.CollectAsync(app, provider, ownedIds, partitionKey).ConfigureAwait(false);
    if (primaryFailure != null && cleanupFailure != null)
    {
        primaryFailure.Data["CosmosSampleCleanupFailure"] = cleanupFailure;
    }
    else
    {
        cleanupFailureToThrow = cleanupFailure;
    }
}

if (cleanupFailureToThrow != null)
{
    throw cleanupFailureToThrow;
}

Console.WriteLine("Done. Cosmos DB create/read/query/delete/batch checks and owned cleanup passed.");

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

/// <summary>Source-generated metadata for the sample-owned document.</summary>
[JsonSerializable(typeof(Product))]
[JsonSerializable(typeof(Product[]))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
internal sealed partial class ProductJsonContext : JsonSerializerContext
{
}
