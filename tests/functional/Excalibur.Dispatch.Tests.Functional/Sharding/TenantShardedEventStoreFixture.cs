// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

#pragma warning disable CA2100 // Database/schema names are constants owned by this fixture, never user input.

namespace Excalibur.Dispatch.Tests.Functional.Sharding;

/// <summary>
///     Two real SQL Server shards — two separate databases in one container, each carrying the event-store
///     schema this package ships.
/// </summary>
/// <remarks>
///     <para>
///     Sharding's guarantee is that a tenant's events live in <em>that tenant's</em> store and are
///     unreachable from another's. Proving it needs two genuinely separate physical destinations: a single
///     store with a tenant column proves row-level confinement, which is a different guarantee with a
///     different failure mode. Two databases is the smallest shape that makes "routed to the wrong shard"
///     observable — the rows are either in one database or the other, and the assertion reads both.
///     </para>
///     <para>
///     The schema is the one the package SHIPS, applied per shard in the order a consumer applies it, so a
///     fixture-local restatement cannot drift from what consumers actually get.
///     </para>
/// </remarks>
public sealed class TenantShardedEventStoreFixture : ContainerFixtureBase
{
    /// <summary>The database backing shard A.</summary>
    public const string ShardADatabase = "excalibur_shard_a";

    /// <summary>The database backing shard B.</summary>
    public const string ShardBDatabase = "excalibur_shard_b";

    /// <summary>The shard id tenant A is mapped to.</summary>
    public const string ShardAId = "shard-a";

    /// <summary>The shard id tenant B is mapped to.</summary>
    public const string ShardBId = "shard-b";

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

    /// <summary>Gets the connection string for shard A's database.</summary>
    public string ShardAConnectionString => ConnectionStringFor(ShardADatabase);

    /// <summary>Gets the connection string for shard B's database.</summary>
    public string ShardBConnectionString => ConnectionStringFor(ShardBDatabase);

    /// <inheritdoc/>
    protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

    /// <summary>Creates both shard databases and applies the shipped schema to each. Idempotent.</summary>
    /// <returns>A task that completes once both shards are ready.</returns>
    public Task EnsureShardsInitializedAsync() => _initializer.RunAsync(InitializeShardsAsync);

    /// <summary>
    ///     Counts the event rows physically present in one shard's database for a given aggregate — read
    ///     straight from the engine, not through any framework type.
    /// </summary>
    /// <param name="database">The shard database to read.</param>
    /// <param name="aggregateId">The aggregate whose rows are counted.</param>
    /// <returns>The number of rows in that database.</returns>
    /// <remarks>
    ///     Deliberately bypasses the event store. The claim under test is <em>which physical destination the
    ///     rows landed in</em>, and asking the routing store where it put them would let the same routing
    ///     decision answer both halves of the question.
    /// </remarks>
    public async Task<int> RowCountInAsync(string database, string aggregateId)
    {
        await using var connection = new SqlConnection(ConnectionStringFor(database));
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM [dbo].[EventStoreEvents] WHERE AggregateId = @aggId",
            connection);
        _ = command.Parameters.AddWithValue("@aggId", aggregateId);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    /// <summary>Removes every event row from both shards, so each arm starts from a known state.</summary>
    /// <returns>A task that completes once both shards are empty.</returns>
    public async Task CleanupAsync()
    {
        foreach (var database in new[] { ShardADatabase, ShardBDatabase })
        {
            await using var connection = new SqlConnection(ConnectionStringFor(database));
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new SqlCommand("DELETE FROM [dbo].[EventStoreEvents]", connection);
            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
    {
        _container = new MsSqlBuilder()
            .WithBoundedMemory()
            .WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
            .WithName($"mssql-tenant-shards-{Guid.NewGuid():N}")
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

    private string ConnectionStringFor(string database)
    {
        var baseConnectionString = _container?.GetConnectionString()
            ?? throw new InvalidOperationException("Container not initialized");

        return new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = database }
            .ConnectionString;
    }

    private async Task InitializeShardsAsync()
    {
        var master = _container?.GetConnectionString()
            ?? throw new InvalidOperationException("Container not initialized");

        await using (var connection = new SqlConnection(master))
        {
            await connection.OpenAsync().ConfigureAwait(false);

            foreach (var database in new[] { ShardADatabase, ShardBDatabase })
            {
                await using var create = new SqlCommand(
                    $"IF DB_ID('{database}') IS NULL CREATE DATABASE [{database}];",
                    connection);
                _ = await create.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        foreach (var database in new[] { ShardADatabase, ShardBDatabase })
        {
            await using var connection = new SqlConnection(ConnectionStringFor(database));
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
}

/// <summary>Shares one two-shard SQL Server container across the tenant-routing arms.</summary>
[CollectionDefinition(nameof(TenantShardedEventStoreCollection))]
public sealed class TenantShardedEventStoreCollection : ICollectionFixture<TenantShardedEventStoreFixture>
{
    // No body — xUnit uses this type only to associate the fixture with the collection.
}
