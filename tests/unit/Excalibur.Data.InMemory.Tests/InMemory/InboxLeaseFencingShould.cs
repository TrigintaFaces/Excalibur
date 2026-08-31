// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

// A CLAIM IS A TERM.
//
// The sibling suite (InboxTerminalTransitionShould) closes the half of this that a status guard can reach:
// Processed is absorbing, so a lapsed caller cannot demote a finalized entry. This suite closes the half a
// status guard structurally CANNOT reach.
//
// The sequence needs no concurrency -- only a handler that outruns its own lease:
//
//   A acquires a lease under term Ta; A's handler runs longer than the lease
//   B reclaims the expired lease and becomes the sole processor, under term Tb
//   A finishes and finalizes ... and lands on B's record
//
// At the instant of A's write the entry is legitimately Processing -- its SUCCESSOR'S. So no status
// predicate separates the two callers, which is why every store already carrying `status != Processed` on
// its writes was nine correct implementations of the wrong predicate. Only the term tells them apart.
//
// That the sequence needs no concurrency is the tell that the property was never specified rather than
// never tested. Every arm below is sequential and driven by a controlled clock: the lapse is a clock move,
// not a sleep, so nothing depends on scheduling.
//
// SAFETY arms go RED if the fence is removed. LIVENESS arms fail a store that refuses everything -- which
// would otherwise satisfy every safety arm by doing nothing.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxLeaseFencingShould
{
	private const string Handler = "TestHandler";
	private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

	private sealed class TestClock(DateTimeOffset now) : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = now;

		public override DateTimeOffset GetUtcNow() => Now;
	}

	private static InMemoryInboxStore NewStore(TimeProvider clock) =>
		new(
			Options.Create(new InMemoryInboxOptions { EnableAutomaticCleanup = false }),
			NullLogger<InMemoryInboxStore>.Instance,
			UntenantedContext.Instance,
			clock);

	/// <summary>
	/// Drives the two-caller lapse: A acquires, A's handler outruns the lease, B reclaims. Returns both
	/// terms so an arm can present either one.
	/// </summary>
	private static async Task<(InMemoryInboxStore Store, string MessageId, LeaseToken TermA, LeaseToken TermB)>
		LapsedReclaimAsync()
	{
		var clock = new TestClock(T0);
		var store = NewStore(clock);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var termA = (await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct))
			.ShouldNotBeNull("the first caller must be admitted");

		// A's handler outruns its lease.
		clock.Now = T0.Add(Lease).AddSeconds(1);

		var termB = (await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct))
			.ShouldNotBeNull("an expired lease must be reclaimable, or the message would be stuck forever");

		return (store, messageId, termA, termB);
	}

	// SAFETY (headline) -- the lapsed caller cannot finalize the record of the caller that replaced it.
	[Fact]
	public async Task RefuseToCompleteUnderALapsedTerm()
	{
		var (store, messageId, termA, _) = await LapsedReclaimAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			(await store.CompleteAsync(messageId, Handler, termA, ct)).ShouldBeFalse(
				"A's lease had already lapsed and been reclaimed, so its finalize must take no effect");

			// The write must not merely REPORT failure -- it must not have happened.
			(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeFalse(
				"B is still processing; A must not have marked B's entry terminal");

			var entry = await store.GetEntryAsync(messageId, Handler, ct);
			entry.ShouldNotBeNull();
			entry.Status.ShouldBe(InboxStatus.Processing, "the entry still belongs to B");
		}
	}

	// SAFETY -- the same for the failure path, which is the one that would resurrect a terminal entry.
	[Fact]
	public async Task RefuseToFailUnderALapsedTerm()
	{
		var (store, messageId, termA, _) = await LapsedReclaimAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			(await store.FailAsync(messageId, Handler, termA, "A threw after losing its lease", ct))
				.ShouldBeFalse("A's lease had lapsed, so its failure must not be recorded against B's entry");

			var entry = await store.GetEntryAsync(messageId, Handler, ct);
			entry.ShouldNotBeNull();
			entry.Status.ShouldBe(InboxStatus.Processing, "B is still processing; A must not have marked it Failed");
			entry.LastError.ShouldBeNull("A's error must not be attributed to B's attempt");
		}
	}

	// LIVENESS -- a fence that blocks the lapsed caller must not block the legitimate one.
	[Fact]
	public async Task CompleteUnderALiveTerm()
	{
		var clock = new TestClock(T0);
		await using var store = NewStore(clock);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var term = (await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull();

		(await store.CompleteAsync(messageId, Handler, term, ct)).ShouldBeTrue(
			"the holder of a live term must be able to finalize");
		(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeTrue();
	}

	// LIVENESS -- the failure path too, including the term being cleared so a redelivery can re-admit.
	[Fact]
	public async Task FailUnderALiveTermAndLeaveTheEntryReAdmittable()
	{
		var clock = new TestClock(T0);
		await using var store = NewStore(clock);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var term = (await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull();

		(await store.FailAsync(messageId, Handler, term, "handler failed", ct)).ShouldBeTrue(
			"the holder of a live term must be able to record its own failure");

		var entry = await store.GetEntryAsync(messageId, Handler, ct);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Failed);

		// A failed entry has no holder, so a redelivery must be re-admittable WITHOUT waiting out the lease.
		// If FailAsync left the old term in place, this would still be sitting behind a live lease.
		(await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull(
			"a Failed entry carries no holder, so a redelivery must be admitted immediately");
	}

	// LIVENESS -- the arm that catches a fence which simply refuses everything: after A is correctly
	// rejected, B must still be able to finish its own work.
	[Fact]
	public async Task StillLetTheReclaimingCallerFinalizeAfterTheLapsedOneIsRefused()
	{
		var (store, messageId, termA, termB) = await LapsedReclaimAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			(await store.CompleteAsync(messageId, Handler, termA, ct)).ShouldBeFalse();

			(await store.CompleteAsync(messageId, Handler, termB, ct)).ShouldBeTrue(
				"the live holder must still finalize after the lapsed caller was fenced out");
			(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeTrue();
		}
	}

	// The term must actually DISTINGUISH the two callers. A store that handed both the same value would
	// satisfy every liveness arm above while fencing nothing.
	[Fact]
	public async Task IssueADistinctTermToTheReclaimingCaller()
	{
		var (store, _, termA, termB) = await LapsedReclaimAsync();
		await using (store)
		{
			termB.ShouldNotBe(termA, "a reclaim that reissued the same term would fence nothing");
		}
	}

	// THE ONE-TOKEN MUTANT.
	//
	// The whole design rests on monotonicity: the term a reclaim writes is STRICTLY greater than the one it
	// displaced, because reclaim admits only when the recorded expiry is strictly earlier than the store
	// clock and the replacement is that clock plus a non-negative duration. Relaxing that one comparison
	// from `<` to `<=` breaks it -- and this arm is what goes RED when someone does.
	//
	// At exactly the expiry instant, `expiry < now` is FALSE, so the lease is still held. Under the `<=`
	// mutant it would read as expired and be handed to a second caller.
	[Fact]
	public async Task TreatALeaseAsStillHeldAtExactlyItsExpiryInstant()
	{
		var clock = new TestClock(T0);
		await using var store = NewStore(clock);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		_ = (await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull();

		// Exactly the expiry instant -- not one tick past it.
		clock.Now = T0.Add(Lease);

		(await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldBeNull(
			"the reclaim comparison must be STRICT: at exactly the expiry instant the lease is still held. " +
			"Relaxing it to <= would admit a second caller here AND allow a reclaim to reissue the same term, " +
			"which is what makes the term an identity rather than merely a deadline.");

		// Past the expiry instant it must be reclaimable -- or the strictness above would just be a stuck
		// lease. One MILLISECOND, not one tick: this store records the term as Unix milliseconds, so a
		// sub-millisecond advance is not representable in it and would leave the clock reading unchanged.
		clock.Now = T0.Add(Lease).AddMilliseconds(1);

		(await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull(
			"past the expiry instant the lease must be reclaimable, or a dead processor would block forever");
	}
}
