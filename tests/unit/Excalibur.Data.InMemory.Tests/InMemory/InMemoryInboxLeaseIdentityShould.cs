// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

// DISTINCT ACQUISITIONS MUST HAVE DISTINCT IDENTITIES -- the other admission path.
//
// The sibling suite (InboxLeaseFencingShould) closes the LAPSE path: A's lease expires, B reclaims, A's
// stale finalize must be refused. It drives that with a clock MOVE, and the term it relies on -- the
// expiry instant -- is genuinely different on either side of a lapse, because a reclaim requires the old
// expiry to be strictly in the past.
//
// This suite closes the path where NO LAPSE OCCURS AND THE CLOCK NEVER MOVES:
//
//   A acquires key K for duration D, under term Ta
//   A FAILS -- and a Failed entry is readmitted IMMEDIATELY, with no expiry needing to pass
//   B acquires K for the same D, in the same millisecond, under term Tb
//
// If the term is the expiry, then Ta == Tb by arithmetic: both are now+D, and now has not moved. A's
// delayed or repeated Complete/Fail is then indistinguishable from B's and mutates B's record. No
// concurrency and no clock skew are required -- only two acquisitions inside one millisecond, which a
// fixed TimeProvider makes deterministic rather than flaky.
//
// The uniqueness argument recorded at the old term site accounted for reclaim-after-expiry only. It was
// sound for the case it addressed and silent about this one.
//
// SAFETY arms go RED if the term stops being a per-acquisition identity. LIVENESS arms fail a store that
// refuses everything -- which would otherwise satisfy every safety arm by doing nothing.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryInboxLeaseIdentityShould
{
	private const string Handler = "TestHandler";
	private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

	/// <summary>A clock that does not move, so "the same millisecond" is exact rather than hoped for.</summary>
	private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
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
	/// A fails and B immediately reacquires, with the clock frozen. Returns both terms.
	/// </summary>
	private static async Task<(InMemoryInboxStore Store, string MessageId, LeaseToken TermA, LeaseToken TermB)>
		FailThenImmediateReacquireAsync()
	{
		var store = NewStore(new FrozenClock(T0));
		var messageId = $"msg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var termA = (await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct))
			.ShouldNotBeNull("the first caller must be admitted");

		(await store.FailAsync(messageId, Handler, termA, "A's handler threw", ct)).ShouldBeTrue(
			"A holds the lease, so its own failure report must be accepted");

		// No clock move. A Failed entry is claimable immediately -- that is the whole point of this path.
		var termB = (await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct))
			.ShouldNotBeNull("a failed message must be retryable at once, not only after the lease lapses");

		return (store, messageId, termA, termB);
	}

	/// <summary>
	/// SAFETY, the headline. The two terms must differ. Everything below depends on this and it is worth
	/// asserting on its own, so a failure says "the identity collided" rather than "a write was accepted".
	/// </summary>
	[Fact]
	public async Task IssueADifferentTermToTheSecondAcquisition_EvenInTheSameMillisecond()
	{
		var (store, _, termA, termB) = await FailThenImmediateReacquireAsync();
		await using (store)
		{
			termB.ShouldNotBe(
				termA,
				"two successful acquisitions of one key are two distinct ownerships. Deriving the term from "
				+ "the expiry makes them identical whenever the clock has not moved, because both compute "
				+ "now plus the same duration");
		}
	}

	/// <summary>
	/// SAFETY. The consequence that actually costs something: A's late finalize landing on B's record.
	/// </summary>
	[Fact]
	public async Task RefuseToCompleteUnderTheSupersededTerm()
	{
		var (store, messageId, termA, _) = await FailThenImmediateReacquireAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			(await store.CompleteAsync(messageId, Handler, termA, ct)).ShouldBeFalse(
				"A's claim ended when it reported failure; a delayed or repeated completion from A must not "
				+ "be accepted against the claim B now holds");

			(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeFalse(
				"and it must not merely be REPORTED as refused -- B is still processing, so the write must "
				+ "not have happened");
		}
	}

	/// <summary>
	/// SAFETY. The same for the failure path, which is the one that would burn B's retry budget.
	/// </summary>
	[Fact]
	public async Task RefuseToFailUnderTheSupersededTerm()
	{
		var (store, messageId, termA, _) = await FailThenImmediateReacquireAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			(await store.FailAsync(messageId, Handler, termA, "A reporting twice", ct)).ShouldBeFalse(
				"A already reported its failure and lost the claim. Accepting a second report would record "
				+ "B's attempt as failed on A's behalf");

			var entry = await store.GetEntryAsync(messageId, Handler, ct);
			entry.ShouldNotBeNull();
			entry.Status.ShouldBe(InboxStatus.Processing, "the entry still belongs to B");
		}
	}

	/// <summary>
	/// LIVENESS, and without it every arm above is satisfied by a store that refuses all writes. B must be
	/// able to finalize the claim it legitimately holds.
	/// </summary>
	[Fact]
	public async Task StillAcceptTheHolderOfTheCurrentTerm()
	{
		var (store, messageId, _, termB) = await FailThenImmediateReacquireAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			(await store.CompleteAsync(messageId, Handler, termB, ct)).ShouldBeTrue(
				"B holds the current claim and must be able to finalize it");

			(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeTrue(
				"and the write must actually have taken effect");
		}
	}

	/// <summary>
	/// LIVENESS. The lapse path must keep working -- the fix replaces what the term IS, so the sibling
	/// suite's property is re-asserted here at the seam rather than assumed to be unaffected.
	/// </summary>
	[Fact]
	public async Task StillReclaimALapsedLeaseAndIssueAFreshTerm()
	{
		var clock = new FrozenClock(T0);
		await using var store = NewStore(clock);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var termA = (await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct))
			.ShouldNotBeNull();

		clock.Now = T0.Add(Lease).AddSeconds(1);

		var termB = (await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct))
			.ShouldNotBeNull("an expired lease must still be reclaimable");

		termB.ShouldNotBe(termA, "and the reclaiming caller must get its own identity");
	}
}
