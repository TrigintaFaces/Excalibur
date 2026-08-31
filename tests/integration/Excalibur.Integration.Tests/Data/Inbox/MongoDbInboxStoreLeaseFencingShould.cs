// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Dispatch;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-MongoDB lock for the <b>fenced</b> half of the lease protocol: a caller whose lease has lapsed must
/// not be able to finalize the record of the caller that replaced it.
/// </summary>
/// <remarks>
/// <para>
/// The sibling suite (<see cref="MongoDbInboxStoreLeaseReclaimShould"/>) binds <i>admission</i> — who gets
/// the lease. This one binds <i>finalization</i> — who is still allowed to write once they have it. Passing
/// admission arms are not evidence for this: admission proves at most one caller is <i>told</i> it holds the
/// message; the fence is about whether the one who was told <i>keeps</i> it.
/// </para>
/// <para>
/// <b>Why real infrastructure.</b> The term is the expiry the SERVER resolved from <c>$$NOW</c> inside the
/// claim pipeline, read back off the post-image and compared inside the write predicate as a BSON date. A
/// mocked driver returns whatever it was told and can certify neither half — nor the round trip, where the
/// term is encoded as Unix milliseconds against a millisecond-precision BSON date. If that encoding lost or
/// gained a single millisecond anywhere, every finalization by the legitimate holder would silently fail
/// closed and look exactly like an early expiry — which is what <see cref="CompleteUnderALiveTerm"/> exists
/// to catch.
/// </para>
/// <para>
/// <b>The transaction caveat is a code fact here, not a comment.</b> <c>$$NOW</c> is documented constant for
/// one pipeline, which is stronger than the relational case, but its behaviour inside a shared transaction is
/// undocumented — so if an acquisition and its finalization ever shared a session, two terms could compare
/// equal and the fence would stop discriminating without failing. The store's lease seam issues every
/// operation session-less; the only session-bearing path is the non-lease transactional one, which does not
/// call these methods. These arms exercise the seam as shipped.
/// </para>
/// <para>
/// <b>Determinism:</b> a short lease and real elapsed time, polled past expiry with a bounded wait. Every
/// assertion is an eventual truth or a lower bound, so load can only lengthen the poll — never flip an
/// outcome. No wall-clock upper bounds.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the SAFETY arms go RED against a finalization that carries no term. The LIVENESS arms
/// fail a store that simply refuses everything, which would otherwise satisfy every safety arm by doing
/// nothing. The MONOTONICITY arm goes RED against the one-token mutant that relaxes the reclaim comparison
/// from <c>$lt</c> to <c>$lte</c>, which is the only way to make two terms compare equal.
/// </para>
/// </remarks>
[Collection(MongoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "MongoDb")]
[Trait("Component", "Inbox")]
public sealed class MongoDbInboxStoreLeaseFencingShould : IClassFixture<MongoDbInboxStoreContainerFixture>
{
	private static readonly TimeSpan ShortLease = TimeSpan.FromMilliseconds(750);
	private static readonly TimeSpan LongLease = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan ReclaimDeadline = TimeSpan.FromSeconds(30);

	private readonly MongoDbInboxStoreContainerFixture _fixture;

	public MongoDbInboxStoreLeaseFencingShould(MongoDbInboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// Drives the two-caller lapse against the real store: A acquires under a short lease, the lease runs
	/// out on the SERVER clock, B reclaims. Returns both terms.
	/// </summary>
	private async Task<(MongoDbInboxStore Store, string MessageId, string HandlerType, LeaseToken TermA, LeaseToken TermB)>
		LapsedReclaimAsync()
	{
		var store = CreateStore();
		var messageId = $"msg-{Guid.NewGuid():N}";
		var handlerType = $"handler-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var termA = (await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull("the first caller must be admitted on a key the store has never seen");

		LeaseToken? reclaimed = null;
		var deadline = DateTime.UtcNow + ReclaimDeadline;

		while (DateTime.UtcNow < deadline)
		{
			reclaimed = await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false);

			if (reclaimed is not null)
			{
				break;
			}

			await Task.Delay(50, ct).ConfigureAwait(false);
		}

		var termB = reclaimed.ShouldNotBeNull(
			"an expired lease MUST be reclaimable, or a dead processor would block the message forever");

		return (store, messageId, handlerType, termA, termB);
	}

	// SAFETY (headline) — the lapsed caller cannot finalize its successor's record.
	[Fact]
	public async Task RefuseToCompleteUnderALapsedTerm()
	{
		var (store, messageId, handlerType, termA, _) = await LapsedReclaimAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;

		(await store.CompleteAsync(messageId, handlerType, termA, ct).ConfigureAwait(false)).ShouldBeFalse(
			"A's lease had lapsed and been reclaimed, so its finalize must match no document");

		(await store.IsProcessedAsync(messageId, handlerType, ct).ConfigureAwait(false)).ShouldBeFalse(
			"B is still processing; A must not have marked B's entry terminal");

		var entry = await store.GetEntryAsync(messageId, handlerType, ct).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Processing, "the entry still belongs to B");
	}

	// SAFETY — the failure path, which is the one that would resurrect a terminal entry.
	[Fact]
	public async Task RefuseToFailUnderALapsedTerm()
	{
		var (store, messageId, handlerType, termA, _) = await LapsedReclaimAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;

		(await store.FailAsync(messageId, handlerType, termA, "A threw after losing its lease", ct).ConfigureAwait(false))
			.ShouldBeFalse("A's lease had lapsed, so its failure must not be recorded against B's entry");

		var entry = await store.GetEntryAsync(messageId, handlerType, ct).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Processing, "B is still processing; A must not have marked it Failed");
	}

	// LIVENESS — the fence must not block the caller it belongs to. This is the arm that catches a term
	// lost or gained in the round trip between the BSON date and its Unix-millisecond encoding.
	[Fact]
	public async Task CompleteUnderALiveTerm()
	{
		var store = CreateStore();
		var messageId = $"msg-{Guid.NewGuid():N}";
		var handlerType = $"handler-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var term = (await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull();

		(await store.CompleteAsync(messageId, handlerType, term, ct).ConfigureAwait(false)).ShouldBeTrue(
			"the holder of a live term must be able to finalize — if this is RED the term does not survive "
			+ "the round-trip through the BSON date and every finalization fails closed");

		(await store.IsProcessedAsync(messageId, handlerType, ct).ConfigureAwait(false)).ShouldBeTrue();
	}

	// LIVENESS — the failure path, and the term being cleared so a redelivery is immediately re-admittable.
	[Fact]
	public async Task FailUnderALiveTermAndLeaveTheEntryReAdmittable()
	{
		var store = CreateStore();
		var messageId = $"msg-{Guid.NewGuid():N}";
		var handlerType = $"handler-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var term = (await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull();

		(await store.FailAsync(messageId, handlerType, term, "handler failed", ct).ConfigureAwait(false)).ShouldBeTrue(
			"the holder of a live term must be able to record its own failure");

		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull("a Failed entry carries no holder, so a redelivery must be admitted immediately");
	}

	// LIVENESS — the arm that catches a fence which simply refuses everything.
	[Fact]
	public async Task StillLetTheReclaimingCallerFinalizeAfterTheLapsedOneIsRefused()
	{
		var (store, messageId, handlerType, termA, termB) = await LapsedReclaimAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;

		(await store.CompleteAsync(messageId, handlerType, termA, ct).ConfigureAwait(false)).ShouldBeFalse();

		(await store.CompleteAsync(messageId, handlerType, termB, ct).ConfigureAwait(false)).ShouldBeTrue(
			"the live holder must still finalize after the lapsed caller was fenced out");
		(await store.IsProcessedAsync(messageId, handlerType, ct).ConfigureAwait(false)).ShouldBeTrue();
	}

	// MONOTONICITY, measured on the server clock rather than inspected.
	//
	// Reclaim admits only when the recorded expiry is STRICTLY less than $$NOW, and the replacement is that
	// same $$NOW plus a non-negative duration. Relaxing that one comparison to $lte would let a reclaim
	// reissue the same term and the fence would stop discriminating without failing.
	[Fact]
	public async Task IssueAStrictlyGreaterTermToTheReclaimingCaller()
	{
		var (_, _, _, termA, termB) = await LapsedReclaimAsync().ConfigureAwait(false);

		termB.ShouldNotBe(termA, "a reclaim that reissued the same term would fence nothing");

		var a = long.Parse(termA.Value, CultureInfo.InvariantCulture);
		var b = long.Parse(termB.Value, CultureInfo.InvariantCulture);

		b.ShouldBeGreaterThan(a,
			"the reclaimed term must be STRICTLY greater than the one it displaced — that is what makes the "
			+ "value an identity rather than merely a deadline");
	}

	private MongoDbInboxStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available — the real-infra fencing lock is never skipped.");

		var options = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			CollectionName = "inbox_lease_fencing_test",
		});

		return new MongoDbInboxStore(options, NullLogger<MongoDbInboxStore>.Instance, SingleTenantTestContext.Instance);
	}
}
