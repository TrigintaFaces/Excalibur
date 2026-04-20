// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Postgres;
using Excalibur.Data.Postgres.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Npgsql;

// ============================================================================
// PostgreSQL Data Provider - Comprehensive Sample
// ============================================================================
//
// Demonstrates ALL Excalibur.Data.Postgres capabilities:
//
//   1. DI Registration        - Connection factory and provider registration
//   2. Dapper Integration     - How the provider uses Dapper under the hood
//   3. Dead Letter Store      - AddPostgresDeadLetterStore for failed messages
//   4. Connection Pooling     - NpgsqlDataSource-based pooling options
//   5. JSONB Support          - EnableJsonb configuration for dynamic JSON
//   6. Prepared Statements    - AutoPrepare for statement caching
//   7. SSL Configuration      - SslMode options for secure connections
//   8. Health Check           - Built-in health monitoring
//
// Prerequisites:
//   - PostgreSQL running on localhost:5432
//   - docker run -d --name postgres -p 5432:5432 \
//       -e POSTGRES_PASSWORD=postgres \
//       -e POSTGRES_DB=excalibur_sample \
//       postgres:16
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration["Postgres:ConnectionString"]
    ?? "Host=localhost;Port=5432;Database=excalibur_sample;Username=postgres;Password=postgres";

// ============================================================================
// 1. DI Registration - Connection Factory Pattern
// ============================================================================
//
// AddPostgresDataExecutors registers a Func<IDbConnection> factory in DI.
// This is the foundation for Dapper-based data access via IDataRequest<T>.

builder.Services.AddPostgresDataExecutors(() => new NpgsqlConnection(connectionString));

Console.WriteLine("PostgreSQL Data Provider Sample");
Console.WriteLine("===============================");
Console.WriteLine();

// ============================================================================
// 2. Persistence Provider - Full Configuration
// ============================================================================
//
// AddPostgresPersistence registers the PostgresPersistenceProvider which
// provides health monitoring, transaction management, retry policies,
// and metrics collection. It uses Dapper under the hood for SQL execution.
//
// Option A: Connection string with inline configuration
builder.Services.AddPostgresPersistence(connectionString, options =>
{
    // Command and connection timeouts
    options.CommandTimeout = 30;
    options.ConnectionTimeout = 15;

    // Enable metrics collection for observability
    options.EnableMetrics = true;

    // ========================================================================
    // 4. Connection Pooling - NpgsqlDataSource-based pooling
    // ========================================================================
    //
    // Npgsql manages connection pools automatically. These options control
    // pool sizing, idle connection lifetime, and pruning behavior.
    options.Pooling.EnableConnectionPooling = true;
    options.Pooling.MinPoolSize = 2;
    options.Pooling.MaxPoolSize = 50;

    // Connection lifecycle management
    options.Connection.ConnectionIdleLifetime = 300;   // 5 minutes before idle connections are pruned
    options.Connection.ConnectionPruningInterval = 10;  // Check every 10 seconds
    options.Connection.EnableTcpKeepAlive = true;       // Detect broken connections
    options.Connection.TcpKeepAliveTime = 30;           // 30 seconds between keepalive probes
    options.Connection.IncludeErrorDetail = true;       // Detailed error info (disable in production)

    // ========================================================================
    // 5. JSONB Support - Dynamic JSON column handling
    // ========================================================================
    //
    // Npgsql's EnableDynamicJson() maps .NET objects to/from PostgreSQL
    // JSONB columns automatically. This is configured via the provider's
    // PostgresProviderOptions.Advanced.EnableJsonb setting.
    //
    // When the persistence builder is used, JSONB support is part of the
    // NpgsqlDataSource configuration in PostgresPersistenceProvider.

    // ========================================================================
    // 6. Prepared Statements - Auto-Prepare for Statement Caching
    // ========================================================================
    //
    // PostgreSQL server-side prepared statements reduce parse overhead for
    // frequently executed queries. Npgsql auto-prepares statements after
    // they are executed a minimum number of times.
    options.Statements.EnablePreparedStatementCaching = true;
    options.Statements.MaxPreparedStatements = 200;        // Cache up to 200 prepared statements
    options.Statements.EnableAutoPrepare = true;
    options.Statements.AutoPrepareMinUsages = 2;           // Prepare after 2 executions

    // ========================================================================
    // 7. SSL Configuration - Secure Connections
    // ========================================================================
    //
    // PostgreSQL supports SSL/TLS connections. SslMode controls the level:
    //   - Disable:    No SSL (not recommended for production)
    //   - Allow:      Try non-SSL first, then SSL
    //   - Prefer:     Try SSL first, then non-SSL (default)
    //   - Require:    SSL required, no certificate validation
    //   - VerifyCA:   SSL required, validate server certificate CA
    //   - VerifyFull: SSL required, validate CA and hostname
    //
    // For local development, SSL is typically disabled:
    // options.Connection.EnableTcpKeepAlive = true; // already set above

    // Resilience - retry policy for transient failures
    options.Resilience.MaxRetryAttempts = 3;
    options.Resilience.RetryDelayMilliseconds = 1000;
});

// Option B: Builder pattern (alternative registration style)
// Uncomment to use instead of Option A above:
//
// builder.Services.AddPostgresPersistence(pgBuilder =>
// {
//     pgBuilder
//         .WithConnectionString(connectionString)
//         .WithConnectionPooling(enabled: true, minSize: 2, maxSize: 50)
//         .WithRetryPolicy(maxAttempts: 3, delayMilliseconds: 1000)
//         .WithTimeouts(connectionTimeout: 15, commandTimeout: 30);
// });

// Option C: Bind from IConfiguration section
// Uncomment to use instead of Option A above:
//
// builder.Services.AddPostgresPersistenceFromSection("Postgres");

// ============================================================================
// 3. Dead Letter Store - Failed Message Handling
// ============================================================================
//
// AddPostgresDeadLetterStore registers IDeadLetterStore and
// IDeadLetterStoreAdmin backed by a PostgreSQL table.
// Failed messages from dispatch pipelines are stored here for
// later inspection, retry, or manual resolution.

builder.Services.AddPostgresDeadLetterStore(connectionString);

// Alternative: configure with full options
// builder.Services.AddPostgresDeadLetterStore(options =>
// {
//     options.ConnectionString = connectionString;
//     options.SchemaName = "public";
//     options.TableName = "dead_letter_messages";
// });

// ============================================================================
// Provider-Level Configuration (PostgresProviderOptions)
// ============================================================================
//
// The lower-level PostgresProviderOptions controls the PostgresPersistenceProvider
// directly. It includes NpgsqlDataSource creation, JSONB support, and SSL.

builder.Services.Configure<PostgresProviderOptions>(options =>
{
    options.ConnectionString = connectionString;
    options.CommandTimeout = 30;
    options.ConnectTimeout = 15;
    options.UseDataSource = true; // Use NpgsqlDataSource for better connection management

    // Connection pool settings
    options.Pool.EnablePooling = true;
    options.Pool.MinPoolSize = 2;
    options.Pool.MaxPoolSize = 50;
    options.Pool.ConnectionIdleLifetime = 300;
    options.Pool.ConnectionPruningInterval = 10;

    // JSONB support via NpgsqlDataSourceBuilder.EnableDynamicJson()
    options.Advanced.EnableJsonb = true;

    // Prepared statement auto-caching
    options.Advanced.PrepareStatements = true;
    options.Advanced.MaxAutoPrepare = 20;
    options.Advanced.AutoPrepareMinUsages = 2;

    // SSL configuration
    options.Advanced.UseSsl = false;        // Set to true for production
    options.Advanced.SslMode = Npgsql.SslMode.Prefer;

    // TCP keepalive for long-lived connections
    options.Advanced.KeepAlive = 30;
});

var app = builder.Build();

// ============================================================================
// 8. Health Check - Built-in Health Monitoring
// ============================================================================
//
// AddPostgresPersistence automatically registers a health check named
// "Postgres_persistence" tagged with ["database", "Postgres", "persistence"].
// The health check verifies:
//   - Basic connectivity (SELECT 1)
//   - Server version detection
//   - Database statistics (size, connections, cache hit ratio)
//   - Blocking query detection
//   - Performance thresholds (>5s connection or >1s query = Degraded)

Console.WriteLine("--- Health Check ---");
Console.WriteLine();

var healthCheckService = app.Services.GetService<HealthCheckService>();
if (healthCheckService != null)
{
    Console.WriteLine("  Health check registered: Postgres_persistence");
    Console.WriteLine("  Tags: database, Postgres, persistence");
    Console.WriteLine("  Thresholds:");
    Console.WriteLine("    - Connection time > 5s = Degraded");
    Console.WriteLine("    - Query time > 1s = Degraded");
    Console.WriteLine("    - Blocking queries detected = Degraded");
    Console.WriteLine();

    try
    {
        var report = await healthCheckService.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"  Status: {report.Status}");

        foreach (var entry in report.Entries)
        {
            Console.WriteLine($"  {entry.Key}: {entry.Value.Status}");
            if (entry.Value.Description != null)
            {
                Console.WriteLine($"    Description: {entry.Value.Description}");
            }

            foreach (var data in entry.Value.Data)
            {
                Console.WriteLine($"    {data.Key}: {data.Value}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Health check failed (expected if PostgreSQL is not running): {ex.Message}");
    }
}

Console.WriteLine();

// ============================================================================
// Summary of Registered Services
// ============================================================================

Console.WriteLine("--- Registered Services Summary ---");
Console.WriteLine();
Console.WriteLine("  IDbConnection             - via AddPostgresDataExecutors (Func<IDbConnection> factory)");
Console.WriteLine("  ISqlPersistenceProvider   - via AddPostgresPersistence (Dapper-based SQL execution)");
Console.WriteLine("  IPersistenceProvider      - keyed as 'postgres' and 'default'");
Console.WriteLine("  ITransactionScope         - PostgresTransactionScope (ReadCommitted default)");
Console.WriteLine("  IDeadLetterStore          - via AddPostgresDeadLetterStore");
Console.WriteLine("  IDeadLetterStoreAdmin     - admin operations (stats, cleanup)");
Console.WriteLine("  IHealthCheck              - Postgres_persistence health check");
Console.WriteLine("  PostgresPersistenceMetrics - OpenTelemetry metrics collection");
Console.WriteLine();

// ============================================================================
// Configuration Reference
// ============================================================================

Console.WriteLine("--- Configuration Reference ---");
Console.WriteLine();

var persistenceOptions = app.Services.GetService<IOptions<PostgresPersistenceOptions>>();
if (persistenceOptions != null)
{
    var opts = persistenceOptions.Value;
    Console.WriteLine("  PostgresPersistenceOptions:");
    Console.WriteLine($"    CommandTimeout:     {opts.CommandTimeout}s");
    Console.WriteLine($"    ConnectionTimeout:  {opts.ConnectionTimeout}s");
    Console.WriteLine($"    EnableMetrics:      {opts.EnableMetrics}");
    Console.WriteLine($"    Pooling.Enabled:    {opts.Pooling.EnableConnectionPooling}");
    Console.WriteLine($"    Pooling.MinSize:    {opts.Pooling.MinPoolSize}");
    Console.WriteLine($"    Pooling.MaxSize:    {opts.Pooling.MaxPoolSize}");
    Console.WriteLine($"    Statements.Cache:   {opts.Statements.EnablePreparedStatementCaching}");
    Console.WriteLine($"    Statements.Max:     {opts.Statements.MaxPreparedStatements}");
    Console.WriteLine($"    Statements.AutoMin: {opts.Statements.AutoPrepareMinUsages}");
    Console.WriteLine($"    Resilience.Retries: {opts.Resilience.MaxRetryAttempts}");
    Console.WriteLine($"    Resilience.Delay:   {opts.Resilience.RetryDelayMilliseconds}ms");
}

Console.WriteLine();

var providerOptions = app.Services.GetService<IOptions<PostgresProviderOptions>>();
if (providerOptions != null)
{
    var opts = providerOptions.Value;
    Console.WriteLine("  PostgresProviderOptions:");
    Console.WriteLine($"    UseDataSource:      {opts.UseDataSource}");
    Console.WriteLine($"    Pool.EnablePooling:  {opts.Pool.EnablePooling}");
    Console.WriteLine($"    Pool.MinPoolSize:    {opts.Pool.MinPoolSize}");
    Console.WriteLine($"    Pool.MaxPoolSize:    {opts.Pool.MaxPoolSize}");
    Console.WriteLine($"    Advanced.EnableJsonb: {opts.Advanced.EnableJsonb}");
    Console.WriteLine($"    Advanced.Prepare:    {opts.Advanced.PrepareStatements}");
    Console.WriteLine($"    Advanced.UseSsl:     {opts.Advanced.UseSsl}");
    Console.WriteLine($"    Advanced.SslMode:    {opts.Advanced.SslMode}");
    Console.WriteLine($"    Advanced.KeepAlive:  {opts.Advanced.KeepAlive}s");
}

Console.WriteLine();
Console.WriteLine("Done! All PostgreSQL capabilities demonstrated.");
