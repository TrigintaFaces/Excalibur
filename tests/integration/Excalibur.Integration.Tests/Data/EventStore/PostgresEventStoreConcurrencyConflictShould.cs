// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-Postgres regression lock: when two concurrent appends race the SAME
/// (aggregate_id, aggregate_type, version) tuple, the UNIQUE-constraint loser must be
/// classified as a <em>concurrency conflict</em> (<see cref="AppendResult.IsConcurrencyConflict"/>),
/// not a generic failure.
/// </summary>
/// <remarks>
/// This exercises the path where the optimistic pre-check passes for both racers (both read the
/// same current version) but only one INSERT can satisfy the UNIQUE
/// (aggregate_id, aggregate_type, version) index. The losing racer hits a Postgres
/// unique-violation at COMMIT/INSERT time — which must surface as a concurrency conflict so the
/// repository's retry path (keyed on <see cref="AppendResult.IsConcurrencyConflict"/>) can re-load
/// and retry, rather than failing hard.
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresEventStoreConcurrencyConflictShould : IClassFixture<PostgresEventStoreContainerFixture>
{
	private readonly PostgresEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStoreConcurrencyConflictShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres container fixture.</param>
	public PostgresEventStoreConcurrencyConflictShould(PostgresEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ConcurrentAppend_SameAggregateAndVersion_LoserIsClassifiedAsConcurrencyConflict()
	{
		// Arrange — real Postgres is mandatory; this lock is never skipped (the bug is server-side:
		// the UNIQUE-constraint violation only happens against real Postgres, never a mock).
		_fixture.DockerAvailable.ShouldBeTrue("real-Postgres concurrency lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = new PostgresEventStore(_fixture.ConnectionString, NullLogger<PostgresEventStore>.Instance);

		var aggregateId = Guid.NewGuid().ToString();
		const string aggregateType = "ConcurrencyConflictAggregate";

		// Seed v0 so BOTH racers target the same existing expected version (0). With the stream
		// already at version 0, both racers pre-check current==0 (pass) and both attempt to INSERT
		// version 1 — only one INSERT can satisfy the UNIQUE (aggregate_id, aggregate_type, version)
		// index, forcing the other into a unique-violation.
		var seedEvent = CreateEvent(aggregateId);
		var seedResult = await store.AppendAsync(
			aggregateId,
			aggregateType,
			new IDomainEvent[] { seedEvent },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);
		seedResult.Success.ShouldBeTrue("seed append must succeed to establish version 0");

		// Distinct event payloads per racer so both rows would be valid but for the version clash.
		var racerA = CreateEvent(aggregateId);
		var racerB = CreateEvent(aggregateId);

		// Act — fire two concurrent appends, both expecting version 0 (i.e. both write version 1).
		var taskA = Task.Run(async () => await store.AppendAsync(
			aggregateId,
			aggregateType,
			new IDomainEvent[] { racerA },
			expectedVersion: 0,
			CancellationToken.None).ConfigureAwait(false));

		var taskB = Task.Run(async () => await store.AppendAsync(
			aggregateId,
			aggregateType,
			new IDomainEvent[] { racerB },
			expectedVersion: 0,
			CancellationToken.None).ConfigureAwait(false));

		var results = await Task.WhenAll(taskA, taskB).ConfigureAwait(false);

		// Assert — exactly one winner, and the loser is a CONCURRENCY CONFLICT (the regression):
		// pre-fix the generic catch returned CreateFailure (IsConcurrencyConflict == false), losing
		// the retry signal; post-fix the UniqueViolation catch returns CreateConcurrencyConflict.
		var successes = results.Count(r => r.Success);
		successes.ShouldBe(1, "exactly one concurrent append may win the UNIQUE (aggregate_id, aggregate_type, version) race");

		var loser = results.Single(r => !r.Success);
		loser.IsConcurrencyConflict.ShouldBeTrue(
			"the UNIQUE-violation loser must be classified as a concurrency conflict (not a generic failure) so the retry path engages");

		// Cleanup
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private static TestDomainEvent CreateEvent(string aggregateId) => new()
	{
		EventId = Guid.NewGuid().ToString(),
		AggregateId = aggregateId,
		OccurredAt = DateTimeOffset.UtcNow,
		Data = $"TestData-{Guid.NewGuid():N}",
	};
}
