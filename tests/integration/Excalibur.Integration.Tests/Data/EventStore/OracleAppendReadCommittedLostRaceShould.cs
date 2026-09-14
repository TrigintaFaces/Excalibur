// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// 4wjpvg — real-Oracle concurrency lock for the ADOPT ruling (SERIALIZABLE → ReadCommitted +
/// UNIQUE(AGGREGATEID, AGGREGATETYPE, VERSION, TENANTID)) on plain <see cref="OracleEventStore.AppendAsync"/>
/// specifically — not <c>AppendWithOutboxStagingAsync</c>, which is separately known (k44na4) to bare-rethrow
/// rather than classify, so a lock aimed there would pass for the wrong reason.
/// </summary>
/// <remarks>
/// <para>
/// <b>A genuine lost race, not a simulated error code.</b> Unlike
/// <see cref="OracleEventStoreRacingConflictClassificationShould"/> (which installs a trigger that raises an
/// arbitrary refusal code to test the classifier's structural fallback), this lock constructs the ACTUAL
/// mechanism ReadCommitted + the UNIQUE constraint now depends on: a BEFORE INSERT trigger commits a
/// competing row for the SAME stream key from an autonomous transaction immediately before this writer's own
/// INSERT runs, so this writer's INSERT hits a REAL ORA-00001 on <c>UQ_EVENTSTOREEVENTS_STREAM</c> — the
/// identical constraint violation two genuinely concurrent writers would produce under ReadCommitted. RED
/// against a store that does not classify ORA-00001 as a lost race (it would surface
/// <see cref="AppendResult.Success"/> = <see langword="false"/> with
/// <see cref="AppendResult.IsConcurrencyConflict"/> = <see langword="false"/> instead — an opaque failure a
/// caller's reload-and-retry policy never fires for).
/// </para>
/// <para>
/// verify-against-real-infra-not-mock: real Oracle only, non-skipped (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleAppendReadCommittedLostRaceShould
{
	private const string AggregateType = "ReadCommittedLostRaceOrder";
	private const string WinnerEventType = "ReadCommittedLostRaceWinner";

	private readonly OracleEventStoreContainerFixture _fixture;

	public OracleAppendReadCommittedLostRaceShould(OracleEventStoreContainerFixture fixture) => _fixture = fixture;

	private OracleEventStore CreateStore() =>
		new(
			() => new OracleConnection(_fixture.ConnectionString),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<OracleEventStore>.Instance,
			tenantContext: SingleTenantTestContext.Instance,
			payloadSerializer: null,
			schema: _fixture.Schema,
			table: _fixture.TableName);

	private static TestDomainEvent CreateEvent(string aggregateId) => new()
	{
		EventId = Guid.NewGuid().ToString(),
		AggregateId = aggregateId,
		OccurredAt = DateTimeOffset.UtcNow,
		Data = "ReadCommittedLostRace",
	};

	/// <summary>
	/// SAFETY — a real ORA-00001 unique-key collision (constructed deterministically) is classified as a
	/// concurrency conflict, reporting the winner's version.
	/// </summary>
	[Fact]
	public async Task ClassifyARealUniqueKeyCollisionAsAConcurrencyConflict_ReportingTheWinnersVersion()
	{
		_fixture.DockerAvailable.ShouldBeTrue("the ReadCommitted lost-race lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		var seed = await store.AppendAsync(
			aggregateId, AggregateType, [CreateEvent(aggregateId)], expectedVersion: -1, CancellationToken.None)
			.ConfigureAwait(false);
		seed.Success.ShouldBeTrue("the seed append establishes the version this writer will contend for");

		var tenantId = await ReadSeededTenantIdAsync(aggregateId).ConfigureAwait(false);

		await InstallLostRaceTriggerAsync(tenantId).ConfigureAwait(false);
		try
		{
			var loser = await store.AppendAsync(
				aggregateId, AggregateType, [CreateEvent(aggregateId)], expectedVersion: 0, CancellationToken.None)
				.ConfigureAwait(false);

			loser.Success.ShouldBeFalse("this writer's INSERT collided with the winner's row on the UNIQUE constraint");
			loser.IsConcurrencyConflict.ShouldBeTrue(
				"a real ORA-00001 collision must classify as a concurrency conflict under ReadCommitted, not an "
				+ "opaque failure — a caller's reload-and-retry policy keys on this flag.");
			loser.NextExpectedVersion.ShouldBe(1, "the conflict must report the WINNER's version (1), which is what makes it actionable");

			// CONTROL on the construction itself: the winner's row must actually be there, or this arm measured
			// a fixture that never built the interleaving under test.
			(await CountRowsAtVersionAsync(aggregateId, 1).ConfigureAwait(false)).ShouldBe(
				1, "the autonomous winner must have committed version 1 before the loser's INSERT ran");
		}
		finally
		{
			await DropLostRaceTriggerAsync().ConfigureAwait(false);
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	/// <summary>LIVENESS — an ordinary, uncontended sequential append still succeeds under ReadCommitted.</summary>
	[Fact]
	public async Task StillSucceedForALegitimateSequentialAppend()
	{
		_fixture.DockerAvailable.ShouldBeTrue("the ReadCommitted liveness lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		var first = await store.AppendAsync(
			aggregateId, AggregateType, [CreateEvent(aggregateId)], expectedVersion: -1, CancellationToken.None)
			.ConfigureAwait(false);
		first.Success.ShouldBeTrue(first.ErrorMessage);

		var second = await store.AppendAsync(
			aggregateId, AggregateType, [CreateEvent(aggregateId)], expectedVersion: 0, CancellationToken.None)
			.ConfigureAwait(false);
		second.Success.ShouldBeTrue(second.ErrorMessage);
		second.IsConcurrencyConflict.ShouldBeFalse("nothing contended this append");

		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Installs a BEFORE INSERT trigger that, on this writer's INSERT for version 1, first commits a
	/// competing version-1 row for the SAME stream key from an autonomous transaction — so this writer's own
	/// INSERT then collides on <c>UQ_EVENTSTOREEVENTS_STREAM</c> for real.
	/// </summary>
	private async Task InstallLostRaceTriggerAsync(string tenantId)
	{
		var trigger = $"""
			CREATE OR REPLACE TRIGGER RC_LOST_RACE_TRG
			BEFORE INSERT ON {_fixture.TableName}
			FOR EACH ROW
			DECLARE
				PRAGMA AUTONOMOUS_TRANSACTION;
			BEGIN
				IF :NEW.AGGREGATETYPE <> '{AggregateType}' OR :NEW.EVENTTYPE = '{WinnerEventType}' OR :NEW.VERSION <> 1 THEN
					RETURN;
				END IF;

				INSERT INTO {_fixture.TableName}
					(EVENTID, AGGREGATEID, AGGREGATETYPE, EVENTTYPE, EVENTDATA, METADATA, VERSION, EVENTTIMESTAMP, TENANTID)
				VALUES
					(:NEW.EVENTID || '-winner', :NEW.AGGREGATEID, :NEW.AGGREGATETYPE, '{WinnerEventType}',
					 EMPTY_BLOB(), NULL, :NEW.VERSION, SYSTIMESTAMP, '{tenantId.Replace("'", "''", StringComparison.Ordinal)}');
				COMMIT;
			END;
			""";

		await ExecuteAsync(trigger).ConfigureAwait(false);
		await AssertTriggerCompiledAsync().ConfigureAwait(false);
	}

	private async Task AssertTriggerCompiledAsync()
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		await using (var status = connection.CreateCommand())
		{
			status.CommandText =
				"SELECT STATUS FROM USER_OBJECTS WHERE OBJECT_NAME = 'RC_LOST_RACE_TRG' AND OBJECT_TYPE = 'TRIGGER'";
			var value = await status.ExecuteScalarAsync().ConfigureAwait(false);
			if (value as string == "VALID")
			{
				return;
			}
		}

		await using var errors = connection.CreateCommand();
		errors.CommandText =
			"SELECT LINE || ':' || POSITION || ' ' || TEXT FROM USER_ERRORS WHERE NAME = 'RC_LOST_RACE_TRG' ORDER BY SEQUENCE";
		var diagnostics = new List<string>();
		await using (var reader = await errors.ExecuteReaderAsync().ConfigureAwait(false))
		{
			while (await reader.ReadAsync().ConfigureAwait(false))
			{
				diagnostics.Add(reader.GetString(0));
			}
		}

		throw new InvalidOperationException(
			"The lost-race trigger did not compile, so this arm would measure a broken fixture: " + string.Join(" | ", diagnostics));
	}

	private async Task DropLostRaceTriggerAsync()
	{
		try
		{
			await ExecuteAsync("DROP TRIGGER RC_LOST_RACE_TRG").ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number == 4080)
		{
			// ORA-04080: already gone.
		}
	}

	private async Task<string> ReadSeededTenantIdAsync(string aggregateId)
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // table name is fixture-owned, not user input
		command.CommandText = $"SELECT TENANTID FROM {_fixture.TableName} WHERE AGGREGATEID = :aggregateId AND ROWNUM = 1";
#pragma warning restore CA2100
		_ = command.Parameters.Add(new OracleParameter("aggregateId", aggregateId));

		var tenantId = await command.ExecuteScalarAsync().ConfigureAwait(false);
		return tenantId as string ?? throw new InvalidOperationException("the seeded row carries no tenant term");
	}

	private async Task<int> CountRowsAtVersionAsync(string aggregateId, long version)
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
#pragma warning disable CA2100
		command.CommandText = $"SELECT COUNT(*) FROM {_fixture.TableName} WHERE AGGREGATEID = :aggregateId AND VERSION = :version";
#pragma warning restore CA2100
		_ = command.Parameters.Add(new OracleParameter("aggregateId", aggregateId));
		_ = command.Parameters.Add(new OracleParameter("version", version));

		return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
	}

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // DDL assembled from fixture-owned identifiers and test constants only.
		command.CommandText = sql;
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
