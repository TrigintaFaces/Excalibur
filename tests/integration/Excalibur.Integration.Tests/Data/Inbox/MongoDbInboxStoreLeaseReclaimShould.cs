// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// kj847e (S868) — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-MongoDB concurrency lock for
/// the <b>lease-aware</b> atomic-claim overload
/// <c>IClaimableInboxStore.TryAcquireLeaseAsync(messageId, handlerType, leaseDuration, ct)</c>. The CAS is a
/// <c>findOneAndUpdate</c> aggregation pipeline keyed on the nullable <c>leaseExpiresAt</c> BSON UTC field
/// evaluated against the <b>Mongo server clock</b> (<c>$$NOW</c>): claim IFF <c>absent OR Received OR
/// (Processing AND leaseExpiry &lt; now)</c>, NEVER when terminal <see cref="InboxStatus.Processed"/>.
/// </summary>
/// <remarks>
/// Complements <c>MongoDbInboxStoreClaimAtomicityShould</c> (the 2-arg overload, which STAYS). Mongo is
/// schemaless — no fixture DDL; the store owns the <c>leaseExpiresAt</c> field. Reclaim is proven with a
/// short lease + real elapsed time (bounded poll, lower-bound only — no faked clock; per
/// <c>verify-against-real-infra</c>). <b>RED on a no-lease impl</b> (inherits the interface's
/// <see cref="System.NotSupportedException"/> default, or a claim-IFF-absent override): an expired
/// <see cref="InboxStatus.Processing"/> entry never becomes reclaimable ⇒ the poll times out ⇒ RED.
/// </remarks>
[Collection(MongoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "MongoDb")]
[Trait("Component", "Inbox")]
public sealed class MongoDbInboxStoreLeaseReclaimShould : IClassFixture<MongoDbInboxStoreContainerFixture>
{
	private const int Concurrency = 16;
	private static readonly TimeSpan ShortLease = TimeSpan.FromMilliseconds(250);
	private static readonly TimeSpan LongLease = TimeSpan.FromSeconds(30);

	private readonly MongoDbInboxStoreContainerFixture _fixture;

	public MongoDbInboxStoreLeaseReclaimShould(MongoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_exactly_one_lease_claim_when_concurrent_callers_race_the_same_message()
	{
		var store = CreateStore();
		const string messageId = "msg-lease-concurrent";
		const string handlerType = "TestHandler";

		var tasks = Enumerable.Range(0, Concurrency)
			.Select(_ => Task.Run(() => store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).AsTask()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		results.Count(claimed => claimed is not null).ShouldBe(
			1,
			$"the lease CAS must admit exactly one of {Concurrency} concurrent claims; got [{string.Join(",", results)}]");
	}

	[Fact]
	public async Task Deny_a_second_claimer_while_the_lease_is_live()
	{
		var store = CreateStore();
		const string messageId = "msg-lease-live";
		const string handlerType = "TestHandler";

		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("first caller acquires the lease");
		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeNull("a live lease must deny a concurrent second claim (no double-processing)");
	}

	[Fact]
	public async Task Reclaim_the_message_after_the_lease_expires()
	{
		var store = CreateStore();
		const string messageId = "msg-lease-expire";
		const string handlerType = "TestHandler";

		// Mark BEFORE the acquiring claim. The lease is stamped at the MONGO SERVER's clock ($$NOW)
		// *during* that round trip, i.e. at or after this mark — so elapsed-from-here is a conservative UPPER bound on how much of the lease
		// has burned by the time the denial below is evaluated. Bounding it in that direction is what keeps the
		// inconclusive guard honest: it can only ever over-estimate the burn, never under-estimate it into a false
		// "the arm discriminated".
		var sinceLeaseAcquired = Stopwatch.StartNew();

		(await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("the dead processor acquires the initial lease");

		var secondClaimWhileLeaseShouldBeLive =
			await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false);
		var elapsedAcquireToDenial = sinceLeaseAcquired.Elapsed;

		// This denial is the DISCRIMINATOR for the reclaim arm below: without it, a no-lease-enforcement impl
		// (every Processing entry claimable) would sail through the reclaim poll on its first attempt and the
		// whole test would pass vacuously. So it must stay — but it is a SAFETY arm, and a safety arm is only
		// meaningful if its observation demonstrably landed INSIDE the window it asserts. Two Mongo round trips
		// under CI load can exceed a 250 ms lease, in which case the lease had genuinely expired and a successful second
		// claim is CORRECT behaviour, not the defect this arm hunts. When the measurement cannot tell those apart,
		// say so instead of accusing the product.
		if (secondClaimWhileLeaseShouldBeLive is not null && elapsedAcquireToDenial >= ShortLease)
		{
			Assert.Fail(
				$"INCONCLUSIVE — this SAFETY arm could not run, and this is NOT a product-defect report. The "
				+ $"acquire→re-claim round trip took {elapsedAcquireToDenial.TotalMilliseconds:F0} ms, which "
				+ $"already reaches the {ShortLease.TotalMilliseconds:F0} ms lease, so a successful second "
				+ $"claim here is equally explained by a lease that legitimately expired under load and by a "
				+ $"lease CAS that never enforced expiry at all. The arm cannot discriminate; re-run on a less "
				+ $"loaded host. Deliberately NOT fixed by lengthening the lease — that would only make this "
				+ $"rarer, not correct.");
		}

		secondClaimWhileLeaseShouldBeLive.ShouldBeNull(
			"the lease is still live immediately after it was taken"
			+ $" (measured acquire→re-claim elapsed: {elapsedAcquireToDenial.TotalMilliseconds:F0} ms — inside"
			+ $" the {ShortLease.TotalMilliseconds:F0} ms lease, so this arm DID discriminate: the CAS admitted"
			+ " a claim against a lease that was still live, which is the no-lease-enforcement defect.)");

		// RED on a no-lease impl: an expired Processing entry never becomes reclaimable ⇒ this times out.
		// Already polled (bounded, lower-bound only) — the liveness direction only needs a generous budget,
		// so extra latency costs polls rather than a red. Left as-is deliberately.
		var reclaimed = await WaitHelpers.WaitUntilAsync(
			async () => await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false) is not null,
			timeout: TimeSpan.FromSeconds(10),
			pollInterval: TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		reclaimed.ShouldBeTrue(
			"an expired lease must let a new processor reclaim the abandoned message (Mongo server-clock expiry)");
	}

	[Fact]
	public async Task Never_reclaim_a_terminal_processed_message()
	{
		var store = CreateStore();
		const string messageId = "msg-lease-processed";
		const string handlerType = "TestHandler";

		(await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("claim the message for processing");
		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);

		await Task.Delay(ShortLease + TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);

		(await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeNull("a completed (Processed) message must never be reclaimed via the lease path");
	}

	// d2afxn: a Failed entry is RE-ADMITTABLE on redelivery (retry). RED on the pre-fix predicate
	// (absent | Received | expired-Processing) which denies Failed → TryClaim false → silent drop.
	[Fact]
	public async Task Readmit_and_retry_a_failed_entry_on_redelivery()
	{
		var store = CreateStore();
		const string messageId = "msg-lease-failed-readmit";
		const string handlerType = "TestHandler";

		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("the initial claim acquires the lease");
		await store.MarkFailedAsync(messageId, handlerType, "handler boom", CancellationToken.None).ConfigureAwait(false);

		var afterFail = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterFail.ShouldNotBeNull();
		afterFail!.Status.ShouldBe(InboxStatus.Failed);

		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("a Failed entry MUST be re-admittable on redelivery (at-least-once + idempotent-handler contract)");

		var afterReclaim = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterReclaim.ShouldNotBeNull();
		afterReclaim!.Status.ShouldBe(
			InboxStatus.Processing,
			"re-admitting a Failed entry transitions it back to Processing under a fresh lease");

		// d2afxn monotonic-RetryCount guarantee (SA-confirmed preserve-only design): re-admit PRESERVES the
		// retry history (never resets to 0); RetryCount increments exactly once per failed attempt at the shared
		// finalize. Impl-agnostic monotonic assertion — non-decreasing across re-admit, strictly greater after a
		// second failed attempt. RED on a reset-to-0 re-admit.
		var retriesAfterFirstFail = afterFail!.RetryCount;
		retriesAfterFirstFail.ShouldBeGreaterThanOrEqualTo(1, "the first failed attempt must record at least one retry");
		afterReclaim.RetryCount.ShouldBeGreaterThanOrEqualTo(
			retriesAfterFirstFail,
			"re-admitting a Failed entry must PRESERVE the retry count (never reset it to 0)");

		await store.MarkFailedAsync(messageId, handlerType, "handler boom again", CancellationToken.None).ConfigureAwait(false);

		var afterSecondFail = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterSecondFail.ShouldNotBeNull();
		afterSecondFail!.RetryCount.ShouldBeGreaterThan(
			retriesAfterFirstFail,
			"RetryCount MUST be monotonic across re-admits — a second failed attempt strictly increases it, never resets");
	}

	private MongoDbInboxStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available — real-infra lease lock is never skipped.");

		var options = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});
		return new MongoDbInboxStore(options, NullLogger<MongoDbInboxStore>.Instance, SingleTenantTestContext.Instance);
	}
}
