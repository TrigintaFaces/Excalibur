// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Redis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

// ============================================================================
// Redis Data Provider Sample
// ============================================================================
//
// Demonstrates ALL Excalibur.Data.Redis capabilities:
//   1. DI registration with AddRedisProvider and options configuration
//   2. Connection pool configuration (timeouts, retries, abort behavior)
//   3. Database selection via DatabaseId option
//   4. Direct CRUD operations via StackExchange.Redis IDatabase
//   5. Transaction support via CreateTransactionScope / RedisTransactionScope
//   6. Health checks via IPersistenceProviderHealth (TestConnectionAsync)
//   7. Provider metrics and connection pool statistics
//   8. Pub/Sub via GetSubscriber()
//   9. Retry policy with exponential backoff (built-in Polly resilience)
//  10. SSL/TLS and password configuration
//
// Prerequisites:
//   - Redis running on localhost:6379
//   - docker run -d --name redis -p 6379:6379 redis:7-alpine
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// 1. DI Registration -- AddRedisProvider with inline options
// ---------------------------------------------------------------------------
// The AddRedisProvider extension registers RedisProviderOptions with
// ValidateOnStart and the RedisProviderOptionsValidator.
builder.Services.AddRedisProvider(options =>
{
    options.ConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    options.DatabaseId = int.TryParse(builder.Configuration["Redis:DatabaseId"], out var dbId) ? dbId : 0;
    options.Name = "sample-redis";

    // 2. Connection Pool Configuration
    //    RedisConnectionPoolOptions controls timeouts, retries, and abort behavior.
    options.Pool = new RedisConnectionPoolOptions
    {
        ConnectTimeout = 10,       // seconds -- time to establish initial connection
        SyncTimeout = 5,           // seconds -- synchronous operation timeout
        AsyncTimeout = 5,          // seconds -- asynchronous operation timeout
        ConnectRetry = 3,          // number of reconnect attempts on connection loss
        AbortOnConnectFail = false, // false = resilient; silently reconnects in background
        RetryCount = 3,            // operation-level retries with exponential backoff
    };

    // SSL/TLS configuration (uncomment for secured Redis)
    // options.UseSsl = true;
    // options.Password = "your-redis-password";

    // Admin operations (INFO, FLUSHDB, etc.) -- disabled by default
    // options.AllowAdmin = true;

    // Read-only mode -- marks provider as read-only for monitoring scenarios
    // options.IsReadOnly = true;
});

// Alternative: bind directly from IConfiguration section
// builder.Services.AddRedisProvider(builder.Configuration.GetSection("Redis"));

// Register the provider itself as a singleton so we can resolve it
builder.Services.AddSingleton<RedisPersistenceProvider>();

builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));

var app = builder.Build();

Console.WriteLine("Redis Data Provider Sample");
Console.WriteLine("==========================");
Console.WriteLine();

// Resolve the provider
var provider = app.Services.GetRequiredService<RedisPersistenceProvider>();
var ct = CancellationToken.None;

// ---------------------------------------------------------------------------
// 3. Database Selection -- DatabaseId is configured in options above
// ---------------------------------------------------------------------------
Console.WriteLine($"Provider Name:  {provider.Name}");
Console.WriteLine($"Provider Type:  {provider.ProviderType}");
Console.WriteLine($"Is Available:   {provider.IsAvailable}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 6. Health Check -- TestConnectionAsync pings Redis with retry policy
// ---------------------------------------------------------------------------
Console.WriteLine("--- Health Check ---");
var healthy = await provider.TestConnectionAsync(ct).ConfigureAwait(false);
Console.WriteLine($"Connection healthy: {healthy}");
Console.WriteLine();

if (!healthy)
{
    Console.WriteLine("ERROR: Cannot connect to Redis. Ensure Redis is running on localhost:6379.");
    Console.WriteLine("  docker run -d --name redis -p 6379:6379 redis:7-alpine");
    return;
}

// ---------------------------------------------------------------------------
// 4. CRUD Operations -- direct IDatabase usage via GetDatabase()
// ---------------------------------------------------------------------------
Console.WriteLine("--- CRUD Operations ---");
var db = provider.GetDatabase();

// SET -- store a key-value pair
await db.StringSetAsync("sample:user:1", "Alice").ConfigureAwait(false);
await db.StringSetAsync("sample:user:2", "Bob").ConfigureAwait(false);
await db.StringSetAsync("sample:counter", "0").ConfigureAwait(false);
Console.WriteLine("SET sample:user:1 = Alice");
Console.WriteLine("SET sample:user:2 = Bob");
Console.WriteLine("SET sample:counter = 0");

// GET -- retrieve values
var user1 = await db.StringGetAsync("sample:user:1").ConfigureAwait(false);
Console.WriteLine($"GET sample:user:1 = {user1}");

// INCREMENT -- atomic counter operations
var newVal = await db.StringIncrementAsync("sample:counter", 5).ConfigureAwait(false);
Console.WriteLine($"INCRBY sample:counter 5 = {newVal}");

// HASH -- structured data
await db.HashSetAsync("sample:product:1", [
    new HashEntry("name", "Mechanical Keyboard"),
    new HashEntry("price", "149.99"),
    new HashEntry("stock", "50"),
]).ConfigureAwait(false);
Console.WriteLine("HSET sample:product:1 {name, price, stock}");

var productName = await db.HashGetAsync("sample:product:1", "name").ConfigureAwait(false);
var productPrice = await db.HashGetAsync("sample:product:1", "price").ConfigureAwait(false);
Console.WriteLine($"HGET sample:product:1 name = {productName}, price = {productPrice}");

// LIST -- ordered collection
await db.ListRightPushAsync("sample:queue", "task-1").ConfigureAwait(false);
await db.ListRightPushAsync("sample:queue", "task-2").ConfigureAwait(false);
await db.ListRightPushAsync("sample:queue", "task-3").ConfigureAwait(false);
var dequeued = await db.ListLeftPopAsync("sample:queue").ConfigureAwait(false);
Console.WriteLine($"LPOP sample:queue = {dequeued}");

// SET collection -- unique members
await db.SetAddAsync("sample:tags", "redis").ConfigureAwait(false);
await db.SetAddAsync("sample:tags", "nosql").ConfigureAwait(false);
await db.SetAddAsync("sample:tags", "cache").ConfigureAwait(false);
var members = await db.SetMembersAsync("sample:tags").ConfigureAwait(false);
Console.WriteLine($"SMEMBERS sample:tags = [{string.Join(", ", members.Select(m => m.ToString()))}]");

// KEY EXPIRY -- TTL support
await db.StringSetAsync("sample:session:abc", "session-data", TimeSpan.FromMinutes(30)).ConfigureAwait(false);
var ttl = await db.KeyTimeToLiveAsync("sample:session:abc").ConfigureAwait(false);
Console.WriteLine($"TTL sample:session:abc = {ttl}");

// DELETE
var deleted = await db.KeyDeleteAsync("sample:user:2").ConfigureAwait(false);
Console.WriteLine($"DEL sample:user:2 = {deleted}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 5. Transaction Support -- batch operations via CreateTransactionScope
// ---------------------------------------------------------------------------
Console.WriteLine("--- Transaction Support ---");

// The provider implements IPersistenceProviderTransaction, accessible via GetService.
var txCapability = (IPersistenceProviderTransaction?)provider.GetService(typeof(IPersistenceProviderTransaction));
if (txCapability != null)
{
    Console.WriteLine("Transaction support: available");
    Console.WriteLine($"Retry policy max attempts: {txCapability.RetryPolicy.MaxRetryAttempts}");

    // Create a transaction scope
    await using var scope = txCapability.CreateTransactionScope(IsolationLevel.ReadCommitted);
    Console.WriteLine($"Transaction ID:  {scope.TransactionId}");
    Console.WriteLine($"Transaction status: {scope.Status}");

    // Register commit/rollback callbacks for observability
    if (scope is ITransactionScopeCallbacks callbacks)
    {
        callbacks.OnCommit(() =>
        {
            Console.WriteLine("  [Callback] Transaction committed successfully");
            return Task.CompletedTask;
        });
        callbacks.OnRollback(() =>
        {
            Console.WriteLine("  [Callback] Transaction rolled back");
            return Task.CompletedTask;
        });
    }

    // Commit the transaction
    await scope.CommitAsync(ct).ConfigureAwait(false);
    Console.WriteLine($"Transaction status after commit: {scope.Status}");
}
else
{
    Console.WriteLine("Transaction support: not available");
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// 7. Provider Metrics & Connection Pool Statistics
// ---------------------------------------------------------------------------
Console.WriteLine("--- Provider Metrics ---");
var healthCapability = (IPersistenceProviderHealth?)provider.GetService(typeof(IPersistenceProviderHealth));
if (healthCapability != null)
{
    var metrics = await healthCapability.GetMetricsAsync(ct).ConfigureAwait(false);
    foreach (var kvp in metrics)
    {
        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
    }

    Console.WriteLine();
    Console.WriteLine("--- Connection Pool Statistics ---");
    var poolStats = await healthCapability.GetConnectionPoolStatsAsync(ct).ConfigureAwait(false);
    if (poolStats != null)
    {
        foreach (var kvp in poolStats)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
    }
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// 8. Pub/Sub -- publish and subscribe to channels
// ---------------------------------------------------------------------------
Console.WriteLine("--- Pub/Sub ---");
var subscriber = provider.GetSubscriber();

// Subscribe to a channel
var messageReceived = new TaskCompletionSource<string>();
var channel = RedisChannel.Literal("sample:notifications");
await subscriber.SubscribeAsync(channel, (ch, message) =>
{
    Console.WriteLine($"  Received on '{ch}': {message}");
    messageReceived.TrySetResult(message!);
}).ConfigureAwait(false);

// Publish a message
var receiversCount = await subscriber.PublishAsync(channel, "Hello from Excalibur!").ConfigureAwait(false);
Console.WriteLine($"Published to {receiversCount} subscriber(s)");

// Wait briefly for the subscription callback
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
try
{
    await messageReceived.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    Console.WriteLine("  (Pub/Sub message delivery timed out -- this can happen in some environments)");
}

await subscriber.UnsubscribeAsync(channel).ConfigureAwait(false);
Console.WriteLine();

// ---------------------------------------------------------------------------
// 9. Retry Policy -- built-in exponential backoff
// ---------------------------------------------------------------------------
Console.WriteLine("--- Retry Policy ---");
Console.WriteLine($"Max retry attempts:   {provider.RetryPolicy.MaxRetryAttempts}");
Console.WriteLine($"Base retry delay:     {provider.RetryPolicy.BaseRetryDelay}");
Console.WriteLine($"Handles exceptions:   RedisException, RedisTimeoutException, RedisConnectionException");
Console.WriteLine("Backoff strategy:     Exponential (2^attempt seconds, max 30s)");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 10. Server Access -- get Redis server info
// ---------------------------------------------------------------------------
Console.WriteLine("--- Server Info ---");
try
{
    var server = provider.GetServer();
    Console.WriteLine($"Server endpoint: {server.EndPoint}");
    Console.WriteLine($"Server connected: {server.IsConnected}");
    Console.WriteLine($"Server type: {server.ServerType}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Server info unavailable: {ex.Message}");
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// Cleanup -- remove sample keys
// ---------------------------------------------------------------------------
Console.WriteLine("--- Cleanup ---");
RedisKey[] keysToDelete =
[
    "sample:user:1", "sample:counter", "sample:product:1",
    "sample:queue", "sample:tags", "sample:session:abc",
];
var deletedCount = await db.KeyDeleteAsync(keysToDelete).ConfigureAwait(false);
Console.WriteLine($"Cleaned up {deletedCount} sample keys");
Console.WriteLine();

// Dispose the provider (closes the ConnectionMultiplexer)
await provider.DisposeAsync().ConfigureAwait(false);
Console.WriteLine("Done! All Redis capabilities demonstrated.");
