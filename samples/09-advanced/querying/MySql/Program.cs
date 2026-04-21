// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.MySql;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using MySqlConnector;

// ============================================================================
// MySQL Data Provider Sample
// ============================================================================
//
// Demonstrates ALL Excalibur.Data.MySql capabilities:
//   1. DI registration with AddExcaliburMySql(configure) and options binding
//   2. Dapper integration via IDataRequest<MySqlConnection, T>
//   3. Connection pooling with MySqlPoolingOptions
//   4. SSL configuration via UseSsl option
//   5. Retry policy for transient failures (Polly-based exponential backoff)
//   6. Health check via IPersistenceProviderHealth
//   7. Transaction support via IPersistenceProviderTransaction
//
// Prerequisites:
//   - MySQL running on localhost:3306
//   - docker run -d --name mysql -p 3306:3306 \
//       -e MYSQL_ROOT_PASSWORD=root \
//       -e MYSQL_DATABASE=excalibur_sample \
//       mysql:8.0
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// ============================================================================
// 1. DI Registration -- AddExcaliburMySql with delegate configuration
// ============================================================================
// Option A: Configure with a delegate for full control
builder.Services.AddExcaliburMySql(options =>
{
    // Connection string -- the only required option
    options.ConnectionString =
        "Server=localhost;Port=3306;Database=excalibur_sample;User=root;Password=root";

    // Command timeout in seconds (default: 30)
    options.CommandTimeout = 30;

    // Connection timeout in seconds (default: 15)
    options.ConnectTimeout = 15;

    // Application name for connection identification
    options.ApplicationName = "MySqlSample";

    // Provider name for keyed DI resolution (default: "mysql")
    options.Name = "mysql-primary";

    // ========================================================================
    // 3. Connection Pooling -- MySqlPoolingOptions
    // ========================================================================
    // Fine-tune MySqlConnector's built-in connection pool.
    options.Pooling = new MySqlPoolingOptions
    {
        EnablePooling = true,       // Enable connection pooling (default: true)
        MinPoolSize = 5,            // Minimum idle connections (default: 0)
        MaxPoolSize = 50,           // Maximum concurrent connections (default: 100)
        ClearPoolOnDispose = true,  // Clear pool when provider disposes (default: false)
    };

    // ========================================================================
    // 4. SSL Configuration
    // ========================================================================
    // When enabled, sets MySqlSslMode.Required on the connection string.
    // For local development with Docker, leave this off.
    options.UseSsl = false;

    // ========================================================================
    // 5. Retry Policy -- Polly-based exponential backoff
    // ========================================================================
    // MaxRetryCount controls how many times transient MySQL errors are retried.
    // Transient errors include: Too many connections (1040), Deadlock (1213),
    // Lock wait timeout (1205), Server gone away (2006), Lost connection (2013).
    options.MaxRetryCount = 3;
});

// Option B (alternative): Bind from IConfiguration section
// builder.Services.AddExcaliburMySql("MySql");
//
// Option C (alternative): Bind from IConfigurationSection directly
// builder.Services.AddExcaliburMySql(builder.Configuration.GetSection("MySql"));

var host = builder.Build();

// ============================================================================
// Run the sample demonstrations
// ============================================================================
using var scope = host.Services.CreateScope();
var provider = scope.ServiceProvider.GetRequiredKeyedService<IPersistenceProvider>("mysql");
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("MySQL Data Provider Sample");
logger.LogInformation("Provider: {Name} (Type: {Type})", provider.Name, provider.ProviderType);

// ============================================================================
// 6. Health Check -- IPersistenceProviderHealth
// ============================================================================
// Access health diagnostics through the GetService escape hatch pattern.
await DemonstrateHealthCheckAsync(provider, logger, CancellationToken.None);

// ============================================================================
// 2. Dapper Integration -- IDataRequest<MySqlConnection, T>
// ============================================================================
// Execute queries using the DataRequest pattern backed by Dapper.
await DemonstrateDapperIntegrationAsync(provider, logger, CancellationToken.None);

// ============================================================================
// 7. Transaction Support -- IPersistenceProviderTransaction
// ============================================================================
// Execute multiple operations in a single transaction scope.
await DemonstrateTransactionSupportAsync(provider, logger, CancellationToken.None);

// ============================================================================
// Connection Pool Statistics
// ============================================================================
await DemonstratePoolStatsAsync(provider, logger, CancellationToken.None);

logger.LogInformation("MySQL sample completed successfully.");

// ============================================================================
// Health Check Demonstration
// ============================================================================
static async Task DemonstrateHealthCheckAsync(
    IPersistenceProvider provider,
    ILogger logger,
    CancellationToken cancellationToken)
{
    logger.LogInformation("--- Health Check ---");

    // Obtain the health sub-interface via GetService (ISP pattern)
    var health = provider.GetService(typeof(IPersistenceProviderHealth)) as IPersistenceProviderHealth;
    if (health is null)
    {
        logger.LogWarning("Health check not supported by this provider.");
        return;
    }

    // Test basic connectivity
    var isConnected = await health.TestConnectionAsync(cancellationToken);
    logger.LogInformation("Connection test: {Result}", isConnected ? "PASSED" : "FAILED");
    logger.LogInformation("IsAvailable: {Available}", health.IsAvailable);

    if (!isConnected)
    {
        logger.LogWarning("Skipping remaining demos -- MySQL is not reachable.");
        return;
    }

    // Retrieve server metrics (version, database, user, hostname, replica status)
    var metrics = await health.GetMetricsAsync(cancellationToken);
    foreach (var (key, value) in metrics)
    {
        logger.LogInformation("  {Key}: {Value}", key, value);
    }
}

// ============================================================================
// Dapper Integration Demonstration
// ============================================================================
static async Task DemonstrateDapperIntegrationAsync(
    IPersistenceProvider provider,
    ILogger logger,
    CancellationToken cancellationToken)
{
    logger.LogInformation("--- Dapper Integration ---");

    // Create a simple data request that uses Dapper under the hood.
    // IDataRequest<MySqlConnection, TResult> defines:
    //   - Command: the Dapper CommandDefinition (SQL + params + timeout)
    //   - Parameters: DynamicParameters for parameterized queries
    //   - ResolveAsync: the Dapper execution function (QueryAsync, ExecuteAsync, etc.)
    var versionRequest = new MySqlVersionRequest();

    // ExecuteAsync routes the request through the provider's retry policy,
    // opens a pooled connection, and invokes the Dapper resolver.
    var version = await provider.ExecuteAsync<MySqlConnection, string>(
        versionRequest, cancellationToken);

    logger.LogInformation("MySQL server version: {Version}", version);

    // Demonstrate a parameterized query
    var calcRequest = new MySqlCalculationRequest(42, 58);
    var result = await provider.ExecuteAsync<MySqlConnection, int>(
        calcRequest, cancellationToken);

    logger.LogInformation("Calculation 42 + 58 = {Result}", result);
}

// ============================================================================
// Transaction Support Demonstration
// ============================================================================
static async Task DemonstrateTransactionSupportAsync(
    IPersistenceProvider provider,
    ILogger logger,
    CancellationToken cancellationToken)
{
    logger.LogInformation("--- Transaction Support ---");

    // Obtain the transaction sub-interface via GetService (ISP pattern)
    var txProvider = provider.GetService(typeof(IPersistenceProviderTransaction))
        as IPersistenceProviderTransaction;

    if (txProvider is null)
    {
        logger.LogWarning("Transaction support not available.");
        return;
    }

    // Create a transaction scope with explicit isolation level
    using var txScope = txProvider.CreateTransactionScope(
        IsolationLevel.ReadCommitted,
        timeout: TimeSpan.FromSeconds(30));

    logger.LogInformation("Transaction scope created (IsolationLevel: ReadCommitted)");

    // Execute operations within the transaction scope
    // The provider enlists each operation in the same transaction.
    var setupRequest = new MySqlSetupTableRequest();
    await txProvider.ExecuteInTransactionAsync<MySqlConnection, int>(
        setupRequest, txScope, cancellationToken);

    logger.LogInformation("Temp table created within transaction");

    var insertRequest = new MySqlInsertRequest("Sample Item", 99.95m);
    await txProvider.ExecuteInTransactionAsync<MySqlConnection, int>(
        insertRequest, txScope, cancellationToken);

    logger.LogInformation("Row inserted within transaction");

    // The transaction commits when the scope completes successfully.
    // On exception, it automatically rolls back.
    logger.LogInformation("Transaction completed successfully");
}

// ============================================================================
// Connection Pool Statistics Demonstration
// ============================================================================
static async Task DemonstratePoolStatsAsync(
    IPersistenceProvider provider,
    ILogger logger,
    CancellationToken cancellationToken)
{
    logger.LogInformation("--- Connection Pool Statistics ---");

    var health = provider.GetService(typeof(IPersistenceProviderHealth)) as IPersistenceProviderHealth;
    if (health is null)
    {
        return;
    }

    // Retrieve MySQL server-side connection pool statistics
    // (threads connected, threads running, max used connections)
    var poolStats = await health.GetConnectionPoolStatsAsync(cancellationToken);
    if (poolStats is null)
    {
        logger.LogWarning("Pool statistics not available.");
        return;
    }

    foreach (var (key, value) in poolStats)
    {
        logger.LogInformation("  {Key}: {Value}", key, value);
    }
}

// ============================================================================
// Sample DataRequest Implementations
// ============================================================================
// These demonstrate how consumers build Dapper-backed data requests.

/// <summary>
/// A data request that retrieves the MySQL server version using Dapper.
/// </summary>
sealed class MySqlVersionRequest : IDataRequest<MySqlConnection, string>
{
    public string RequestId { get; } = Guid.NewGuid().ToString();
    public string RequestType => "MySqlVersion";
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public string? CorrelationId => null;
    public IDictionary<string, object>? Metadata => null;

    public CommandDefinition Command => new("SELECT VERSION()");
    public DynamicParameters Parameters => new();

    // ResolveAsync defines the Dapper execution strategy.
    // Here we use QuerySingleAsync to get a scalar string result.
    public Func<MySqlConnection, Task<string>> ResolveAsync =>
        async connection => await connection.QuerySingleAsync<string>(Command).ConfigureAwait(false);
}

/// <summary>
/// A parameterized data request demonstrating Dapper's DynamicParameters.
/// </summary>
sealed class MySqlCalculationRequest : IDataRequest<MySqlConnection, int>
{
    private readonly int _a;
    private readonly int _b;

    public MySqlCalculationRequest(int a, int b)
    {
        _a = a;
        _b = b;
    }

    public string RequestId { get; } = Guid.NewGuid().ToString();
    public string RequestType => "MySqlCalculation";
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public string? CorrelationId => null;
    public IDictionary<string, object>? Metadata => null;

    public CommandDefinition Command => new("SELECT @A + @B AS Result", Parameters);

    public DynamicParameters Parameters
    {
        get
        {
            var p = new DynamicParameters();
            p.Add("A", _a);
            p.Add("B", _b);
            return p;
        }
    }

    public Func<MySqlConnection, Task<int>> ResolveAsync =>
        async connection => await connection.QuerySingleAsync<int>(Command).ConfigureAwait(false);
}

/// <summary>
/// Creates a temporary table for transaction demonstration.
/// </summary>
sealed class MySqlSetupTableRequest : IDataRequest<MySqlConnection, int>
{
    public string RequestId { get; } = Guid.NewGuid().ToString();
    public string RequestType => "MySqlSetupTable";
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public string? CorrelationId => null;
    public IDictionary<string, object>? Metadata => null;

    public CommandDefinition Command => new("""
        CREATE TEMPORARY TABLE IF NOT EXISTS sample_items (
            id INT AUTO_INCREMENT PRIMARY KEY,
            name VARCHAR(100) NOT NULL,
            price DECIMAL(10,2) NOT NULL,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        )
        """);

    public DynamicParameters Parameters => new();

    public Func<MySqlConnection, Task<int>> ResolveAsync =>
        async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
}

/// <summary>
/// Inserts a row into the temporary table within a transaction.
/// </summary>
sealed class MySqlInsertRequest : IDataRequest<MySqlConnection, int>
{
    private readonly string _name;
    private readonly decimal _price;

    public MySqlInsertRequest(string name, decimal price)
    {
        _name = name;
        _price = price;
    }

    public string RequestId { get; } = Guid.NewGuid().ToString();
    public string RequestType => "MySqlInsert";
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public string? CorrelationId => null;
    public IDictionary<string, object>? Metadata => null;

    public CommandDefinition Command => new(
        "INSERT INTO sample_items (name, price) VALUES (@Name, @Price)",
        Parameters);

    public DynamicParameters Parameters
    {
        get
        {
            var p = new DynamicParameters();
            p.Add("Name", _name);
            p.Add("Price", _price);
            return p;
        }
    }

    public Func<MySqlConnection, Task<int>> ResolveAsync =>
        async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
}
