// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.EventSourcing;

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // Schema/table names are constants owned by this fixture, never user input.

namespace Excalibur.Dispatch.Tests.Functional.Erasure;

/// <summary>
///     A real SQL Server event store carrying the schema this package ships, for the GDPR erasure arms.
/// </summary>
/// <remarks>
///     Erasure tombstones rows in place — it nulls the payload and overwrites the event type — so the
///     assertions have to read the engine directly. A store's own reader is the wrong instrument here: it
///     recognises the tombstone marker and reports an erased stream, which is true but is the store telling
///     you about its own write. The counts below come from SQL.
/// </remarks>
public sealed class ErasureEventStoreFixture : ContainerFixtureBase
{
    private static readonly string[] SchemaScripts =
    [
        "src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/001_CreateEventStoreSchema.sql",
        "src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/002_CreateSnapshotSchema.sql",
        "src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/003_MigrateToMultiTenant.sql",
        "src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/004_MakeEventTenantTotal.sql",
        "src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/006_ConvergeUntenantedToDefaultTenant.sql",
        "src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/007_MakeEventDataNullableForErasure.sql",
    ];

    private readonly OneTimeInitializer _initializer = new();
    private MsSqlContainer? _container;

    /// <summary>Gets the event-store schema name (the store's default).</summary>
    public string SchemaName => "dbo";

    /// <summary>Gets the event-store table name (the store's default).</summary>
    public string TableName => "EventStoreEvents";

    /// <inheritdoc/>
    protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

    /// <summary>Creates a connection to the event-store database.</summary>
    /// <returns>A new connection.</returns>
    public SqlConnection CreateConnection() =>
        new(_container?.GetConnectionString() ?? throw new InvalidOperationException("Container not initialized"));

    /// <summary>Applies the shipped event-store schema. Idempotent.</summary>
    /// <returns>A task that completes once the schema is present.</returns>
    public Task EnsureInitializedAsync() => _initializer.RunAsync(InitializeSchemaAsync);

    /// <summary>Removes every event row, so each arm starts from a known state.</summary>
    /// <returns>A task that completes once the table is empty.</returns>
    public async Task CleanupAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand($"DELETE FROM [{SchemaName}].[{TableName}]", connection);
        _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>Counts an aggregate's rows that still carry a readable payload.</summary>
    /// <param name="aggregateId">The aggregate to count.</param>
    /// <returns>The number of rows whose event data survives.</returns>
    public Task<int> SurvivingPayloadCountAsync(string aggregateId) => ScalarAsync(
        $"SELECT COUNT(*) FROM [{SchemaName}].[{TableName}] WHERE AggregateId = @aggId AND EventData IS NOT NULL",
        aggregateId);

    /// <summary>Counts an aggregate's rows that carry the framework's erasure tombstone marker.</summary>
    /// <param name="aggregateId">The aggregate to count.</param>
    /// <returns>The number of tombstoned rows.</returns>
    public Task<int> TombstonedRowCountAsync(string aggregateId) => ScalarAsync(
        $"SELECT COUNT(*) FROM [{SchemaName}].[{TableName}] WHERE AggregateId = @aggId AND EventType = '{ErasedEventMarker.EventType}'",
        aggregateId);

    /// <summary>Counts an aggregate's rows, erased or not.</summary>
    /// <param name="aggregateId">The aggregate to count.</param>
    /// <returns>The total number of rows for that aggregate.</returns>
    public Task<int> TotalRowCountAsync(string aggregateId) => ScalarAsync(
        $"SELECT COUNT(*) FROM [{SchemaName}].[{TableName}] WHERE AggregateId = @aggId",
        aggregateId);

    /// <inheritdoc/>
    protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
    {
        _container = new MsSqlBuilder()
            .WithBoundedMemory()
            .WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
            .WithName($"mssql-erasure-e2e-{Guid.NewGuid():N}")
            .WithPassword("Test@Pass123")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync(cancellationToken).ConfigureAwait(false);
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
            // Suppress disposal errors and timeouts so a teardown failure cannot crash the test host.
        }
    }

    private async Task<int> ScalarAsync(string sql, string aggregateId)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = new SqlCommand(sql, connection);
        _ = command.Parameters.AddWithValue("@aggId", aggregateId);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private async Task InitializeSchemaAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        foreach (var scriptPath in SchemaScripts)
        {
            foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches(scriptPath))
            {
                await using var command = new SqlCommand(batch, connection);
                _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}

/// <summary>Shares one SQL Server event store across the erasure arms.</summary>
[CollectionDefinition(nameof(ErasureEventStoreCollection))]
public sealed class ErasureEventStoreCollection : ICollectionFixture<ErasureEventStoreFixture>
{
    // No body — xUnit uses this type only to associate the fixture with the collection.
}
