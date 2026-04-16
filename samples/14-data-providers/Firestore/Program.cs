// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// -----------------------------------------------------------------------
// Excalibur.Data.Firestore -- Getting Started Sample
//
// Demonstrates:
//   1. DI registration with AddExcaliburFirestore (builder-based)
//   2. Emulator support for local development
//   3. CRUD operations via ICloudNativePersistenceProvider
//   4. Real-time listener subscriptions (change feed)
//   5. Flexible credential configuration
//   6. Health check integration
// -----------------------------------------------------------------------

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Firestore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ---------------------------------------------------------------------------
// 1. Build the host with Firestore services
// ---------------------------------------------------------------------------

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole();

// ---------------------------------------------------------------------------
// Option A: Register Firestore via AddExcaliburFirestore builder (preferred for code-first)
// ---------------------------------------------------------------------------
builder.Services.AddExcaliburFirestore(firestore =>
{
    // ProjectId is required for production. When EmulatorHost is set,
    // the SDK uses FIRESTORE_EMULATOR_HOST and ProjectId can be any string.
    firestore.ProjectId("excalibur-sample");

    // Point to the local Firestore emulator for development.
    // The emulator avoids the need for real GCP credentials.
    firestore.EmulatorHost("localhost:8086");

    // Default collection used when the partition key does not override it.
    firestore.CollectionName("items");
});

// ---------------------------------------------------------------------------
// Option B (commented): Register from IConfiguration via builder
// ---------------------------------------------------------------------------
// builder.Services.AddExcaliburFirestore(firestore =>
//     firestore.BindConfiguration("Firestore"));

// ---------------------------------------------------------------------------
// 5. Credential configuration examples (for production, not emulator)
//
//    a) Application Default Credentials (ADC) -- no extra config needed.
//       The SDK uses GOOGLE_APPLICATION_CREDENTIALS or metadata server.
//
//    b) Service account JSON file path:
//       options.CredentialsPath = "/secrets/service-account.json";
//
//    c) Inline JSON credentials (useful in containers / secret managers):
//       options.CredentialsJson = Environment.GetEnvironmentVariable("GCP_CREDENTIALS_JSON");
//
// When EmulatorHost is set, all credential settings are ignored.
// ---------------------------------------------------------------------------

var host = builder.Build();

// ---------------------------------------------------------------------------
// Resolve services
// ---------------------------------------------------------------------------
var provider = host.Services.GetRequiredService<ICloudNativePersistenceProvider>();
var healthService = host.Services.GetRequiredService<HealthCheckService>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

// ---------------------------------------------------------------------------
// 2. Initialize the provider (connects to emulator or GCP)
// ---------------------------------------------------------------------------
logger.LogInformation("Initializing Firestore provider...");
await provider.InitializeAsync(null!, CancellationToken.None);
logger.LogInformation("Firestore provider initialized.");

// ---------------------------------------------------------------------------
// 6. Health Check
// ---------------------------------------------------------------------------
logger.LogInformation("--- Health Check ---");
var healthReport = await healthService.CheckHealthAsync(CancellationToken.None);
logger.LogInformation("Health status: {Status}", healthReport.Status);

// ---------------------------------------------------------------------------
// 3. CRUD Operations
// ---------------------------------------------------------------------------
var partitionKey = new PartitionKey("items");

// -- CREATE --
logger.LogInformation("--- Create ---");
var newItem = new SampleItem
{
    Id = Guid.NewGuid().ToString(),
    Name = "Excalibur Widget",
    Price = 42.99m,
    CreatedAt = DateTimeOffset.UtcNow
};
var createResult = await provider.CreateAsync(newItem, partitionKey, CancellationToken.None);
logger.LogInformation(
    "Created document: Success={Success}, StatusCode={StatusCode}",
    createResult.Success,
    createResult.StatusCode);

// -- READ --
logger.LogInformation("--- Read ---");
var fetched = await provider.GetByIdAsync<SampleItem>(
    newItem.Id, partitionKey, consistencyOptions: null, CancellationToken.None);
if (fetched is not null)
{
    logger.LogInformation("Fetched document: Name={Name}, Price={Price}", fetched.Name, fetched.Price);
}
else
{
    logger.LogWarning("Document not found after create.");
}

// -- UPDATE --
logger.LogInformation("--- Update ---");
newItem.Name = "Excalibur Widget v2";
newItem.Price = 49.99m;
var updateResult = await provider.UpdateAsync(
    newItem, partitionKey, etag: null, CancellationToken.None);
logger.LogInformation(
    "Updated document: Success={Success}, StatusCode={StatusCode}",
    updateResult.Success,
    updateResult.StatusCode);

// -- QUERY --
logger.LogInformation("--- Query ---");
var queryOps = (ICloudNativePersistenceQueryOperations)provider;
var queryResult = await queryOps.QueryAsync<SampleItem>(
    queryText: "",
    partitionKey,
    parameters: null,
    consistencyOptions: null,
    CancellationToken.None);
logger.LogInformation("Query returned {Count} document(s).", queryResult.Documents.Count);

// -- DELETE --
logger.LogInformation("--- Delete ---");
var deleteResult = await provider.DeleteAsync(
    newItem.Id, partitionKey, etag: null, CancellationToken.None);
logger.LogInformation(
    "Deleted document: Success={Success}, StatusCode={StatusCode}",
    deleteResult.Success,
    deleteResult.StatusCode);

// ---------------------------------------------------------------------------
// Provider metadata
// ---------------------------------------------------------------------------
logger.LogInformation("--- Provider Info ---");
if (provider is ICloudNativeProviderInfo info)
{
    logger.LogInformation("Cloud provider: {CloudProvider}", info.CloudProvider);
}

// ---------------------------------------------------------------------------
// 4. Real-time Listener (Change Feed) -- setup demonstration
//
// The Firestore provider supports real-time listeners via
// ICloudNativePersistenceChangeFeed.CreateChangeFeedSubscriptionAsync.
// The subscription uses Firestore's native snapshot listener under the hood,
// delivering document changes (Added, Modified, Removed) through an
// IAsyncEnumerable<IChangeFeedEvent<T>> channel.
//
// Below we show how to create, read from, and dispose a subscription.
// In production, you would typically run this in a BackgroundService.
// ---------------------------------------------------------------------------
logger.LogInformation("--- Real-time Listener (Change Feed) ---");

var changeFeed = provider.GetService(typeof(ICloudNativePersistenceChangeFeed))
    as ICloudNativePersistenceChangeFeed;

if (changeFeed is not null)
{
    logger.LogInformation("Change feed is supported. Creating subscription on 'items' collection...");

    await using var subscription = await changeFeed.CreateChangeFeedSubscriptionAsync<SampleItem>(
        containerName: "items",
        options: null,
        CancellationToken.None);

    logger.LogInformation(
        "Subscription created: Id={SubscriptionId}, Active={IsActive}",
        subscription.SubscriptionId,
        subscription.IsActive);

    // Insert a document so the listener picks up the change.
    var listenItem = new SampleItem
    {
        Id = Guid.NewGuid().ToString(),
        Name = "Listener Test Item",
        Price = 9.99m,
        CreatedAt = DateTimeOffset.UtcNow
    };
    _ = await provider.CreateAsync(listenItem, partitionKey, CancellationToken.None);

    // Read changes with a short timeout so the sample does not block forever.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    try
    {
        await foreach (var change in subscription.ReadChangesAsync(cts.Token))
        {
            logger.LogInformation(
                "Change event: Type={EventType}, DocId={DocumentId}, Seq={SequenceNumber}",
                change.EventType,
                change.DocumentId,
                change.SequenceNumber);
        }
    }
    catch (OperationCanceledException)
    {
        // Expected -- the timeout elapsed.
        logger.LogInformation("Listener timed out after 5 seconds (expected in sample).");
    }

    // Stop the subscription explicitly (also handled by DisposeAsync above).
    await subscription.StopAsync(CancellationToken.None);
    logger.LogInformation("Subscription stopped.");

    // Clean up the listener test document.
    _ = await provider.DeleteAsync(listenItem.Id, partitionKey, etag: null, CancellationToken.None);
}
else
{
    logger.LogWarning("Change feed not available on this provider instance.");
}

// ---------------------------------------------------------------------------
// Dispose
// ---------------------------------------------------------------------------
logger.LogInformation("--- Cleanup ---");
if (provider is IAsyncDisposable asyncDisposable)
{
    await asyncDisposable.DisposeAsync();
    logger.LogInformation("Provider disposed.");
}

logger.LogInformation("Firestore sample complete.");

// ---------------------------------------------------------------------------
// Sample document model
// ---------------------------------------------------------------------------
public class SampleItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
