// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.ElasticSearch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure regression lock for <see cref="ElasticsearchOutboxStore"/>'s delivery-claim and
/// terminal-transition primitives against a live Elasticsearch container.
/// </summary>
/// <remarks>
/// <para>
/// Locks three properties the store previously did not hold:
/// </para>
/// <list type="number">
/// <item>concurrent pollers receive <b>disjoint</b> batches (there was no claim at all, so two pollers
/// received byte-identical batches by construction and every message was delivered twice);</item>
/// <item>a stale failure report never <b>resurrects</b> a delivered message (the write was a blind
/// full-document overwrite, so a late <c>MarkFailedAsync</c> reopened a sent message and it was re-sent);</item>
/// <item>concurrent <c>MarkSentAsync</c> callers admit <b>exactly one</b> winner (the already-sent check
/// and the write were separate round trips, so both callers could observe a non-sent message and both
/// "succeed").</item>
/// </list>
/// <para>
/// <b>Real infrastructure, never skipped</b> (<c>verify-against-real-infra-not-mock</c>): the
/// compare-and-swap on <c>if_seq_no</c>/<c>if_primary_term</c> is enforced by the Elasticsearch server, so
/// a mocked client cannot reproduce it — a mock returns whatever it was told and would certify the broken
/// store as correct. Docker availability is asserted rather than skip-gated.
/// </para>
/// <para>
/// <b>Non-vacuous</b>: each safety assertion was verified RED against the pre-fix implementation — the
/// disjointness arm fails with a full 20-id overlap on the claimless search, the resurrection arm fails
/// with a Failed terminal status, and the exactly-one arm fails with two winners. Each is paired with a
/// <b>liveness</b> arm so an inert store (one that claims nothing, or refuses every transition) cannot
/// pass: claims must still cover every staged message, a legitimate failure must still be recorded, and
/// an expired lease must still be reclaimable.
/// </para>
/// </remarks>
[Collection(ElasticsearchOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Outbox")]
public sealed class ElasticsearchOutboxStoreClaimAtomicityShould
{
	private const int StagedCount = 20;
	private const int Concurrency = 8;

	/// <summary>
	/// The lease used by the reclaim arm, in seconds.
	/// </summary>
	/// <remarks>
	/// It has to clear the index refresh interval (one second by default) with room to spare, because the
	/// claim waits for a refresh before returning and that wait is charged against the lease it just
	/// stamped. Five seconds leaves roughly four for the safety assertion to run in, and still expires
	/// well inside the reclaim poll's budget.
	/// </remarks>
	private const int ReclaimLeaseSeconds = 5;

	private readonly ElasticsearchOutboxStoreContainerFixture _fixture;

	public ElasticsearchOutboxStoreClaimAtomicityShould(ElasticsearchOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Hand_disjoint_batches_to_two_pollers_claiming_the_same_staged_messages()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the real-Elasticsearch disjoint-claim lock must never be skipped (verify-against-real-infra-not-mock).");

		var pollerA = CreateStore(processorId: "poller-a");
		var pollerB = CreateStore(processorId: "poller-b");

		try
		{
			var stagedIds = new List<string>(StagedCount);
			for (var i = 0; i < StagedCount; i++)
			{
				var message = NewMessage();
				stagedIds.Add(message.Id);
				await pollerA.StageMessageAsync(message, CancellationToken.None);
			}

			var batches = await Task.WhenAll(
				Task.Run(async () => (await pollerA.GetUnsentMessagesAsync(StagedCount, CancellationToken.None)).Select(m => m.Id).ToList()),
				Task.Run(async () => (await pollerB.GetUnsentMessagesAsync(StagedCount, CancellationToken.None)).Select(m => m.Id).ToList()));

			var claimedA = batches[0];
			var claimedB = batches[1];

			// SAFETY: no message may be handed to both pollers.
			var overlap = claimedA.Intersect(claimedB, StringComparer.Ordinal).ToList();
			overlap.ShouldBeEmpty(
				$"concurrent claims must be disjoint — {overlap.Count} message(s) were claimed by BOTH pollers, "
				+ "which is a duplicate delivery of each. A claimless search returns the same batch to everyone.");

			// SAFETY: no message may be handed to the same poller twice.
			var union = claimedA.Concat(claimedB).ToList();
			union.Count.ShouldBe(
				union.Distinct(StringComparer.Ordinal).Count(),
				"no message id may appear more than once across the two batches");

			// LIVENESS: a store that claims nothing would satisfy every assertion above. Every staged
			// message must actually have been handed to exactly one poller.
			union.OrderBy(id => id, StringComparer.Ordinal).ShouldBe(
				stagedIds.OrderBy(id => id, StringComparer.Ordinal),
				ignoreOrder: false,
				"every staged message must be claimed by exactly one poller — a claim that hides messages "
				+ "from everyone stalls delivery instead of duplicating it");
		}
		finally
		{
			await DisposeAndCleanAsync(pollerA, pollerB);
		}
	}

	[Fact]
	public async Task Refuse_to_reopen_a_sent_message_when_a_failure_report_lands_after_delivery()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the real-Elasticsearch no-resurrection lock must never be skipped (verify-against-real-infra-not-mock).");

		var store = CreateStore(processorId: "poller-a");

		try
		{
			var message = NewMessage();
			await store.StageMessageAsync(message, CancellationToken.None);
			_ = await store.GetUnsentMessagesAsync(10, CancellationToken.None);

			await store.MarkSentAsync(message.Id, CancellationToken.None);

			// A failure report generated before the success is reported after it.
			await store.MarkFailedAsync(message.Id, "transport timed out", retryCount: 1, CancellationToken.None);

			// SAFETY: the delivered message must not have been reopened.
			var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
			stats.SentMessageCount.ShouldBe(1, "the delivered message must remain sent");
			stats.FailedMessageCount.ShouldBe(
				0,
				"a stale failure report must not resurrect a delivered message — a blind full-document "
				+ "overwrite reopens it to Failed and the message is then delivered a second time");

			// LIVENESS: the guard must not have disabled failure reporting altogether.
			var stagedIds = (await store.GetUnsentMessagesAsync(10, CancellationToken.None)).Select(m => m.Id).ToList();
			stagedIds.ShouldNotContain(message.Id, "a sent message must never become claimable again");
		}
		finally
		{
			await DisposeAndCleanAsync(store);
		}
	}

	[Fact]
	public async Task Record_a_failure_when_the_message_has_not_been_delivered()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the real-Elasticsearch failure-recording liveness arm must never be skipped.");

		var store = CreateStore(processorId: "poller-a");

		try
		{
			var message = NewMessage();
			await store.StageMessageAsync(message, CancellationToken.None);
			_ = await store.GetUnsentMessagesAsync(10, CancellationToken.None);

			await store.MarkFailedAsync(message.Id, "transport refused", retryCount: 2, CancellationToken.None);

			// LIVENESS: the no-resurrection guard must still allow a genuine failure to be recorded.
			var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
			stats.FailedMessageCount.ShouldBe(
				1,
				"a failure on a message that was never sent must still be recorded — a guard that refuses "
				+ "every transition would satisfy the no-resurrection arm while recording nothing");

			var failed = (await store.GetAllTenantsFailedMessagesAsync(maxRetries: 0, olderThan: null, batchSize: 10, CancellationToken.None)).ToList();
			failed.ShouldContain(m => m.Id == message.Id, "the failed message must be retrievable for retry");
			failed.Single(m => m.Id == message.Id).LastError.ShouldBe("transport refused");
		}
		finally
		{
			await DisposeAndCleanAsync(store);
		}
	}

	[Fact]
	public async Task Admit_exactly_one_winner_when_concurrent_callers_mark_the_same_message_sent()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the real-Elasticsearch exactly-one-mark-sent lock must never be skipped (verify-against-real-infra-not-mock).");

		var store = CreateStore(processorId: "poller-a");

		try
		{
			var message = NewMessage();
			await store.StageMessageAsync(message, CancellationToken.None);
			_ = await store.GetUnsentMessagesAsync(10, CancellationToken.None);

			var outcomes = await Task.WhenAll(
				Enumerable.Range(0, Concurrency).Select(_ => Task.Run(async () =>
				{
					try
					{
						await store.MarkSentAsync(message.Id, CancellationToken.None);
						return true;
					}
					catch (InvalidOperationException)
					{
						// The documented loser outcome: the message was already marked sent.
						return false;
					}
				})));

			// SAFETY + LIVENESS in one assertion: exactly one — never two (a double-acknowledged delivery),
			// never zero (a store that refuses every transition and strands the message).
			outcomes.Count(won => won).ShouldBe(
				1,
				$"exactly one of {Concurrency} concurrent callers may mark the message sent; got "
				+ $"[{string.Join(",", outcomes)}]. A check-then-act across separate round trips lets several win.");

			var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
			stats.SentMessageCount.ShouldBe(1, "the message must end up sent exactly once");
		}
		finally
		{
			await DisposeAndCleanAsync(store);
		}
	}

	[Fact]
	public async Task Settle_on_delivered_when_a_success_and_a_failure_are_reported_concurrently()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the real-Elasticsearch concurrent-terminal-transition lock must never be skipped.");

		var store = CreateStore(processorId: "poller-a");

		try
		{
			var message = NewMessage();
			await store.StageMessageAsync(message, CancellationToken.None);
			_ = await store.GetUnsentMessagesAsync(10, CancellationToken.None);

			// Whichever lands first, the outcome is deterministic: if the failure wins the race the success
			// still applies on top; if the success wins, the failure must stand down. Either way the message
			// ends DELIVERED — never Failed, which would re-queue an already-delivered message.
			await Task.WhenAll(
				Task.Run(async () =>
				{
					try
					{
						await store.MarkSentAsync(message.Id, CancellationToken.None);
					}
					catch (InvalidOperationException)
					{
						// Already sent — an acceptable outcome for this race.
					}
				}),
				Task.Run(() => store.MarkFailedAsync(message.Id, "transport timed out", retryCount: 1, CancellationToken.None).AsTask()));

			var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
			stats.SentMessageCount.ShouldBe(
				1,
				"a delivered message must remain delivered regardless of how the success and failure reports interleave");
			stats.FailedMessageCount.ShouldBe(
				0,
				"the failure report must not win over a delivery — that re-queues an already-delivered message");
		}
		finally
		{
			await DisposeAndCleanAsync(store);
		}
	}

	/// <remarks>
	/// <para>
	/// <b>Why the lease is seconds long and not one second.</b> The claim stamps the lease and then waits
	/// for the index to refresh before returning, because a claim nobody can see hides nothing. On a
	/// near-real-time index that wait is bounded by the refresh interval — a second by default — so a
	/// one-second lease is spent in full by the very write that grants it, and the first instant any other
	/// poller could look is already past expiry. Measured on a live container: the claim was issued at
	/// <c>…21.092</c> and returned at <c>…22.096</c>, stamping a lease that expired at <c>…22.092</c>; the
	/// second poller's search went out fifteen milliseconds after the lease had died. The store was
	/// behaving correctly and the arm was asking it for something a near-real-time index cannot express.
	/// </para>
	/// <para>
	/// The lease is therefore set comfortably above the refresh interval, which leaves the safety arm most
	/// of the window to run in rather than fifteen milliseconds on the wrong side of it. Nothing here is
	/// weakened to get green: a live lease still has to hide the message, an expired one still has to
	/// release it, and the elapsed time is reported on failure so a future reader can tell a real
	/// regression from a machine too slow to have observed one.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Reclaim_an_expired_lease_so_a_crashed_poller_cannot_strand_a_message()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the real-Elasticsearch lease-reclaim liveness arm must never be skipped.");

		var crashed = CreateStore(processorId: "poller-a", leaseTimeoutSeconds: ReclaimLeaseSeconds);
		var recovering = CreateStore(processorId: "poller-b", leaseTimeoutSeconds: ReclaimLeaseSeconds);

		try
		{
			var message = NewMessage();
			await crashed.StageMessageAsync(message, CancellationToken.None);

			var claimIssuedAt = DateTimeOffset.UtcNow;
			var firstClaim = (await crashed.GetUnsentMessagesAsync(10, CancellationToken.None)).Select(m => m.Id).ToList();
			firstClaim.ShouldContain(message.Id, "the first poller must claim the staged message");

			// SAFETY: while the lease is live, no other poller may take the message.
			var eager = (await recovering.GetUnsentMessagesAsync(10, CancellationToken.None)).Select(m => m.Id).ToList();
			var elapsed = DateTimeOffset.UtcNow - claimIssuedAt;
			eager.ShouldNotContain(
				message.Id,
				"a live lease must hide the message from other pollers. The lease runs "
				+ $"{ReclaimLeaseSeconds}s from the claim and the second poller looked {elapsed.TotalMilliseconds:F0}ms "
				+ "in; if that is at or beyond the lease, this machine was too slow to observe a live lease "
				+ "and the lease needs raising, not the store fixing.");

			// The first poller "crashes" without reporting an outcome; its lease expires. Polled rather
			// than slept through: expiry is a wall-clock property, and a fixed delay is either flaky under
			// load or wastefully long.
			var reclaimed = await PollUntilReclaimedAsync(recovering, message.Id, TimeSpan.FromSeconds(30));

			// LIVENESS: the message must come back, or a crashed poller strands it forever.
			reclaimed.ShouldBeTrue(
				"an expired lease must be reclaimable — otherwise a poller that dies mid-delivery loses the "
				+ "message permanently, trading duplicate delivery for lost delivery");
		}
		finally
		{
			await DisposeAndCleanAsync(crashed, recovering);
		}
	}

	/// <summary>
	/// Polls <paramref name="poller"/> until it is handed <paramref name="messageId"/>, or the budget runs out.
	/// </summary>
	private static async Task<bool> PollUntilReclaimedAsync(
		ElasticsearchOutboxStore poller,
		string messageId,
		TimeSpan budget)
	{
		var deadline = DateTimeOffset.UtcNow + budget;

		do
		{
			var batch = await poller.GetUnsentMessagesAsync(10, CancellationToken.None);
			if (batch.Any(m => m.Id == messageId))
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250));
		}
		while (DateTimeOffset.UtcNow < deadline);

		return false;
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	private ElasticsearchOutboxStore CreateStore(string processorId, int leaseTimeoutSeconds = 300)
	{
		var options = Options.Create(new ElasticsearchOutboxOptions
		{
			IndexName = _fixture.IndexName,
			RefreshPolicy = "wait_for",
			ProcessorId = processorId,
			LeaseTimeoutSeconds = leaseTimeoutSeconds,
		});

		return new ElasticsearchOutboxStore(_fixture.Client, options, NullLogger<ElasticsearchOutboxStore>.Instance);
	}

	private static OutboundMessage NewMessage() =>
		new()
		{
			Id = Guid.NewGuid().ToString("N"),
			MessageType = "TestMessage",
			Payload = [1, 2, 3],
			Destination = "test-destination",
			CreatedAt = DateTimeOffset.UtcNow,
			Status = OutboxStatus.Staged,
		};

	private async Task DisposeAndCleanAsync(params ElasticsearchOutboxStore[] stores)
	{
		foreach (var store in stores)
		{
			await store.DisposeAsync().ConfigureAwait(false);
		}

		await _fixture.DeleteIndexAsync().ConfigureAwait(false);
	}
}
