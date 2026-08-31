// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Helpers;

using Xunit;

#pragma warning disable CA1812 // Internal record is never instantiated (constructed inline below)

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-SQL-Server coverage of the shipped 009 migration — the path every EXISTING deployment takes.
/// </summary>
/// <remarks>
/// <para>
/// The snapshot table's natural key is 1148 bytes (two NVARCHAR(255) columns and one NVARCHAR(64), at two
/// bytes per character). SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700.
/// Earlier releases of 002 declared the key CLUSTERED, which the engine accepts at CREATE TABLE with only a
/// warning and then enforces per ROW: a save whose aggregate id, type and tenant together run past roughly
/// 450 characters is refused outright. Every other snapshot in the same table saves normally, so the fault
/// is invisible until a consumer introduces a long aggregate type — and then the snapshot is simply never
/// written and every load falls back to a full event replay.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — the pre-migration CLUSTERED shape is shown to actually
/// reject the oversized save, so the migration answers a real failure and this test cannot pass vacuously
/// against an already-correct table. LIVENESS — after 009 the same save succeeds and the snapshot is
/// readable back through the store, so the fix is not "the error stopped" but "the row is there."
/// </para>
/// <para>
/// The key columns are populated at their declared maxima rather than at some length that merely happens to
/// exceed the cap, because the shipped schema permits those values and a key that cannot hold what its own
/// columns admit is the defect itself.
/// </para>
/// </remarks>
[Collection(SqlServerSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerSnapshotKeyIndexWidthMigrationShould
{
	/// <summary>An aggregate id at the column's declared maximum.</summary>
	private static readonly string LongAggregateId = new('a', 255);

	/// <summary>An aggregate type at the column's declared maximum. With the id above the key is oversized.</summary>
	private static readonly string LongAggregateType = new('t', 255);

	private readonly SqlServerSnapshotStoreContainerFixture _fixture;

	public SqlServerSnapshotKeyIndexWidthMigrationShould(SqlServerSnapshotStoreContainerFixture fixture) =>
		_fixture = fixture;

	private SqlServerSnapshotStore Store() =>
		new(
			() => _fixture.CreateConnection(),
			NullLogger<SqlServerSnapshotStore>.Instance,
			tenantContext: UntenantedTestTenantContext.Instance,
			schema: _fixture.SchemaName,
			table: _fixture.TableName);

	[Fact]
	public async Task MakeLongKeyedSnapshotsSaveableOnADatabaseCreatedWithAClusteredNaturalKey()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a snapshot that silently fails to save turns every load into a full replay — this "
			+ "real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		try
		{
			// ARRANGE — reproduce the shape a consumer's existing database is in: the key as an earlier 002
			// declared it. This is what makes the SAFETY arm below non-vacuous.
			await SetPrimaryKeyClusteredAsync().ConfigureAwait(false);
			(await IsPrimaryKeyClusteredAsync().ConfigureAwait(false))
				.ShouldBeTrue("precondition: the pre-migration shape has the natural key CLUSTERED");

			var store = Store();
			var snapshot = NewSnapshot();

			// SAFETY — on the un-migrated shape the save is REJECTED by the engine rather than silently
			// dropped, and nothing is stored. The store surfaces engine failures through the data-request seam, which wraps the driver's
			// exception -- so the assertion is on the wrapper, and on the INNER cause, to pin this to the
			// index-key-width rejection specifically rather than to any failure at all.
			var rejected = await Should.ThrowAsync<OperationFailedException>(
				async () => await store.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false))
				.ConfigureAwait(false);

			rejected.InnerException.ShouldBeOfType<SqlException>()
				.Message.ShouldContain("900",
					Case.Sensitive,
					"the pre-migration failure must be the engine refusing an index entry over the 900-byte "
					+ "clustered cap");

			(await RowCountAsync().ConfigureAwait(false))
				.ShouldBe(0, "the rejected save stored nothing — this is precisely what 009 exists to fix");

			// ACT — apply the shipped migration exactly as a consumer's migration runner would.
			await ApplyMigration009Async().ConfigureAwait(false);

			(await IsPrimaryKeyClusteredAsync().ConfigureAwait(false))
				.ShouldBeFalse("009 must leave the natural key NONCLUSTERED so the 1700-byte cap applies");

			// LIVENESS — the same save now succeeds...
			await store.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

			(await RowCountAsync().ConfigureAwait(false))
				.ShouldBe(1, "the migrated database stores the snapshot (the save is not a no-op)");

			// ...and the row is readable back through the store, at the key that could not be written before.
			var loaded = await store.GetLatestSnapshotAsync(
				LongAggregateId, LongAggregateType, CancellationToken.None).ConfigureAwait(false);

			_ = loaded.ShouldNotBeNull("the migrated table serves the snapshot back at its full-length key");
			loaded.Version.ShouldBe(snapshot.Version);

			// The uniqueness guarantee is unchanged by the migration: the key is the same three columns, so a
			// second save at the same key updates rather than inserting a duplicate.
			await store.SaveSnapshotAsync(NewSnapshot(version: 2), CancellationToken.None).ConfigureAwait(false);

			(await RowCountAsync().ConfigureAwait(false))
				.ShouldBe(1, "the natural key is still UNIQUE after the migration — only its index kind changed");

			// Re-running the migration is a no-op rather than an error — a consumer's runner may replay it.
			await ApplyMigration009Async().ConfigureAwait(false);

			(await IsPrimaryKeyClusteredAsync().ConfigureAwait(false))
				.ShouldBeFalse("009 is safe to re-run against an already-migrated database");
		}
		finally
		{
			// This collection shares one table. Whatever happened above, hand it back in the shape 002
			// ships, so a failure here cannot cascade into the other locks as a schema defect.
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
			await ApplyMigration009Async().ConfigureAwait(false);
		}
	}

	private static SnapshotRow NewSnapshot(long version = 1) =>
		new(
			SnapshotId: Guid.NewGuid().ToString(),
			AggregateId: LongAggregateId,
			AggregateType: LongAggregateType,
			Version: version,
			CreatedAt: DateTimeOffset.UtcNow,
			Data: Encoding.UTF8.GetBytes("{\"state\":\"long-key\"}"),
			Metadata: null,
			TenantId: null);

	/// <summary>
	/// Applies the shipped 009 migration exactly as a consumer's migration runner would: read the script the
	/// package ships, split it into batches, and execute them in order against a real engine. The path is
	/// inlined rather than passed in so the command text provably originates in a literal.
	/// </summary>
	private async Task ApplyMigration009Async()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches(
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/009_MakeSnapshotKeyFitTheIndexLimit.sql"))
		{
			// CA2100: the command text is the package's own migration script, read from a literal path.
			// No caller supplies it and no test value reaches it. Same handling as the sibling fixtures
			// that execute shipped DDL.
#pragma warning disable CA2100
			await using var command = new SqlCommand(batch, connection);
#pragma warning restore CA2100
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Rebuilds the primary key the way an earlier 002 declared it — CLUSTERED over the natural key — so the
	/// migration under test has the shape it exists to repair. The clustered index the current schema creates
	/// is dropped first, since a table can carry only one.
	/// </summary>
	private async Task SetPrimaryKeyClusteredAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: the only interpolated elements are the fixture's constant schema/table names.
#pragma warning disable CA2100
		await using var command = new SqlCommand(
			$"""
			ALTER TABLE [{_fixture.SchemaName}].[{_fixture.TableName}] DROP CONSTRAINT [PK_EventStoreSnapshots];
			IF EXISTS (SELECT 1 FROM sys.indexes
			           WHERE object_id = OBJECT_ID(N'[{_fixture.SchemaName}].[{_fixture.TableName}]')
			             AND name = N'CIX_EventStoreSnapshots_AggregateTypeTenant')
			    DROP INDEX [CIX_EventStoreSnapshots_AggregateTypeTenant]
			        ON [{_fixture.SchemaName}].[{_fixture.TableName}];
			ALTER TABLE [{_fixture.SchemaName}].[{_fixture.TableName}]
			    ADD CONSTRAINT [PK_EventStoreSnapshots]
			        PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType], [TenantId]);
			""",
			connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task<bool> IsPrimaryKeyClusteredAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			"SELECT i.type_desc FROM sys.indexes i "
			+ "WHERE i.object_id = OBJECT_ID(@table) AND i.name = 'PK_EventStoreSnapshots'",
			connection);
		_ = command.Parameters.AddWithValue("@table", $"[{_fixture.SchemaName}].[{_fixture.TableName}]");

		var typeDesc = (string?)await command.ExecuteScalarAsync().ConfigureAwait(false);

		_ = typeDesc.ShouldNotBeNull("the table must carry a PK_EventStoreSnapshots constraint");

		return string.Equals(typeDesc, "CLUSTERED", StringComparison.Ordinal);
	}

	private async Task<int> RowCountAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: the only interpolated elements are the fixture's constant schema/table names.
#pragma warning disable CA2100
		await using var command = new SqlCommand(
			$"SELECT COUNT(*) FROM [{_fixture.SchemaName}].[{_fixture.TableName}]",
			connection);
#pragma warning restore CA2100

		return (int)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
	}

	private sealed record SnapshotRow(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId) : ISnapshot;
}
