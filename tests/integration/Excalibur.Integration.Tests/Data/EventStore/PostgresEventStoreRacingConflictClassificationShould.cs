// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-Postgres regression lock for the one append the optimistic pre-check cannot catch: a writer
/// that passed the pre-check and then lost the version to a writer that committed first.
/// </summary>
/// <remarks>
/// <para>
/// The pre-check rejects a stale expected version before any row is written, and that is what hid this
/// path from ordinary use while the classification underneath it was wrong. Only a genuinely concurrent
/// writer, arriving between the pre-check and the insert, reaches the failure handler at all. Until this
/// lock nothing exercised it, and the loser was reported as an opaque failure with the conflict flag
/// false -- so a caller's reload-and-retry policy, which keys on that flag, surfaced an error for an
/// ordinary and expected outcome.
/// </para>
/// <para>
/// The interleaving is CONSTRUCTED rather than raced, because a race that depends on machine timing
/// proves nothing on the run where it does not happen. A separate connection writes the contested
/// version and holds it uncommitted; the store's pre-check reads committed state and therefore passes;
/// the store's insert then blocks on the uncommitted key. Only once the store is OBSERVED waiting on
/// that lock is the winner committed, which forces the loser down the exact path under test. If the
/// wait is never observed the test fails rather than passing on an interleaving that did not occur --
/// a pass without the wait would be the pre-check answering, not the classifier.
/// </para>
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresEventStoreRacingConflictClassificationShould : IClassFixture<PostgresEventStoreContainerFixture>
{
	private readonly PostgresEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStoreRacingConflictClassificationShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres container fixture.</param>
	public PostgresEventStoreRacingConflictClassificationShould(PostgresEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task AppendThatLosesTheVersionAfterItsPreCheckPassed_IsReportedAsAConcurrencyConflict()
	{
		// Real Postgres is mandatory: the behaviour under test is the server refusing the second writer,
		// which no mock reproduces -- a mocked driver returns whatever it was told to return.
		_fixture.DockerAvailable.ShouldBeTrue("the racing-conflict classification lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = new PostgresEventStore(
			_fixture.ConnectionString, NullLogger<PostgresEventStore>.Instance, SingleTenantTestContext.Instance);

		var aggregateId = Guid.NewGuid().ToString();
		const string AggregateType = "RacingConflictAggregate";

		var seed = await store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);
		seed.Success.ShouldBeTrue("the seed append establishes the version both writers will contend for");

		// The tenant term is read back from the seeded row rather than assumed, so the winner's row lands
		// on the same stream identity the store's own key uses.
		var tenantId = await ReadSeededTenantIdAsync(aggregateId, AggregateType).ConfigureAwait(false);

		await using var winner = new NpgsqlConnection(_fixture.ConnectionString);
		await winner.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		await using var winnerTransaction = await winner.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);
		await InsertContestedVersionAsync(winner, winnerTransaction, aggregateId, AggregateType, tenantId).ConfigureAwait(false);

		// The loser's pre-check reads COMMITTED state, so it does not see the row above and passes; its
		// insert then blocks on the winner's uncommitted key. Deliberately not awaited yet -- it has to be
		// in flight for the interleaving to exist.
		var loserAppend = store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateEvent(aggregateId) },
			expectedVersion: 0,
			CancellationToken.None).AsTask();

		var blocked = await WaitForABackendToBlockOnALockAsync().ConfigureAwait(false);
		blocked.ShouldBeTrue(
			"the append must be observed waiting on the winner's key before the winner commits; without that "
			+ "wait the pre-check would have rejected it and this run would prove nothing about classification");

		await winnerTransaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

		var loser = await loserAppend.ConfigureAwait(false);

		loser.Success.ShouldBeFalse("the contested version was already taken by the committed writer");
		loser.IsConcurrencyConflict.ShouldBeTrue(
			"an append that lost its version to another writer is a concurrency conflict whatever the engine "
			+ "raised; reported as an opaque failure, the caller's reload-and-retry policy never fires");
		loser.NextExpectedVersion.ShouldBe(1, "the conflict reports the winner's version, which is what makes it actionable");

		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private static TestDomainEvent CreateEvent(string aggregateId) => new()
	{
		EventId = Guid.NewGuid().ToString(),
		AggregateId = aggregateId,
		OccurredAt = DateTimeOffset.UtcNow,
		Data = "TestData-" + Guid.NewGuid().ToString("N"),
	};

	private static async Task InsertContestedVersionAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction transaction,
		string aggregateId,
		string aggregateType,
		string tenantId)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText =
			"INSERT INTO public.events "
			+ "(event_id, aggregate_id, aggregate_type, event_type, event_data, metadata, version, timestamp, tenant_id) "
			+ "VALUES (@eventId, @aggregateId, @aggregateType, 'TestDomainEvent', @eventData, NULL, 1, @timestamp, @tenantId)";
		_ = command.Parameters.AddWithValue("eventId", Guid.NewGuid().ToString());
		_ = command.Parameters.AddWithValue("aggregateId", aggregateId);
		_ = command.Parameters.AddWithValue("aggregateType", aggregateType);
		_ = command.Parameters.AddWithValue("eventData", System.Text.Encoding.UTF8.GetBytes("{}"));
		_ = command.Parameters.AddWithValue("timestamp", DateTimeOffset.UtcNow);
		_ = command.Parameters.AddWithValue("tenantId", tenantId);

		_ = await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
	}

	private async Task<string> ReadSeededTenantIdAsync(string aggregateId, string aggregateType)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText =
			"SELECT tenant_id FROM public.events WHERE aggregate_id = @aggregateId AND aggregate_type = @aggregateType LIMIT 1";
		_ = command.Parameters.AddWithValue("aggregateId", aggregateId);
		_ = command.Parameters.AddWithValue("aggregateType", aggregateType);

		var tenantId = await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
		return tenantId as string ?? throw new InvalidOperationException("the seeded row carries no tenant term");
	}

	/// <summary>
	/// Waits until the server reports a backend waiting on a lock, which is the loser's insert queued
	/// behind the winner's uncommitted key.
	/// </summary>
	/// <returns><see langword="true"/> once a waiting backend is observed; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// Polls the server rather than sleeping a guessed interval: a sleep either releases early -- leaving
	/// the pre-check to answer instead of the classifier -- or lengthens the suite for no gain. Reporting
	/// <see langword="false"/> on expiry lets the caller fail loudly rather than assert against an
	/// interleaving that never happened.
	/// </remarks>
	private async Task<bool> WaitForABackendToBlockOnALockAsync()
	{
		var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		while (DateTimeOffset.UtcNow < deadline)
		{
			await using (var command = connection.CreateCommand())
			{
				command.CommandText =
					"SELECT count(*) FROM pg_stat_activity "
					+ "WHERE wait_event_type = 'Lock' AND state = 'active' AND datname = current_database()";

				var waiting = (long?)await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false) ?? 0;
				if (waiting > 0)
				{
					return true;
				}
			}

			await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None).ConfigureAwait(false);
		}

		return false;
	}
}
