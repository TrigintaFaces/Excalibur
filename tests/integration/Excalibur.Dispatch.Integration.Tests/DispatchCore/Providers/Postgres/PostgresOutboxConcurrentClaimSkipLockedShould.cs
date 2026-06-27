// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Outbox.Postgres;

using Npgsql;

using OutboxMessage = Excalibur.Dispatch.Delivery.OutboxMessage;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Author≠impl regression lock for bd-d7ubqx (MS-A3): the Postgres outbox claim CTE
/// (<see cref="ReserveOutboxMessages"/>) must use <c>FOR UPDATE SKIP LOCKED</c> so two concurrent
/// dispatchers never claim overlapping rows (no double-dispatch).
/// </summary>
/// <remarks>
/// <para>
/// Non-vacuity (RED on the pre-fix code): the pre-fix CTE was a plain <c>SELECT … LIMIT</c> with NO row
/// lock. With dispatcher A holding an OPEN (uncommitted) reservation transaction over the first batch,
/// dispatcher B's snapshot still sees those rows as eligible (<c>dispatcher_id IS NULL</c>), so B's
/// un-locked CTE selects the SAME rows; B's UPDATE then blocks on A's row locks and, after A commits,
/// updates+returns the very rows A already claimed — the union of the two returned id-sets OVERLAPS
/// (double-dispatch). With <c>FOR UPDATE SKIP LOCKED</c>, B's CTE skips the rows A has locked and claims
/// a DISJOINT batch, so the union has no duplicates. This test drives the exact production SQL
/// (<c>ReserveOutboxMessages.Command.CommandText</c>) on two real connections with A's claim held in an
/// open transaction — the deterministic lock-window that distinguishes the two implementations.
/// </para>
/// <para>Serial (-m:1, real Postgres via TestContainers). Per-test isolation via a unique table name.</para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Component, TestComponents.Outbox)]
[Trait("Database", "Postgres")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PostgresOutboxConcurrentClaimSkipLockedShould : IntegrationTestBase
{
	private const int ReservationTimeoutMs = 300_000;
	private const int SqlTimeoutSeconds = 30;

	private readonly PostgresFixture _pgFixture;
	private readonly string _tableName = $"outbox_skiplock_{Guid.NewGuid():N}";

	public PostgresOutboxConcurrentClaimSkipLockedShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	[Fact]
	public async Task ClaimDisjointBatchesWhenTwoDispatchersReserveConcurrently()
	{
		// Arrange — seed 10 eligible rows with strictly ordered occurred_on so ORDER BY is deterministic.
		await CreateTableAsync();
		const int totalRows = 10;
		const int batchSize = 5;
		await SeedEligibleRowsAsync(totalRows);

		await using var connA = new NpgsqlConnection(_pgFixture.ConnectionString);
		await using var connB = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connA.OpenAsync(TestCancellationToken);
		await connB.OpenAsync(TestCancellationToken);

		// Dispatcher A claims the first batch inside an OPEN transaction and HOLDS the row locks.
		await using var txA = await connA.BeginTransactionAsync(TestCancellationToken);
		var batchA = await ReserveAsync(connA, txA, "dispatcher-A", batchSize);
		batchA.Count.ShouldBe(batchSize, "dispatcher A must claim its full batch");

		// Dispatcher B claims concurrently (autocommit). FOR UPDATE SKIP LOCKED makes this return a
		// DISJOINT batch immediately; the pre-fix no-lock CTE would block here until A commits, then
		// return the rows A already holds.
		var batchBTask = Task.Run(
			() => ReserveAsync(connB, transaction: null, "dispatcher-B", totalRows),
			TestCancellationToken);

		// Let B's statement get in flight, then release A so a pre-fix (blocking) B can also complete.
		await Task.Delay(250, TestCancellationToken);
		await txA.CommitAsync(TestCancellationToken);

		var batchB = await batchBTask;

		// Assert — the two claimed id-sets are disjoint; no message is claimed by both dispatchers.
		batchB.ShouldNotBeEmpty("dispatcher B must claim the rows A did not lock");

		var overlap = batchA.Intersect(batchB, StringComparer.Ordinal).ToList();
		overlap.ShouldBeEmpty(
			$"no message_id may be claimed by both dispatchers; overlap=[{string.Join(",", overlap)}]");

		var union = batchA.Concat(batchB).ToList();
		union.Count.ShouldBe(union.Distinct(StringComparer.Ordinal).Count(),
			"the union of both dispatchers' claimed ids must contain no duplicates");
	}

	private async Task<List<string>> ReserveAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction? transaction,
		string dispatcherId,
		int batchSize)
	{
		// Drive the EXACT production claim SQL (ReserveOutboxMessages) so the lock binds the shipped CTE.
		var request = new ReserveOutboxMessages(
			dispatcherId,
			batchSize,
			ReservationTimeoutMs,
			_tableName,
			SqlTimeoutSeconds,
			TestCancellationToken);

		var command = new CommandDefinition(
			request.Command.CommandText,
			request.Parameters,
			transaction,
			SqlTimeoutSeconds,
			cancellationToken: TestCancellationToken);

		var rows = await connection.QueryAsync<OutboxMessage>(command).ConfigureAwait(false);
		return rows.Select(static r => r.MessageId).ToList();
	}

	private async Task CreateTableAsync()
	{
		var createSql = $"""
			CREATE TABLE IF NOT EXISTS {_tableName} (
			    id SERIAL PRIMARY KEY,
			    message_id VARCHAR(100) NOT NULL UNIQUE,
			    message_type VARCHAR(500) NOT NULL,
			    message_metadata TEXT,
			    message_body TEXT NOT NULL,
			    occurred_on TIMESTAMPTZ NOT NULL DEFAULT NOW(),
			    attempts INT NOT NULL DEFAULT 0,
			    dispatcher_id VARCHAR(100),
			    dispatcher_timeout TIMESTAMPTZ,
			    next_attempt_at TIMESTAMPTZ
			);
			""";

		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(createSql);
	}

	private async Task SeedEligibleRowsAsync(int count)
	{
		const string insertSql = """
			INSERT INTO {0} (message_id, message_type, message_metadata, message_body, occurred_on, attempts)
			VALUES (@MessageId, 'TestMessage', '{{}}', '{{"data":"x"}}', @OccurredOn, 0);
			""";

		var baseTime = DateTimeOffset.UtcNow.AddMinutes(-count);

		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		for (var i = 0; i < count; i++)
		{
			_ = await connection.ExecuteAsync(
				string.Format(System.Globalization.CultureInfo.InvariantCulture, insertSql, _tableName),
				new { MessageId = Guid.NewGuid().ToString(), OccurredOn = baseTime.AddSeconds(i) });
		}
	}
}
