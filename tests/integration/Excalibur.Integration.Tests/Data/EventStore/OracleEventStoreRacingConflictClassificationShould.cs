// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;

using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-Oracle regression lock for an append that loses its version to another writer and is refused with
/// something other than ORA-08177.
/// </summary>
/// <remarks>
/// <para>
/// The store has two paths that already report a conflict correctly: the pre-check, which rejects a stale
/// expected version before any row is written, and the bounded serializable retry, which re-runs the whole
/// unit of work after ORA-08177 and converges because the next attempt's pre-check sees the winner. Neither
/// covers the case here. A lost race does not always surface as ORA-08177 -- a unique-constraint violation
/// on the stream key, a deadlock victim, and a session cancelled while waiting on the winner's locks are
/// the same logical outcome under different codes, and none of them enters the retry loop at all, whose
/// filter admits ORA-08177 alone. Before the classifier existed those losers left the loop by raising and
/// were reported as opaque failures with the conflict flag false, so a caller's reload-and-retry policy --
/// which keys on that flag -- never fired for an ordinary, expected outcome.
/// </para>
/// <para>
/// The interleaving is CONSTRUCTED rather than raced, because a race that depends on machine timing proves
/// nothing on the run where it does not happen. A trigger installed for the duration of the test does, in
/// one deterministic step, exactly what a lost race does: it commits the contested version from a separate
/// transaction, and then refuses this writer with a code the retry filter does not admit. That is the
/// state the classifier exists to read -- nothing written by this append, and a stream that has moved past
/// the version the append required.
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleEventStoreRacingConflictClassificationShould : IClassFixture<OracleEventStoreContainerFixture>
{
	private const string AggregateType = "OracleRacingConflictAggregate";

	/// <summary>The event type the trigger writes for the winner, and the one it must not fire on again.</summary>
	private const string WinnerEventType = "RacingConflictWinner";

	private readonly OracleEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleEventStoreRacingConflictClassificationShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Oracle container fixture.</param>
	public OracleEventStoreRacingConflictClassificationShould(OracleEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task AppendRefusedWithANonRetryableCode_AfterTheStreamMoved_IsReportedAsAConcurrencyConflict()
	{
		// Real Oracle is mandatory: the behaviour under test is how the engine's own error reaches the store
		// and what the store then reads back from committed state. A mocked driver returns whatever it was
		// told to return, which is the answer being tested.
		_fixture.DockerAvailable.ShouldBeTrue("the racing-conflict classification lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		var seed = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[CreateEvent(aggregateId)],
			expectedVersion: -1,
			TestContext.Current.CancellationToken).ConfigureAwait(false);
		seed.Success.ShouldBeTrue("the seed append establishes the version both writers will contend for");

		// The tenant term is read back from the seeded row rather than assumed, so the winner's row lands on
		// the same stream identity the store's own key uses.
		var tenantId = await ReadSeededTenantIdAsync(aggregateId).ConfigureAwait(false);

		await InstallRaceTriggerAsync(commitTheWinner: true, tenantId).ConfigureAwait(false);
		try
		{
			var loser = await store.AppendAsync(
				aggregateId,
				AggregateType,
				[CreateEvent(aggregateId)],
				expectedVersion: 0,
				TestContext.Current.CancellationToken).ConfigureAwait(false);

			loser.Success.ShouldBeFalse("the contested version was taken by the writer that committed first");

			// CONTROL on the construction itself. If the trigger's autonomous winner never committed, the
			// stream never moved, and the conflict assertion below would be reporting a fixture that did not
			// build the state under test rather than a classifier that failed to read it.
			(await ReadCurrentVersionAsync(aggregateId).ConfigureAwait(false)).ShouldBe(
				1,
				"the trigger's autonomous winner must have committed version 1 before the append was refused; "
				+ "without it this arm is asserting against a race that never happened. The append reported: "
				+ loser.ErrorMessage);
			loser.IsConcurrencyConflict.ShouldBeTrue(
				"an append that lost its version to another writer is a concurrency conflict whatever the engine "
				+ "raised; reported as an opaque failure, the caller's reload-and-retry policy never fires");
			loser.NextExpectedVersion.ShouldBe(
				1, "the conflict reports the winner's version, which is what makes it actionable");
		}
		finally
		{
			await DropRaceTriggerAsync().ConfigureAwait(false);
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task AppendThatFailsOnItsOwnAccount_WhileTheStreamStandsStill_IsReportedAsAPlainFailure()
	{
		// LIVENESS. Without this arm the classifier could report every failed append as a conflict and the
		// arm above would still pass, which would turn every genuine store fault into a retry loop the caller
		// can never exit. Same trigger, same refusal code, one difference: no winner commits, so the stream
		// is still at the version the append required and the failure is the append's own.
		_fixture.DockerAvailable.ShouldBeTrue("the racing-conflict classification lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		var seed = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[CreateEvent(aggregateId)],
			expectedVersion: -1,
			TestContext.Current.CancellationToken).ConfigureAwait(false);
		seed.Success.ShouldBeTrue("the seed append establishes the version this writer will require");

		var tenantId = await ReadSeededTenantIdAsync(aggregateId).ConfigureAwait(false);

		await InstallRaceTriggerAsync(commitTheWinner: false, tenantId).ConfigureAwait(false);
		try
		{
			var failed = await store.AppendAsync(
				aggregateId,
				AggregateType,
				[CreateEvent(aggregateId)],
				expectedVersion: 0,
				TestContext.Current.CancellationToken).ConfigureAwait(false);

			failed.Success.ShouldBeFalse("the trigger refused the insert, so nothing was appended");
			failed.IsConcurrencyConflict.ShouldBeFalse(
				"the stream never left the version this append required, so nothing else claimed it: this is the "
				+ "append's own failure and reporting it as a conflict would send the caller into a reload-and-retry "
				+ "loop that can never succeed");
		}
		finally
		{
			await DropRaceTriggerAsync().ConfigureAwait(false);
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task AppendWithNoContention_StillSucceeds()
	{
		// LIVENESS. The classifier runs on the failure path only; this arm proves the failure path is not
		// reached at all when nothing goes wrong, so neither arm above can be satisfied by a store that has
		// stopped appending.
		_fixture.DockerAvailable.ShouldBeTrue("the racing-conflict classification lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		var seed = await store.AppendAsync(
			aggregateId, AggregateType, [CreateEvent(aggregateId)], expectedVersion: -1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		seed.Success.ShouldBeTrue(seed.ErrorMessage);

		var second = await store.AppendAsync(
			aggregateId, AggregateType, [CreateEvent(aggregateId)], expectedVersion: 0, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		second.Success.ShouldBeTrue(second.ErrorMessage);
		second.IsConcurrencyConflict.ShouldBeFalse("nothing contended this append");

		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private OracleEventStore CreateStore() =>
		new(
			() => new OracleConnection(_fixture.ConnectionString),
			NullLogger<OracleEventStore>.Instance,
			tenantContext: SingleTenantTestContext.Instance,
			payloadSerializer: null,
			schema: _fixture.Schema,
			table: _fixture.TableName);

	private static TestDomainEvent CreateEvent(string aggregateId) => new()
	{
		EventId = Guid.NewGuid().ToString(),
		AggregateId = aggregateId,
		OccurredAt = DateTimeOffset.UtcNow,
		Data = "TestData-" + Guid.NewGuid().ToString("N"),
	};

	/// <summary>
	/// Installs a BEFORE INSERT trigger that reproduces, deterministically, the state a lost race leaves
	/// behind.
	/// </summary>
	/// <param name="commitTheWinner">
	/// When <see langword="true"/> the trigger first commits the contested version from an autonomous
	/// transaction, so the stream has moved by the time the store re-reads it. When <see langword="false"/>
	/// it refuses without committing anything, so the stream is where the append left it.
	/// </param>
	/// <param name="tenantId">The tenant term the seeded row carries, so the winner lands on the same stream key.</param>
	/// <remarks>
	/// <para>
	/// The refusal is ORA-00060, a deadlock victim. That choice is the point of the arm rather than an
	/// arbitrary code: it is a real shape a lost race takes, and it is one the store's serializable-retry
	/// filter does not admit, so the append leaves the retry loop on its first attempt and reaches the
	/// classifier directly. Raising ORA-08177 instead would be answered by the retry, whose next attempt's
	/// pre-check reports the conflict -- a path that already worked and is not what this lock is for.
	/// </para>
	/// <para>
	/// The winner is written from a PRAGMA AUTONOMOUS_TRANSACTION block, which is what makes it a separate
	/// writer: it commits independently of the transaction that is about to be refused, exactly as a
	/// competing session would, and it survives that transaction's rollback. The guard on the event type
	/// stops the trigger firing on its own insert.
	/// </para>
	/// </remarks>
	private async Task InstallRaceTriggerAsync(bool commitTheWinner, string tenantId)
	{
		// PRAGMA AUTONOMOUS_TRANSACTION is legal on the trigger itself but NOT on a block nested inside
		// it, so the winner's independent commit is declared at trigger level and only when this arm needs
		// one. An autonomous trigger that raises rolls its own work back, which is why the COMMIT precedes
		// the refusal.
		var autonomous = commitTheWinner ? "	PRAGMA AUTONOMOUS_TRANSACTION;" : string.Empty;
		var winnerBlock = commitTheWinner
			? $"""
				INSERT INTO {_fixture.TableName}
					(EVENTID, AGGREGATEID, AGGREGATETYPE, EVENTTYPE, EVENTDATA, METADATA, VERSION, EVENTTIMESTAMP, TENANTID)
				VALUES
					(:NEW.EVENTID || '-winner', :NEW.AGGREGATEID, :NEW.AGGREGATETYPE, '{WinnerEventType}',
					 EMPTY_BLOB(), NULL, :NEW.VERSION, SYSTIMESTAMP, '{tenantId.Replace("'", "''", StringComparison.Ordinal)}');
				COMMIT;
				"""
			: "NULL;";

		var trigger = $"""
			CREATE OR REPLACE TRIGGER RACING_CONFLICT_TRG
			BEFORE INSERT ON {_fixture.TableName}
			FOR EACH ROW
			DECLARE
			{autonomous}
				deadlock_victim EXCEPTION;
				PRAGMA EXCEPTION_INIT(deadlock_victim, -60);
			BEGIN
				IF :NEW.AGGREGATETYPE <> '{AggregateType}' OR :NEW.EVENTTYPE = '{WinnerEventType}' THEN
					RETURN;
				END IF;

				IF :NEW.VERSION = 0 THEN
					RETURN;
				END IF;

				{winnerBlock}

				RAISE deadlock_victim;
			END;
			""";

		await ExecuteAsync(trigger).ConfigureAwait(false);

		// CREATE OR REPLACE TRIGGER succeeds even when the body does not compile -- Oracle stores the trigger
		// in an INVALID state and the failure only surfaces later, as ORA-04098 on the insert this arm is
		// measuring. Read back rather than trust the exit: an invalid trigger would otherwise look exactly
		// like a store that refused the append for its own reasons.
		await AssertTriggerCompiledAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Fails with the compiler's own diagnostics if the trigger was stored INVALID.
	/// </summary>
	private async Task AssertTriggerCompiledAsync()
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		await using (var status = connection.CreateCommand())
		{
			status.CommandText =
				"SELECT STATUS FROM USER_OBJECTS WHERE OBJECT_NAME = 'RACING_CONFLICT_TRG' AND OBJECT_TYPE = 'TRIGGER'";
			var value = await status.ExecuteScalarAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

			if (value as string == "VALID")
			{
				return;
			}
		}

		await using var errors = connection.CreateCommand();
		errors.CommandText =
			"SELECT LINE || ':' || POSITION || ' ' || TEXT FROM USER_ERRORS WHERE NAME = 'RACING_CONFLICT_TRG' ORDER BY SEQUENCE";

		var diagnostics = new List<string>();
		await using (var reader = await errors.ExecuteReaderAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
		{
			while (await reader.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
			{
				diagnostics.Add(reader.GetString(0));
			}
		}

		throw new InvalidOperationException(
			"The race trigger did not compile, so this arm would measure a broken fixture rather than the store: "
			+ string.Join(" | ", diagnostics));
	}

	private async Task DropRaceTriggerAsync()
	{
		try
		{
			await ExecuteAsync("DROP TRIGGER RACING_CONFLICT_TRG").ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number == 4080)
		{
			// ORA-04080: the trigger is already gone. Nothing to undo.
		}
	}

	private async Task<long> ReadCurrentVersionAsync(string aggregateId)
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // The table name comes from the fixture, not from caller input.
		command.CommandText =
			$"SELECT NVL(MAX(VERSION), -1) FROM {_fixture.TableName} WHERE AGGREGATEID = :aggregateId";
#pragma warning restore CA2100
		_ = command.Parameters.Add(new OracleParameter("aggregateId", aggregateId));

		var version = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		return Convert.ToInt64(version, System.Globalization.CultureInfo.InvariantCulture);
	}

	private async Task<string> ReadSeededTenantIdAsync(string aggregateId)
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // The table name comes from the fixture, not from caller input.
		command.CommandText =
			$"SELECT TENANTID FROM {_fixture.TableName} WHERE AGGREGATEID = :aggregateId AND ROWNUM = 1";
#pragma warning restore CA2100
		_ = command.Parameters.Add(new OracleParameter("aggregateId", aggregateId));

		var tenantId = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		return tenantId as string ?? throw new InvalidOperationException("the seeded row carries no tenant term");
	}

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // DDL assembled from fixture-owned identifiers and test constants only.
		command.CommandText = sql;
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}
}
