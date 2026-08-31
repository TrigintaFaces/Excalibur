// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in this test fixture.

namespace Excalibur.Workflows.SqlServer.Tests;

/// <summary>
/// Shared fixture for the SQL Server durable workflow signal-inbox TestContainer.
/// </summary>
/// <remarks>
/// Starts a real SQL Server container and creates <c>[dbo].[workflow_signal_inbox]</c> whose columns and
/// constraints mirror EXACTLY what <see cref="SqlServerWorkflowSignalInbox"/>'s Dapper INSERT/SELECT
/// reference (per the package README schema): the <c>Sequence BIGINT IDENTITY</c> arrival column backs
/// deterministic drain ordering, and the <c>UNIQUE (TenantId, InstanceId, SignalId)</c> constraint backs
/// the conditional-insert dedup that survives a "restart" (a fresh inbox instance over the same DB) while
/// keeping two tenants' identically-named signals distinct rather than treating the second as a duplicate.
/// Never skipped: when Docker is unavailable the base fixture fails fast.
/// </remarks>
public sealed class SqlServerWorkflowSignalInboxContainerFixture : ContainerFixtureBase
{
    private MsSqlContainer? _container;
    private readonly OneTimeInitializer _initializer = new();

    /// <summary>
    /// Gets the schema name for the signal-inbox table (the impl's default).
    /// </summary>
    public string SchemaName { get; } = "dbo";

    /// <summary>
    /// Gets the table name for the signal inbox (the impl's default).
    /// </summary>
    public string TableName { get; } = "workflow_signal_inbox";

    /// <summary>
    /// Gets the connection string for the SQL Server container.
    /// </summary>
    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized");

    /// <inheritdoc/>
    protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

    /// <inheritdoc/>
    protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
    {
        _container = new MsSqlBuilder()
            .WithBoundedMemory()
            .WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
            .WithName($"mssql-workflow-signal-inbox-test-{Guid.NewGuid():N}")
            .WithPassword("Test@Pass123")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures the signal-inbox schema is initialized.
    /// </summary>
    public Task EnsureInitializedAsync() => _initializer.RunAsync(InitializeSchemaAsync);

    /// <summary>
    /// Provisions the schema. Runs once, through <see cref="OneTimeInitializer"/>, so a failure
    /// here is rethrown to every later caller instead of being retried against a database this
    /// call already half-provisioned.
    /// </summary>
    private async Task InitializeSchemaAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        // Mirrors the package README DDL exactly: IDENTITY arrival sequence + UNIQUE (TenantId, InstanceId, SignalId).
        var createTableSql = $"""
            IF NOT EXISTS (SELECT * FROM sys.tables t
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = '{SchemaName}' AND t.name = '{TableName}')
            BEGIN
                CREATE TABLE [{SchemaName}].[{TableName}] (
                    Sequence    BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId    NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,
                    InstanceId  NVARCHAR(200)        NOT NULL,
                    SignalId    NVARCHAR(200)        NOT NULL,
                    SignalName  NVARCHAR(200)        NOT NULL,
                    PayloadJson NVARCHAR(MAX)        NULL,
                    CONSTRAINT UQ_{TableName} UNIQUE (TenantId, InstanceId, SignalId)
                );
            END
            """;

        await using var command = new SqlCommand(createTableSql, connection);
        _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new <see cref="SqlConnection"/> to the container.
    /// </summary>
    /// <returns>A new connection instance.</returns>
    public SqlConnection CreateConnection() => new(ConnectionString);

    /// <summary>
    /// Cleans up all rows from the signal-inbox table between tests.
    /// </summary>
    public async Task CleanupTableAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        var truncateSql = $"TRUNCATE TABLE [{SchemaName}].[{TableName}]";
        await using var command = new SqlCommand(truncateSql, connection);
        _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_container is not null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // Suppress disposal errors and timeouts to prevent test host crash.
        }
    }
}
