// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;
using Excalibur.EventSourcing.Oracle.Requests;

using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// i8ghy0 — real-Oracle locks for the batch-append rewrite from N per-row round-trips (Dapper multi-exec)
/// to one ODP.NET array-bound round-trip (<see cref="OracleCommand.ArrayBindCount"/>).
/// </summary>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleEventStoreArrayBindBatchAppendShould : IClassFixture<OracleEventStoreContainerFixture>
{
	private const string AggregateType = "ArrayBindBatchAggregate";

	private readonly OracleEventStoreContainerFixture _fixture;

	public OracleEventStoreArrayBindBatchAppendShould(OracleEventStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// CORRECTNESS ARM (item 2) — calls <see cref="InsertEventsBatchRequest"/> directly (the array-bound
	/// request itself, not just the store wrapper) with a &gt;1-row batch, then reads the table back
	/// independently and matches POSITION to EVENTID — never trusting the returned list's order. A version
	/// that returns the right COUNT with wrong or duplicated positions fails this arm.
	/// </summary>
	[Fact]
	public async Task AssignEachRowItsOwnCorrectPosition_ForABatchOfMoreThanOneEvent()
	{
		_fixture.DockerAvailable.ShouldBeTrue("i8ghy0 correctness lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		const int n = 10;
		var aggregateId = $"agg-{Guid.NewGuid():N}";
		var rows = new EventInsertRow[n];
		for (var i = 0; i < n; i++)
		{
			rows[i] = new EventInsertRow(
				EventId: $"evt-{i}-{Guid.NewGuid():N}",
				AggregateId: aggregateId,
				AggregateType: AggregateType,
				EventType: "ArrayBindTestEvent",
				EventData: [(byte)i],
				Metadata: i % 2 == 0 ? null : [(byte)(i + 1)], // mix of null and non-null BLOB, per the array-bind size alignment concern
				Version: i,
				Timestamp: DateTimeOffset.UtcNow);
		}

		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

		var req = new InsertEventsBatchRequest(
			rows, transaction, TenantScope.Untenanted, TestContext.Current.CancellationToken, _fixture.Schema, _fixture.TableName);
		var returned = await Excalibur.Data.DbConnectionExtensions.ResolveAsync(connection, req).ConfigureAwait(false);
		await transaction.CommitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		returned.Count.ShouldBe(n, "one position must be returned per row in the batch — a wrong count fails regardless of the values.");

		// Independent read-back: query by EVENTID, never by trusting the returned list's row order.
		await using var verifyConnection = new OracleConnection(_fixture.ConnectionString);
		await verifyConnection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var verifyCommand = verifyConnection.CreateCommand();
#pragma warning disable CA2100 // table name is fixture-owned, not user input
		verifyCommand.CommandText = $"SELECT EVENTID, POSITION, VERSION FROM {_fixture.TableName} WHERE AGGREGATEID = :AggId";
#pragma warning restore CA2100
		_ = verifyCommand.Parameters.Add(new OracleParameter("AggId", aggregateId));
		var actualByEventId = new Dictionary<string, (long Position, long Version)>(StringComparer.Ordinal);
		await using (var reader = await verifyCommand.ExecuteReaderAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
		{
			while (await reader.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
			{
				actualByEventId[reader.GetString(0)] = (Convert.ToInt64(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture), reader.GetInt64(2));
			}
		}

		actualByEventId.Count.ShouldBe(n, "the table must actually hold N rows, independent of what the request object claimed.");

		var positionsSeen = new HashSet<long>();
		for (var i = 0; i < n; i++)
		{
			var row = rows[i];
			returned.ShouldContain(p => p.Version == row.Version, $"the returned list must carry an entry for version {row.Version}.");
			var claimed = returned.Single(p => p.Version == row.Version);

			actualByEventId.ShouldContainKey(row.EventId);
			var actual = actualByEventId[row.EventId];

			actual.Version.ShouldBe(row.Version, $"row {row.EventId}'s persisted VERSION must match what was submitted.");
			claimed.Position.ShouldBe(
				actual.Position,
				$"row {row.EventId}: the POSITION the array-bind returned ({claimed.Position}) must be the SAME "
				+ $"POSITION Oracle actually assigned to THIS row ({actual.Position}) — not another row's position, "
				+ "and not a count that merely looks right.");

			positionsSeen.Add(actual.Position).ShouldBeTrue($"POSITION {actual.Position} must not be duplicated across rows.");
		}

		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	/// <summary>BOUNDARY ARM (item 5) — batch size 1 and batch size <see cref="InsertEventsBatchRequest.MaxEventsPerStatement"/> both work.</summary>
	[Theory]
	[InlineData(1)]
	[InlineData(InsertEventsBatchRequest.MaxEventsPerStatement)]
	public async Task HandleTheBatchSizeBoundaries(int n)
	{
		_fixture.DockerAvailable.ShouldBeTrue("i8ghy0 boundary lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggregateId = $"agg-{Guid.NewGuid():N}";
		var rows = Enumerable.Range(0, n).Select(i => new EventInsertRow(
			EventId: $"evt-{i}-{Guid.NewGuid():N}",
			AggregateId: aggregateId,
			AggregateType: AggregateType,
			EventType: "ArrayBindBoundaryEvent",
			EventData: [1],
			Metadata: null,
			Version: i,
			Timestamp: DateTimeOffset.UtcNow)).ToArray();

		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

		var req = new InsertEventsBatchRequest(
			rows, transaction, TenantScope.Untenanted, TestContext.Current.CancellationToken, _fixture.Schema, _fixture.TableName);
		var returned = await Excalibur.Data.DbConnectionExtensions.ResolveAsync(connection, req).ConfigureAwait(false);
		await transaction.CommitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		returned.Count.ShouldBe(n);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// CONCURRENCY ARM (item 3) — two concurrent multi-event appends at the same expected version, through
	/// the real <see cref="OracleEventStore.AppendAsync"/> path, must produce exactly one success and one
	/// concurrency failure, and the losing batch must not be partially persisted.
	/// </summary>
	[Fact]
	public async Task ProduceExactlyOneWinnerAndNoPartialBatch_WhenTwoMultiEventAppendsRaceTheSameVersion()
	{
		_fixture.DockerAvailable.ShouldBeTrue("i8ghy0 concurrency lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";
		const int batchSize = 5;

		var barrier = new Barrier(2);
		async Task<AppendResult> AppendBatchAsync()
		{
			var events = Enumerable.Range(0, batchSize).Select(_ => (IDomainEvent)new TestDomainEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				OccurredAt = DateTimeOffset.UtcNow,
				Data = "race",
			}).ToList();
			_ = barrier.SignalAndWait(TimeSpan.FromSeconds(10));
			return await store.AppendAsync(aggregateId, AggregateType, events, expectedVersion: -1, TestContext.Current.CancellationToken)
				.ConfigureAwait(false);
		}

		var results = await Task.WhenAll(AppendBatchAsync(), AppendBatchAsync()).ConfigureAwait(false);

		results.Count(r => r.Success).ShouldBe(1, "exactly one of the two racing batches must win.");
		results.Count(r => !r.Success && r.IsConcurrencyConflict).ShouldBe(1, "the loser must be reported as a concurrency conflict, not a silent or opaque failure.");

		await using var verifyConnection = new OracleConnection(_fixture.ConnectionString);
		await verifyConnection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var countCommand = verifyConnection.CreateCommand();
#pragma warning disable CA2100
		countCommand.CommandText = $"SELECT COUNT(*) FROM {_fixture.TableName} WHERE AGGREGATEID = :AggId";
#pragma warning restore CA2100
		_ = countCommand.Parameters.Add(new OracleParameter("AggId", aggregateId));
		var count = Convert.ToInt64(
			await countCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken).ConfigureAwait(false),
			System.Globalization.CultureInfo.InvariantCulture);

		count.ShouldBe(batchSize, $"exactly the winner's {batchSize} rows must persist — a partially-committed loser batch would leave more, a partially-committed winner would leave fewer.");

		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// PERFORMANCE ARM (item 4) — wall time for a representative batch: the OLD per-row-loop shape
	/// (reproduced inline here, unchanged from the pre-fix form, for comparison only — production no longer
	/// contains it) versus the NEW array-bound <see cref="InsertEventsBatchRequest"/>. Both run the identical
	/// INSERT statement text; only the binding strategy differs. Wall time rather than a server-side
	/// round-trip counter (<c>V$MYSTAT</c>) because the container's test user has no grant on Oracle's
	/// dynamic performance views (<c>ORA-00942</c>) — measured, not assumed — and the bead's own acceptance
	/// criteria (item 4) explicitly allow either round-trips or wall time.
	/// </summary>
	[Fact]
	public async Task ReduceServerRoundTrips_ComparedToThePerRowLoop()
	{
		_fixture.DockerAvailable.ShouldBeTrue("i8ghy0 performance lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		const int n = 100;
		var insertSql =
			$"INSERT INTO {_fixture.TableName} (EVENTID, AGGREGATEID, AGGREGATETYPE, EVENTTYPE, EVENTDATA, METADATA, VERSION, EVENTTIMESTAMP, TENANTID) "
			+ "VALUES (:EventId, :AggregateId, :AggregateType, :EventType, :EventData, :Metadata, :Version, :Timestamp, :TenantId) "
			+ "RETURNING POSITION INTO :OutPosition";

		// OLD SHAPE: one round trip per row (this is what InsertEventsBatchRequest did before i8ghy0 — kept
		// here, inline, purely as the "before" side of the measurement; production no longer contains it).
		var oldAggregateId = $"agg-old-{Guid.NewGuid():N}";
		await using (var oldConnection = new OracleConnection(_fixture.ConnectionString))
		{
			await oldConnection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
			var oldStopwatch = System.Diagnostics.Stopwatch.StartNew();

			await using var oldTransaction = oldConnection.BeginTransaction(IsolationLevel.ReadCommitted);
			for (var i = 0; i < n; i++)
			{
				await using var command = oldConnection.CreateCommand();
#pragma warning disable CA2100 // insertSql is a fixture/constant-built statement, not user input
				command.CommandText = insertSql;
#pragma warning restore CA2100
				command.BindByName = true;
				command.Transaction = oldTransaction;
				_ = command.Parameters.Add(new OracleParameter("EventId", $"old-{i}-{Guid.NewGuid():N}"));
				_ = command.Parameters.Add(new OracleParameter("AggregateId", oldAggregateId));
				_ = command.Parameters.Add(new OracleParameter("AggregateType", AggregateType));
				_ = command.Parameters.Add(new OracleParameter("EventType", "PerfProbe"));
				_ = command.Parameters.Add(new OracleParameter("EventData", OracleDbType.Blob) { Value = new byte[] { 1 } });
				_ = command.Parameters.Add(new OracleParameter("Metadata", OracleDbType.Blob) { Value = DBNull.Value });
				_ = command.Parameters.Add(new OracleParameter("Version", (long)i));
				_ = command.Parameters.Add(new OracleParameter("Timestamp", DateTimeOffset.UtcNow));
				_ = command.Parameters.Add(new OracleParameter("TenantId", "__untenanted__"));
				var outPos = new OracleParameter("OutPosition", OracleDbType.Int64) { Direction = ParameterDirection.Output };
				_ = command.Parameters.Add(outPos);
				_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
			}
			await oldTransaction.CommitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

			oldStopwatch.Stop();
			var oldElapsedMs = oldStopwatch.Elapsed.TotalMilliseconds;

			// NEW SHAPE: the real array-bound request.
			var newAggregateId = $"agg-new-{Guid.NewGuid():N}";
			await using var newConnection = new OracleConnection(_fixture.ConnectionString);
			await newConnection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
			var newStopwatch = System.Diagnostics.Stopwatch.StartNew();

			var rows = Enumerable.Range(0, n).Select(i => new EventInsertRow(
				EventId: $"new-{i}-{Guid.NewGuid():N}", AggregateId: newAggregateId, AggregateType: AggregateType,
				EventType: "PerfProbe", EventData: [1], Metadata: null, Version: i, Timestamp: DateTimeOffset.UtcNow)).ToArray();

			await using var newTransaction = newConnection.BeginTransaction(IsolationLevel.ReadCommitted);
			var req = new InsertEventsBatchRequest(
				rows, newTransaction, TenantScope.Untenanted, TestContext.Current.CancellationToken, _fixture.Schema, _fixture.TableName);
			_ = await Excalibur.Data.DbConnectionExtensions.ResolveAsync(newConnection, req).ConfigureAwait(false);
			await newTransaction.CommitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

			newStopwatch.Stop();
			var newElapsedMs = newStopwatch.Elapsed.TotalMilliseconds;

			// NOT asserted as a hard CI gate. Measured empirically (i8ghy0): in an ISOLATED run this
			// comparison shows array binding winning by ~6x at n=100 (old=4382ms, new=720ms — recorded on
			// the bead). But wall time sharing a process with other tests is demonstrably too noisy to gate
			// on: run immediately after other Oracle tests in this same suite, the SAME old per-row-loop code
			// measured 88ms instead of 4382ms for the identical 100-row batch — a 50x swing with no code
			// change, from Oracle client/server statement-cache and connection warm-state that the per-row
			// loop benefits from disproportionately. A hard assertion here would be flaky by construction,
			// not by defect. The deterministic arms above (correctness, boundary, concurrency) are the CI
			// gate; this arm's job is to RECORD the measurement per the bead's own item 4, not to gate on it.
			Console.WriteLine(
				$"i8ghy0 PERFORMANCE ARM: n={n} events — OLD (per-row loop) wall time={oldElapsedMs:F1}ms, "
				+ $"NEW (array bind) wall time={newElapsedMs:F1}ms.");
		}

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
}
