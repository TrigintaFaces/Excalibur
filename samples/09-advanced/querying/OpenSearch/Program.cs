// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch;
using Excalibur.Data.OpenSearch.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenSearch.Client;

// ============================================================================
// OpenSearch Sample
// ============================================================================
//
// Demonstrates ALL OpenSearch capabilities provided by Excalibur.Data.OpenSearch:
//
//   1. DI Registration -- Single-node and multi-node cluster setup
//   2. Connection Settings -- Custom configuration via ConnectionSettings
//   3. Resilience -- Circuit breaker and retry policy options
//   4. Health Checks -- Cluster health monitoring
//   5. Persistence Provider -- Keyed persistence with index/shard configuration
//   6. Dead Letter Handling -- Failed document routing and retry
//   7. Startup Connectivity Verification -- Host extension for ping check
//
// Prerequisites:
//   - OpenSearch 2.x running locally:
//     docker run -d --name opensearch -p 9200:9200 \
//       -e "discovery.type=single-node" \
//       -e "DISABLE_SECURITY_PLUGIN=true" \
//       opensearchproject/opensearch:2.11.0
//
// ============================================================================

Console.WriteLine("OpenSearch Sample");
Console.WriteLine("=================");
Console.WriteLine();

// ──────────────────────────────────────────────────────────────────────
// Section 1: Single-Node Registration
// ──────────────────────────────────────────────────────────────────────
//
// The simplest setup -- connect to a single OpenSearch node with optional
// ConnectionSettings customization.

Console.WriteLine("--- Section 1: Single-Node Registration ---");
Console.WriteLine();

var singleNodeBuilder = Host.CreateApplicationBuilder(args);

singleNodeBuilder.Services.AddOpenSearchServices(
    nodeUri: "http://localhost:9200",
    configureSettings: settings =>
    {
        // Customize connection settings: default index, request timeout, etc.
        settings
            .DefaultIndex("my-default-index")
            .RequestTimeout(TimeSpan.FromSeconds(30))
            .DisableDirectStreaming();
    });

// Register the health check for monitoring
singleNodeBuilder.Services
    .AddHealthChecks()
    .AddOpenSearchHealthCheck(
        name: "opensearch",
        timeout: TimeSpan.FromSeconds(10));

var singleNodeApp = singleNodeBuilder.Build();
Console.WriteLine("  Single-node host built successfully.");

// Verify connectivity at startup (pings the cluster)
try
{
    await singleNodeApp.VerifyOpenSearchConnectivityAsync().ConfigureAwait(false);
    Console.WriteLine("  Cluster connectivity verified.");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Connectivity check failed: {ex.Message}");
    Console.WriteLine("  (This is expected if OpenSearch is not running locally.)");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────────────
// Section 2: Multi-Node Cluster Registration
// ──────────────────────────────────────────────────────────────────────
//
// For production clusters with multiple nodes, pass a collection of URIs.
// The client uses a StaticConnectionPool for round-robin load balancing.

Console.WriteLine("--- Section 2: Multi-Node Cluster Registration ---");
Console.WriteLine();

var multiNodeBuilder = Host.CreateApplicationBuilder(args);

var clusterUris = new[]
{
    new Uri("http://opensearch-node1:9200"),
    new Uri("http://opensearch-node2:9200"),
    new Uri("http://opensearch-node3:9200"),
};

multiNodeBuilder.Services.AddOpenSearchServices(
    nodeUris: clusterUris,
    configureSettings: settings =>
    {
        settings
            .DefaultIndex("products")
            .EnableDebugMode()
            .RequestTimeout(TimeSpan.FromSeconds(60));
    });

var multiNodeApp = multiNodeBuilder.Build();
Console.WriteLine($"  Multi-node host built with {clusterUris.Length} nodes.");
Console.WriteLine();

// ──────────────────────────────────────────────────────────────────────
// Section 3: Preconfigured Client Registration
// ──────────────────────────────────────────────────────────────────────
//
// When you need full control over the OpenSearchClient, create it yourself
// and register the instance directly.

Console.WriteLine("--- Section 3: Preconfigured Client Registration ---");
Console.WriteLine();

var preconfiguredBuilder = Host.CreateApplicationBuilder(args);

#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
var connectionSettings = new ConnectionSettings(new Uri("http://localhost:9200"))
    .DefaultIndex("custom-index")
    .RequestTimeout(TimeSpan.FromSeconds(45))
    .DefaultFieldNameInferrer(name => name.ToUpperInvariant());
#pragma warning restore CA2000

var preconfiguredClient = new OpenSearchClient(connectionSettings);

preconfiguredBuilder.Services.AddOpenSearchServices(
    client: preconfiguredClient,
    registry: services =>
    {
        // The registry callback lets you add additional services that
        // depend on the OpenSearch client being available.
        Console.WriteLine("  Registry callback invoked -- add custom services here.");
    });

var preconfiguredApp = preconfiguredBuilder.Build();
Console.WriteLine("  Preconfigured client host built successfully.");
Console.WriteLine();

// ──────────────────────────────────────────────────────────────────────
// Section 4: Resilience Configuration
// ──────────────────────────────────────────────────────────────────────
//
// Excalibur.Data.OpenSearch provides resilience options for retry policies
// and circuit breakers via OpenSearchResilienceOptions, composed into
// OpenSearchConfigurationOptions.

Console.WriteLine("--- Section 4: Resilience Configuration ---");
Console.WriteLine();

// OpenSearchResilienceOptions is a POCO you can bind from configuration
// or configure in code. It composes retry, circuit breaker, and timeout settings.
var resilienceOptions = new OpenSearchResilienceOptions
{
    Enabled = true,
    Retry = new OpenSearchRetryPolicyOptions
    {
        Enabled = true,
        MaxAttempts = 5,
        BaseDelay = TimeSpan.FromSeconds(2),
        MaxDelay = TimeSpan.FromSeconds(60),
        JitterFactor = 0.2,
        UseExponentialBackoff = true,
    },
    CircuitBreaker = new CircuitBreakerOptions
    {
        Enabled = true,
        FailureThreshold = 10,
        MinimumThroughput = 20,
        BreakDuration = TimeSpan.FromSeconds(45),
        SamplingDuration = TimeSpan.FromSeconds(120),
        FailureRateThreshold = 0.6,
    },
    Timeouts = new OpenSearchTimeoutOptions
    {
        SearchTimeout = TimeSpan.FromSeconds(15),
        IndexTimeout = TimeSpan.FromSeconds(30),
        BulkTimeout = TimeSpan.FromMinutes(5),
        DeleteTimeout = TimeSpan.FromSeconds(15),
    },
};

Console.WriteLine("  Resilience configuration:");
Console.WriteLine($"    Retry enabled:           {resilienceOptions.Retry.Enabled}");
Console.WriteLine($"    Max retry attempts:      {resilienceOptions.Retry.MaxAttempts}");
Console.WriteLine($"    Exponential backoff:     {resilienceOptions.Retry.UseExponentialBackoff}");
Console.WriteLine($"    Base delay:              {resilienceOptions.Retry.BaseDelay}");
Console.WriteLine($"    Circuit breaker enabled: {resilienceOptions.CircuitBreaker.Enabled}");
Console.WriteLine($"    Failure threshold:       {resilienceOptions.CircuitBreaker.FailureThreshold}");
Console.WriteLine($"    Break duration:          {resilienceOptions.CircuitBreaker.BreakDuration}");
Console.WriteLine($"    Failure rate threshold:  {resilienceOptions.CircuitBreaker.FailureRateThreshold:P0}");
Console.WriteLine($"    Search timeout:          {resilienceOptions.Timeouts.SearchTimeout}");
Console.WriteLine($"    Bulk timeout:            {resilienceOptions.Timeouts.BulkTimeout}");
Console.WriteLine();

// ──────────────────────────────────────────────────────────────────────
// Section 5: Persistence Provider
// ──────────────────────────────────────────────────────────────────────
//
// Register the OpenSearch persistence provider for keyed DI resolution.
// This integrates with Excalibur.Data.Abstractions' IPersistenceProvider.

Console.WriteLine("--- Section 5: Persistence Provider ---");
Console.WriteLine();

var persistenceBuilder = Host.CreateApplicationBuilder(args);

persistenceBuilder.Services.AddOpenSearchServices(
    nodeUri: "http://localhost:9200");

persistenceBuilder.Services.AddOpenSearchPersistence(options =>
{
    options.IndexPrefix = "myapp-";
    options.RefreshPolicy = OpenSearchRefreshPolicy.WaitFor;
    options.NumberOfShards = 2;
    options.NumberOfReplicas = 1;
    options.MaxResultCount = 500;
});

var persistenceApp = persistenceBuilder.Build();
Console.WriteLine("  Persistence provider registered with:");
Console.WriteLine("    Index prefix:     myapp-");
Console.WriteLine("    Refresh policy:   WaitFor");
Console.WriteLine("    Shards:           2");
Console.WriteLine("    Replicas:         1");
Console.WriteLine("    Max results:      500");
Console.WriteLine();

// ──────────────────────────────────────────────────────────────────────
// Section 6: Dead Letter Handling
// ──────────────────────────────────────────────────────────────────────
//
// Failed documents can be routed to a dead letter index for later retry.
// Configure via OpenSearchDeadLetterOptions.

Console.WriteLine("--- Section 6: Dead Letter Handling ---");
Console.WriteLine();

var dlqBuilder = Host.CreateApplicationBuilder(args);

dlqBuilder.Services.AddOpenSearchServices(
    nodeUri: "http://localhost:9200");

dlqBuilder.Services.Configure<OpenSearchDeadLetterOptions>(options =>
{
    options.DeadLetterIndexPrefix = "dlq-myapp";
    options.MaxRetryCount = 5;
    options.RetentionPeriod = TimeSpan.FromDays(90);
});

// Register the dead letter handler
dlqBuilder.Services.AddSingleton<OpenSearchDeadLetterHandler>();

var dlqApp = dlqBuilder.Build();
Console.WriteLine("  Dead letter handler configured:");
Console.WriteLine("    Index prefix:      dlq-myapp");
Console.WriteLine("    Max retry count:   5");
Console.WriteLine("    Retention period:  90 days");
Console.WriteLine();

// ──────────────────────────────────────────────────────────────────────
// Section 7: Health Check Verification
// ──────────────────────────────────────────────────────────────────────
//
// Health checks can be resolved and executed to verify cluster status.

Console.WriteLine("--- Section 7: Health Check Verification ---");
Console.WriteLine();

using (var scope = singleNodeApp.Services.CreateScope())
{
    var healthCheckService = scope.ServiceProvider
        .GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();

    if (healthCheckService is not null)
    {
        try
        {
            var report = await healthCheckService.CheckHealthAsync(CancellationToken.None)
                .ConfigureAwait(false);

            Console.WriteLine($"  Overall status: {report.Status}");
            foreach (var entry in report.Entries)
            {
                Console.WriteLine($"    {entry.Key}: {entry.Value.Status} -- {entry.Value.Description}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Health check failed: {ex.Message}");
            Console.WriteLine("  (This is expected if OpenSearch is not running locally.)");
        }
    }
    else
    {
        Console.WriteLine("  HealthCheckService not available.");
    }
}

Console.WriteLine();
Console.WriteLine("Done! All OpenSearch capabilities demonstrated.");
