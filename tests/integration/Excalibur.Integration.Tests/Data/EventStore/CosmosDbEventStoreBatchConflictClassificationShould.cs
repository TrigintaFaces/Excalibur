// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-emulator lock on the append path that reports its outcome instead of throwing it.
/// </summary>
/// <remarks>
/// <para>
/// An append of more than one event goes through a transactional batch by default, and executing a batch
/// RETURNS a response carrying the failure rather than raising it. The store's conflict handler catches a
/// thrown conflict, which is what the single-event path produces, so a batched one never reached it: the
/// loser came back as an opaque failure whose text happened to contain the word the caller needed while
/// the flag the caller actually branches on stayed false. Since batching is the default for any append of
/// more than one event, that was the ordinary path, not a corner of it.
/// </para>
/// <para>
/// The collision is CONSTRUCTED rather than raced, because a race decided by emulator timing proves
/// nothing on the run where it does not happen. A document is planted on the contested identifier and
/// left out of the version sequence, so it collides with the batch exactly as a concurrent writer's
/// document would while the version probe still reports the stream where the caller expects it -- which
/// is what puts the append past its pre-check and into the batch. The second arm then races the same
/// path for real and reports what it observed either way, since a race that did not occur is not
/// evidence and must not be presented as any.
/// </para>
/// </remarks>
[Collection(CosmosDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbEventStoreBatchConflictClassificationShould
	: IClassFixture<CosmosDbEventStoreContainerFixture>, IAsyncLifetime
{
	private const string AggregateType = "BatchConflictAggregate";

	private readonly CosmosDbEventStoreContainerFixture _fixture;
	private readonly string _containerName = "events_" + Guid.NewGuid().ToString("N");

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbEventStoreBatchConflictClassificationShould"/> class.
	/// </summary>
	/// <param name="fixture">The Cosmos DB emulator fixture.</param>
	public CosmosDbEventStoreBatchConflictClassificationShould(CosmosDbEventStoreContainerFixture fixture)
		=> _fixture = fixture;

	/// <inheritdoc/>
	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the batch-conflict classification lock exercises a status the emulator itself returns, which no "
			+ "mock reproduces; it is never skipped");

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		try
		{
			_ = await _fixture.Client.GetContainer(_fixture.DatabaseName, _containerName)
				.DeleteContainerAsync().ConfigureAwait(false);
		}
		catch (CosmosException)
		{
			// The container was never created, or is already gone; nothing to clean up.
		}
	}

	[Fact]
	public async Task ReportABatchedLostRaceAsAConcurrencyConflict_NotAsAnOpaqueFailure()
	{
		await using var store = CreateStore();
		var eventStore = (IEventStore)store;

		var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

		var seed = await eventStore.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);
		seed.Success.ShouldBeTrue("the seed append fixes the stream at the version the batch will target");

		// Read the stream identifier back from the document the store itself wrote, rather than
		// reconstructing it here: the composition is the store's business and a test that mirrors it can
		// agree with a mistake.
		var streamId = await ReadSeededStreamIdAsync(aggregateId).ConfigureAwait(false);

		// The contested identifier, occupied. The document carries no version, so the version probe -- an
		// aggregate over that property -- does not count it and still reports the stream at 0. That is what
		// lets the append past its pre-check and into the batch, which is the path under test.
		var container = _fixture.Client.GetContainer(_fixture.DatabaseName, _containerName);
		_ = await container.CreateItemAsync<object>(
			new { id = streamId + ":1", streamId },
			new Microsoft.Azure.Cosmos.PartitionKey(streamId)).ConfigureAwait(false);

		// Two events, so the append batches. Both target versions the pre-check believes are free.
		var loser = await eventStore.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateEvent(aggregateId), CreateEvent(aggregateId) },
			expectedVersion: 0,
			CancellationToken.None).ConfigureAwait(false);

		loser.Success.ShouldBeFalse("the contested identifier was already taken");
		loser.IsConcurrencyConflict.ShouldBeTrue(
			"a batch refused on the contested identifier is a lost race; reported as an opaque failure, the "
			+ "caller's reload-and-retry policy never fires and an ordinary outcome surfaces as an error");
	}

	[Fact]
	public async Task ReportEveryLoserOfABatchedRaceAsAConcurrencyConflict()
	{
		await using var store = CreateStore();
		var eventStore = (IEventStore)store;

		var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

		var seed = await eventStore.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { CreateEvent(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);
		seed.Success.ShouldBeTrue("the seed append fixes the version every racer will contend for");

		const int Racers = 8;
		var appends = new List<Task<AppendResult>>(Racers);
		for (var i = 0; i < Racers; i++)
		{
			// Two events each, so every racer takes the batch path rather than the single-item one.
			appends.Add(eventStore.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { CreateEvent(aggregateId), CreateEvent(aggregateId) },
				expectedVersion: 0,
				CancellationToken.None).AsTask());
		}

		var results = await Task.WhenAll(appends).ConfigureAwait(false);

		// Safety first, and unconditionally: however the racers were scheduled, the stream may only have
		// been advanced once.
		results.Count(r => r.Success).ShouldBe(1, "exactly one racer may take the contested version");

		// Honesty, for whichever losers actually reached the batch. A racer whose pre-check ran after the
		// winner committed was rejected before writing anything, which is a conflict too -- so the
		// assertion holds either way, and does not depend on the schedule this run happened to get.
		results.Count(r => !r.Success && r.IsConcurrencyConflict).ShouldBe(
			Racers - 1,
			"every loser is a concurrency conflict, whether it was refused by the pre-check or by the batch");
	}

	private static TestDomainEvent CreateEvent(string aggregateId) => new()
	{
		EventId = Guid.NewGuid().ToString(),
		AggregateId = aggregateId,
		OccurredAt = DateTimeOffset.UtcNow,
		Data = "TestData-" + Guid.NewGuid().ToString("N"),
	};

	private CosmosDbEventStore CreateStore() =>
		new(
			_fixture.Client,
			Options.Create(new CosmosDbEventStoreOptions
			{
				DatabaseName = _fixture.DatabaseName,
				EventsContainerName = _containerName,

				// The default, stated here so the arm cannot be silently turned into a test of the
				// single-item path by a change to that default.
				UseTransactionalBatch = true,
			}),
			NullLogger<CosmosDbEventStore>.Instance,
			SingleTenantTestContext.Instance);

	private async Task<string> ReadSeededStreamIdAsync(string aggregateId)
	{
		var container = _fixture.Client.GetContainer(_fixture.DatabaseName, _containerName);
		var query = new QueryDefinition("SELECT VALUE c.streamId FROM c WHERE c.aggregateId = @aggregateId")
			.WithParameter("@aggregateId", aggregateId);

		using var iterator = container.GetItemQueryIterator<string>(query);
		while (iterator.HasMoreResults)
		{
			var page = await iterator.ReadNextAsync().ConfigureAwait(false);
			var streamId = page.FirstOrDefault();
			if (!string.IsNullOrEmpty(streamId))
			{
				return streamId;
			}
		}

		throw new InvalidOperationException("the seeded event carries no stream identifier");
	}
}
