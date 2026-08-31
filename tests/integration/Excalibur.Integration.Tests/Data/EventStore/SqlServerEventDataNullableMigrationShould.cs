// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Helpers;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-SQL-Server coverage of the shipped 007 migration — the path every EXISTING deployment takes.
/// </summary>
/// <remarks>
/// <para>
/// The erasure locks in this directory all build their table from the current 001, which ships EventData
/// nullable, so they prove the FRESH-install path only. A consumer who created their database from an
/// earlier 001 has EventData NOT NULL, and on that shape the tombstone UPDATE is rejected outright —
/// erasure fails and no payload is destroyed. Upgrading the package does not change a column that already
/// exists, so 007 is the only thing that closes it for them. Nothing else in the suite exercises that.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — the pre-migration NOT NULL shape is shown to actually
/// reject the erase, so the migration is answering a real failure rather than a supposed one, and this test
/// cannot pass vacuously against an already-correct column. LIVENESS — after 007 the same erase succeeds
/// and the personal data is verifiably gone from the stored bytes.
/// </para>
/// <para>
/// The payload assertion reads the raw EventData bytes back off the engine and looks for the subject's name
/// itself. Asserting that the erase call reported success, or that a column went NULL, would both be
/// proxies; scanning the stored bytes for the personal data is the property the obligation is actually
/// about. It is deliberately run over EVERY row of the table, not just the erased aggregate's, so a
/// migration that moved the data instead of destroying it would still be caught.
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventDataNullableMigrationShould
{
	private const string AggregateType = "Order";

	/// <summary>The subject's personal data, as it appears inside the serialized payload.</summary>
	private const string SubjectName = "Ada-Lovelace-PersonalData-Marker";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventDataNullableMigrationShould(SqlServerEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	private SqlServerEventStore Store() =>
		new(
			() => _fixture.CreateConnection(),
			NullLogger<SqlServerEventStore>.Instance,
			schema: _fixture.SchemaName,
			table: _fixture.TableName,
			tenantContext: UntenantedTestTenantContext.Instance);

	private sealed record OrderPlaced(string AggregateId, long Version, string CustomerName) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(OrderPlaced);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[Fact]
	public async Task MakeErasureWorkOnADatabaseCreatedBeforeTheColumnWasNullable()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"GDPR erasure is a legal obligation — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		try
		{
			// ARRANGE — reproduce the shape a consumer's existing database is in: the column as an
			// earlier 001 declared it. This is what makes the SAFETY arm below non-vacuous.
			await SetEventDataNullabilityAsync(nullable: false).ConfigureAwait(false);
			(await IsEventDataNullableAsync().ConfigureAwait(false))
				.ShouldBeFalse("precondition: the pre-migration shape has EventData NOT NULL");

			var aggregateId = "agg-" + Guid.NewGuid().ToString("N");
			var store = Store();

			_ = await store.AppendAsync(
				aggregateId, AggregateType,
				new IDomainEvent[] { new OrderPlaced(aggregateId, 0, SubjectName) },
				-1, CancellationToken.None).ConfigureAwait(false);

			(await StoredBytesContainAsync(SubjectName).ConfigureAwait(false))
				.ShouldBeTrue("precondition: the subject's personal data is genuinely in the stored payload");

			// SAFETY — on the un-migrated shape the erase does not silently no-op, it is REJECTED by the
			// engine, and the personal data is still there afterwards. This is the consumer-visible defect.
			// The store surfaces engine failures through the data-request seam, which wraps the driver's
			// exception -- so the assertion is on the wrapper, and on the INNER cause, to pin this to the
			// NOT NULL rejection specifically rather than to any failure at all.
			var rejected = await Should.ThrowAsync<OperationFailedException>(
				async () => await store.EraseEventsAsync(
					aggregateId, AggregateType, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false))
				.ConfigureAwait(false);

			rejected.InnerException.ShouldBeOfType<SqlException>()
				.Message.ShouldContain("EventData",
					Case.Sensitive,
					"the pre-migration failure must be the engine refusing NULL in EventData");

			(await StoredBytesContainAsync(SubjectName).ConfigureAwait(false))
				.ShouldBeTrue("the rejected erase destroyed nothing — this is precisely what 007 exists to fix");

			// ACT — apply the shipped migration exactly as a consumer's migration runner would.
			await ApplyMigration007Async().ConfigureAwait(false);

			(await IsEventDataNullableAsync().ConfigureAwait(false))
				.ShouldBeTrue("007 must leave EventData nullable so the tombstone UPDATE is permitted");

			// LIVENESS — the same erase now succeeds...
			var erased = await store.EraseEventsAsync(
				aggregateId, AggregateType, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

			erased.ShouldBe(1, "the migrated database erases the aggregate's event (erasure is not a no-op)");

			// ...and the personal data is actually GONE from the stored bytes, not merely reported gone.
			(await StoredBytesContainAsync(SubjectName).ConfigureAwait(false))
				.ShouldBeFalse("no byte of the subject's personal data may survive anywhere in the table");

			(await store.IsErasedAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeTrue("the aggregate reports itself erased after the migrated erase");

			// Re-running the migration is a no-op rather than an error — a consumer's runner may replay it.
			await ApplyMigration007Async().ConfigureAwait(false);

			(await IsEventDataNullableAsync().ConfigureAwait(false))
				.ShouldBeTrue("007 is safe to re-run against an already-migrated database");
		}
		finally
		{
			// This collection shares one table. Whatever happened above, hand it back in the shape 001
			// ships, so a failure here cannot cascade into the other locks as a schema defect.
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
			await SetEventDataNullabilityAsync(nullable: true).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Applies the shipped 007 migration exactly as a consumer's migration runner would: read the script
	/// the package ships, split it into batches, and execute them in order against a real engine. The
	/// path is inlined rather than passed in so the command text provably originates in a literal.
	/// </summary>
	private async Task ApplyMigration007Async()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches(
			"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/007_MakeEventDataNullableForErasure.sql"))
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

	private async Task SetEventDataNullabilityAsync(bool nullable)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: the only interpolated elements are the fixture's constant schema/table names.
#pragma warning disable CA2100
		await using var command = new SqlCommand(
			$"ALTER TABLE [{_fixture.SchemaName}].[{_fixture.TableName}] "
			+ $"ALTER COLUMN [EventData] VARBINARY(MAX) {(nullable ? "NULL" : "NOT NULL")}",
			connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task<bool> IsEventDataNullableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			"SELECT c.is_nullable FROM sys.columns c "
			+ "WHERE c.object_id = OBJECT_ID(@table) AND c.name = 'EventData'",
			connection);
		_ = command.Parameters.AddWithValue("@table", $"[{_fixture.SchemaName}].[{_fixture.TableName}]");

		return (bool)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
	}

	/// <summary>
	/// Reads every EventData payload in the table off the engine and reports whether the given text
	/// survives in any of them. Read directly rather than through the store, so a defect in the store's
	/// own interpretation of its tombstone cannot conceal data that is still present.
	/// </summary>
	private async Task<bool> StoredBytesContainAsync(string text)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: the only interpolated elements are the fixture's constant schema/table names.
#pragma warning disable CA2100
		await using var command = new SqlCommand(
			$"SELECT [EventData] FROM [{_fixture.SchemaName}].[{_fixture.TableName}]",
			connection);
#pragma warning restore CA2100

		await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

		while (await reader.ReadAsync().ConfigureAwait(false))
		{
			if (reader.IsDBNull(0))
			{
				continue;
			}

			var payload = (byte[])reader.GetValue(0);
			if (Encoding.UTF8.GetString(payload).Contains(text, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}
}
