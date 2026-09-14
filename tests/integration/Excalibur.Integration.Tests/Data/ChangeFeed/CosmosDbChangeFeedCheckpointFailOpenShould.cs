// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;
using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Integration.Tests.Data.Saga;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

using Tests.Shared.Helpers;
using Tests.Shared.Infrastructure;

using CosmosPartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Excalibur.Integration.Tests.Data.ChangeFeed;

/// <summary>
/// Real-Cosmos-emulator, NON-SKIPPED lock for bead <c>Excalibur_Dispatch-kb1u1u</c> (independently
/// authored, per <c>verify-against-real-infra-not-mock</c> -- same class of proof as the sibling
/// <see cref="CosmosDbChangeFeedCheckpointStoreDurabilityShould"/>): the evttu3 fail-open ruling on
/// <see cref="CosmosDbChangeFeedSubscription{TDocument}"/> has a unit lock
/// (<c>ChangeFeedCheckpointFailureTrackerShould</c>) for the pure failure-counting decision, but nothing
/// exercises the SUBSCRIPTION wiring against a real change feed. This proves both arms the evttu3 ruling
/// demands: with checkpoint saves forced to fail, the subscription keeps delivering documents, logs the
/// failure, and flips its degraded health signal at the configured bound (SAFETY); on the healthy path a
/// restarted subscription resumes from the persisted checkpoint rather than replaying from the beginning
/// (LIVENESS).
/// </summary>
/// <remarks>
/// <para>
/// Reuses <see cref="CosmosDbSagaStoreContainerFixture"/> (already the established real-Cosmos fixture in
/// this test folder, see <see cref="CosmosDbChangeFeedCheckpointStoreDurabilityShould"/>) rather than
/// standing up a second emulator fixture. <see cref="CosmosDbSagaStoreContainerFixture.EnsureAvailable"/>
/// hard-fails (never soft-skips) when the emulator is not available, per that fixture's own documented
/// policy.
/// </para>
/// <para>
/// <b>Fault injection seam:</b> the change feed itself (the documents container) is REAL Cosmos -- that is
/// what "the subscription keeps delivering events" actually has to prove. The checkpoint-save FAILURE is
/// injected via <see cref="FaultInjectingCheckpointStore"/>, a thin decorator around the real
/// <see cref="CosmosDbChangeFeedCheckpointStore"/> that can be told to throw on <c>SaveAsync</c> while still
/// forwarding <c>LoadAsync</c> to the real, persisted checkpoint. This controls exactly the condition under
/// test (a checkpoint-save fault) without needing to induce an actual emulator failure (e.g. RU
/// exhaustion), while everything the ruling's SAFETY arm actually cares about -- the change-feed
/// enumeration itself -- stays real.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "ChangeFeed")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbChangeFeedCheckpointFailOpenShould : IClassFixture<CosmosDbSagaStoreContainerFixture>
{
	private readonly CosmosDbSagaStoreContainerFixture _fixture;

	public CosmosDbChangeFeedCheckpointFailOpenShould(CosmosDbSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task KeepDeliveringEvents_LogTheFailure_AndDegradeAtTheBound_WhenCheckpointSavesAreForcedToFail()
	{
		_fixture.EnsureAvailable();

		var container = await CreateDocumentsContainerAsync().ConfigureAwait(false);
		var checkpointContainer = await CreateCheckpointsContainerAsync().ConfigureAwait(false);

		var realStore = new CosmosDbChangeFeedCheckpointStore(checkpointContainer);
		var faultStore = new FaultInjectingCheckpointStore(realStore) { ShouldFail = true };
		var logger = new CapturingLogger<object>();

		// Bound of 1: the FIRST checkpoint-save failure must already cross it. This removes any
		// dependency on how the emulator happens to batch concurrently-inserted documents into change
		// feed pages -- the wiring under test (subscription reads MaxConsecutiveCheckpointFailures,
		// feeds failures into the tracker, surfaces IsCheckpointDegraded) is exercised identically
		// whether it takes one failed save or several to reach the bound; the counting arithmetic
		// itself already has a real-infra-free unit lock (ChangeFeedCheckpointFailureTrackerShould).
		var options = new ChangeFeedOptions
		{
			StartPosition = ChangeFeedStartPosition.Beginning,
			PollingInterval = TimeSpan.FromMilliseconds(200),
			MaxConsecutiveCheckpointFailures = 1,
		};

		await using var subscription =
			new CosmosDbChangeFeedSubscription<FailOpenDoc>(container, options, logger, faultStore);
		await subscription.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// A generous SAFETY NET, not the exit mechanism: every wait below ends on an OBSERVED condition
		// (a document delivered; the degraded flag flipped), so this deadline fires only if the
		// subscription stops making progress altogether.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		var received = new ConcurrentQueue<string>();
		var readTask = Task.Run(async () =>
		{
			await foreach (var evt in subscription.ReadChangesAsync(cts.Token))
			{
				if (evt.Document is not null)
				{
					received.Enqueue(evt.Document.Id);
				}
			}
		});

		// Each document is created only AFTER the previous one has been DELIVERED. That is strictly
		// stronger than pacing the inserts on a timer and hoping they straddle poll cycles: a page that
		// has already been read cannot contain a document created after it was read, so every document
		// here PROVABLY lands in a separate change-feed page, and delivery across multiple poll cycles
		// is proven rather than assumed.
		var docs = new List<FailOpenDoc>();
		for (var i = 0; i < 4; i++)
		{
			var doc = new FailOpenDoc { Id = $"kb1u1u-{Guid.NewGuid():N}", PartitionKey = "kb1u1u", Seq = i };
			docs.Add(doc);
			await container.CreateItemAsync(doc, new CosmosPartitionKey(doc.PartitionKey), cancellationToken: cts.Token)
				.ConfigureAwait(false);

			var delivered = await WaitHelpers.WaitUntilAsync(
				() => received.Contains(doc.Id),
				TimeSpan.FromSeconds(30),
				cancellationToken: cts.Token).ConfigureAwait(false);
			delivered.ShouldBeTrue(
				$"document {doc.Seq} was never delivered -- a checkpoint-save failure must not stop, slow, or "
				+ "truncate event delivery, and nothing after this point can prove anything without it.");
		}

		// Poll the real post-page state rather than waiting a fixed time and hoping. The checkpoint-save
		// for a page runs only once the consumer resumes iteration PAST that page's final yield, so the
		// LAST page's (failing) save lands a moment after its document is delivered. No assertion here:
		// the richly-worded assertions below are the check, and they report which half is missing.
		_ = await WaitHelpers.WaitUntilAsync(
			() => faultStore.SaveAttempts > 0 && subscription.IsCheckpointDegraded,
			TimeSpan.FromSeconds(30),
			cancellationToken: cts.Token).ConfigureAwait(false);

		await cts.CancelAsync().ConfigureAwait(false);
		await readTask.ConfigureAwait(false);
		await subscription.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// SAFETY: every checkpoint save failed, and the feed still delivered every document -- a
		// checkpoint-save failure did not stop, slow, or truncate event delivery.
		received.ShouldBe(docs.Select(d => d.Id), ignoreOrder: true,
			"a checkpoint-save failure must not stop event delivery -- the events were already yielded "
			+ "to the consumer before the (failing) save is even attempted.");

		faultStore.SaveAttempts.ShouldBeGreaterThan(0,
			"the fault-injecting store was never asked to save a checkpoint, so this run proves nothing "
			+ "about the fail-open path -- the subscription must call SaveAsync after processing a page.");

		// The failure was logged at Warning via the reserved ChangeFeedError event id.
		logger.Entries.ShouldContain(
			e => e.EventId.Id == DataCosmosDbEventId.ChangeFeedError && e.Level == LogLevel.Warning,
			"a checkpoint-save failure must be logged (Warning, ChangeFeedError) -- this is the SIGNAL half "
			+ "of the evttu3 fail-open ruling.");

		// The configured bound (1) was crossed by the very first failure, so the subscription must report
		// itself checkpoint-degraded and log the Critical escalation exactly once.
		subscription.IsCheckpointDegraded.ShouldBeTrue(
			"MaxConsecutiveCheckpointFailures=1 was configured and every checkpoint save failed -- the "
			+ "subscription must flip IsCheckpointDegraded rather than continue silently forever. This is "
			+ "the BOUND half of the evttu3 fail-open ruling.");
		subscription.CheckpointLag.ShouldNotBeNull(
			"a degraded subscription must report how long it has been degraded.");
		logger.Entries.ShouldContain(
			e => e.EventId.Id == DataCosmosDbEventId.ChangeFeedCheckpointDegraded && e.Level == LogLevel.Critical,
			"crossing the bound must log the Critical escalation (ChangeFeedCheckpointDegraded).");
	}

	[Fact]
	public async Task PersistTheCheckpointAndResumeFromIt_OnTheHealthyPath()
	{
		_fixture.EnsureAvailable();

		var container = await CreateDocumentsContainerAsync().ConfigureAwait(false);
		var checkpointContainer = await CreateCheckpointsContainerAsync().ConfigureAwait(false);

		var docA = new FailOpenDoc { Id = $"kb1u1u-a-{Guid.NewGuid():N}", PartitionKey = "kb1u1u", Seq = 1 };
		await container.CreateItemAsync(docA, new CosmosPartitionKey(docA.PartitionKey)).ConfigureAwait(false);

		var options = new ChangeFeedOptions
		{
			StartPosition = ChangeFeedStartPosition.Beginning,
			PollingInterval = TimeSpan.FromMilliseconds(200),
		};

		// FIRST "run": a healthy checkpoint store (never forced to fail). Reads doc A, and -- critically
		// -- keeps the enumeration running rather than breaking the instant doc A is observed: the
		// checkpoint-save for a page only executes once the consumer resumes iteration PAST that page's
		// last yield, so breaking immediately would race the very save this arm exists to prove happened.
		// The run therefore ends on the OBSERVED checkpoint, not on a deadline it has to beat.
		var store1 = new CosmosDbChangeFeedCheckpointStore(checkpointContainer);
		var logger1 = new CapturingLogger<object>();
		var receivedFirstRun = new ConcurrentQueue<string>();
		await using (var subscription1 =
			new CosmosDbChangeFeedSubscription<FailOpenDoc>(container, options, logger1, store1))
		{
			await subscription1.StartAsync(CancellationToken.None).ConfigureAwait(false);

			// Generous SAFETY NET; the exit below is an observed condition, not this deadline.
			using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(60));
			var readTask1 = Task.Run(async () =>
			{
				try
				{
					await foreach (var evt in subscription1.ReadChangesAsync(cts1.Token))
					{
						if (evt.Document is not null)
						{
							receivedFirstRun.Enqueue(evt.Document.Id);
						}
					}
				}
				catch (OperationCanceledException)
				{
					// Expected: cancelling cts1 is how this run intentionally ends.
				}
			});

			// End this run on the condition the SECOND run actually depends on -- a checkpoint that is
			// durably READABLE -- instead of on elapsed time. The subscription's checkpoint key is
			// restart-invariant (`cf-{container.Id}`, deliberately not the per-instance SubscriptionId),
			// which is the whole reason run 2 is able to find it.
			var checkpointKey = $"cf-{container.Id}";
			var persisted = await WaitHelpers.WaitUntilAsync(
				async () => await store1.LoadAsync(checkpointKey, cts1.Token).ConfigureAwait(false) is not null,
				TimeSpan.FromSeconds(30),
				cancellationToken: cts1.Token).ConfigureAwait(false);
			persisted.ShouldBeTrue(
				"the first run never persisted a readable checkpoint, so a failure of the resume asserted "
				+ "below would be for want of a checkpoint rather than for want of resumption logic.");

			await cts1.CancelAsync().ConfigureAwait(false);
			await readTask1.ConfigureAwait(false);
			await subscription1.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}

		receivedFirstRun.ShouldContain(docA.Id, "the first run must have actually observed doc A before 'restarting'.");

		// Insert doc B AFTER the simulated restart point.
		var docB = new FailOpenDoc { Id = $"kb1u1u-b-{Guid.NewGuid():N}", PartitionKey = "kb1u1u", Seq = 2 };
		await container.CreateItemAsync(docB, new CosmosPartitionKey(docB.PartitionKey)).ConfigureAwait(false);

		// SECOND "run": a FRESH subscription instance and a FRESH CosmosDbChangeFeedCheckpointStore
		// instance -- simulating a process restart -- pointed at the SAME checkpoints container. The
		// checkpoint key is `cf-{container.Id}` (restart-invariant, not the per-instance SubscriptionId),
		// so if durable continuation actually works this run resumes AFTER doc A's page and observes
		// doc B without replaying doc A. The default (non-durable) InMemory store, or a store that never
		// truly persists, would replay from Beginning and see doc A again FIRST.
		var store2 = new CosmosDbChangeFeedCheckpointStore(checkpointContainer);
		var logger2 = new CapturingLogger<object>();
		var receivedSecondRun = new List<string>();
		await using (var subscription2 =
			new CosmosDbChangeFeedSubscription<FailOpenDoc>(container, options, logger2, store2))
		{
			await subscription2.StartAsync(CancellationToken.None).ConfigureAwait(false);

			// Generous SAFETY NET; this run exits on observing doc B, not on this deadline.
			using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(60));
			try
			{
				await foreach (var evt in subscription2.ReadChangesAsync(cts2.Token))
				{
					if (evt.Document is not null)
					{
						receivedSecondRun.Add(evt.Document.Id);
					}

					if (evt.Document?.Id == docB.Id)
					{
						// Stop the instant doc B is observed. There is nothing left to wait for, and
						// waiting cannot mask the defect this run exists to catch: doc A was created
						// FIRST, so any run that replayed from the beginning delivers it BEFORE doc B.
						// By the time doc B arrives, a broken checkpoint has already put doc A in this
						// list, and the ShouldNotContain below sees it.
						await cts2.CancelAsync().ConfigureAwait(false);
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Expected: cts2 is how this run intentionally ends once doc B is observed.
			}

			await subscription2.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}

		// LIVENESS: the resumed position is asserted directly, not merely "no exception was thrown" --
		// an implementation that swallows every save and never persists anything would replay from
		// Beginning and this would fail.
		receivedSecondRun.ShouldContain(docB.Id);
		receivedSecondRun.ShouldNotContain(docA.Id,
			"a restarted subscription reading a REAL, persisted checkpoint must resume from AFTER the "
			+ "last processed page, not replay doc A from the beginning of the feed.");
	}

	private async Task<Container> CreateDocumentsContainerAsync()
	{
		var database = _fixture.Client.GetDatabase(_fixture.DatabaseName);
		var response = await database.CreateContainerIfNotExistsAsync(
			new ContainerProperties($"changefeed-failopen-docs-{Guid.NewGuid():N}", "/partitionKey"))
			.ConfigureAwait(false);
		return response.Container;
	}

	private async Task<Container> CreateCheckpointsContainerAsync()
	{
		var database = _fixture.Client.GetDatabase(_fixture.DatabaseName);
		var response = await database.CreateContainerIfNotExistsAsync(
			new ContainerProperties($"changefeed-failopen-checkpoints-{Guid.NewGuid():N}", "/subscriptionId"))
			.ConfigureAwait(false);
		return response.Container;
	}

	/// <summary>Minimal document type for the change-feed source container used by this suite.</summary>
	private sealed class FailOpenDoc
	{
		public string Id { get; set; } = string.Empty;

		public string PartitionKey { get; set; } = string.Empty;

		public int Seq { get; set; }
	}

	/// <summary>
	/// Decorates a real <see cref="IChangeFeedCheckpointStore"/> so <c>SaveAsync</c> can be told to
	/// throw on demand, while <c>LoadAsync</c> always forwards to the real store. This is the fault
	/// injection seam: the change feed itself stays real Cosmos; only the checkpoint-save fault is
	/// synthetic and controlled.
	/// </summary>
	private sealed class FaultInjectingCheckpointStore : IChangeFeedCheckpointStore
	{
		private readonly IChangeFeedCheckpointStore _inner;
		private int _saveAttempts;

		public FaultInjectingCheckpointStore(IChangeFeedCheckpointStore inner) => _inner = inner;

		public bool ShouldFail { get; set; }

		public int SaveAttempts => Volatile.Read(ref _saveAttempts);

		public Task<string?> LoadAsync(string subscriptionId, CancellationToken cancellationToken) =>
			_inner.LoadAsync(subscriptionId, cancellationToken);

		public Task SaveAsync(string subscriptionId, string continuationToken, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _saveAttempts);

			if (ShouldFail)
			{
				throw new IOException(
					"Injected checkpoint-save failure (Excalibur_Dispatch-kb1u1u real-infra lock).");
			}

			return _inner.SaveAsync(subscriptionId, continuationToken, cancellationToken);
		}
	}
}
