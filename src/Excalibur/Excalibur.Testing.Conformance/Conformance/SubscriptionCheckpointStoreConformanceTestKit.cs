// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Conformance arms for <see cref="ISubscriptionCheckpointStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// A checkpoint is the one number a catch-up subscriber carries, and two instances of the same
/// subscription share it and race for it. The contract is therefore a COMPARE-AND-SET rather than a
/// write: the caller states the value it believes is current, and the store reports whether that belief
/// held. Every arm here exists to pin one half of that, because an implementation that merely stores and
/// returns a number satisfies the signature while losing the property.
/// </para>
/// <para>
/// <b>The failure this protects against.</b> If a losing advance is reported as success, the slower
/// instance overwrites the faster one's progress with a lower mark, and every event between the two
/// marks is delivered a second time. Nothing downstream can detect that — the events are real, in order,
/// and already processed.
/// </para>
/// <para>
/// Derive the kit once per provider and wire every arm. An unwired arm is indistinguishable from a
/// passing one.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class SubscriptionCheckpointStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates the store under test. Each call may return a new instance over the SAME durable storage;
	/// arms that need two instances say so.
	/// </summary>
	/// <returns>The store under test.</returns>
	protected abstract ISubscriptionCheckpointStore CreateStore();

	/// <summary>Generates a subscription name unique to one arm, so arms do not collide.</summary>
	/// <returns>A unique subscription name.</returns>
	protected virtual string GenerateSubscriptionName() => "sub-" + Guid.NewGuid().ToString("N");

	/// <summary>An unknown subscription has no checkpoint, reported as null rather than zero.</summary>
	/// <remarks>
	/// Zero is a legitimate position — it is what a subscriber that has processed the first event holds —
	/// so conflating "never started" with "at the beginning" would make a fresh subscription skip the
	/// first event forever.
	/// </remarks>
	public virtual async Task GetCheckpoint_ForUnknownSubscription_ShouldReturnNull()
	{
		var checkpoint = await CreateStore()
			.GetCheckpointAsync(GenerateSubscriptionName(), CancellationToken.None).ConfigureAwait(false);

		if (checkpoint is not null)
		{
			throw new TestFixtureAssertionException(
				$"An unknown subscription must report null, not {checkpoint}. Reporting 0 would be "
				+ "indistinguishable from a subscription that has processed the first event.");
		}
	}

	/// <summary>A first advance, expecting no prior checkpoint, is accepted and is durable.</summary>
	public virtual async Task Advance_FromNoCheckpoint_ShouldBeAcceptedAndPersist()
	{
		var store = CreateStore();
		var name = GenerateSubscriptionName();

		var outcome = await store
			.AdvanceCheckpointAsync(name, expectedPosition: null, newPosition: 7, CancellationToken.None)
			.ConfigureAwait(false);

		if (outcome != CheckpointAdvanceOutcome.Advanced)
		{
			throw new TestFixtureAssertionException(
				$"A first advance over no prior checkpoint must be Advanced but was {outcome}.");
		}

		// Read back through a SEPARATE instance. A store that kept the value in a field would satisfy
		// the outcome assertion above and fail here, which is the whole point of a durable store.
		var readBack = await CreateStore().GetCheckpointAsync(name, CancellationToken.None)
			.ConfigureAwait(false);

		if (readBack != 7)
		{
			throw new TestFixtureAssertionException(
				$"Expected the advanced checkpoint 7 to be readable from another instance but got "
				+ $"{readBack?.ToString() ?? "null"}. A checkpoint that does not outlive the instance "
				+ "makes a subscription replay the whole stream on every restart.");
		}
	}

	/// <summary>An advance stating the current position is accepted.</summary>
	public virtual async Task Advance_WithMatchingExpectedPosition_ShouldBeAccepted()
	{
		var store = CreateStore();
		var name = GenerateSubscriptionName();

		_ = await store.AdvanceCheckpointAsync(name, null, 3, CancellationToken.None).ConfigureAwait(false);

		var outcome = await store.AdvanceCheckpointAsync(name, 3, 9, CancellationToken.None)
			.ConfigureAwait(false);

		if (outcome != CheckpointAdvanceOutcome.Advanced)
		{
			throw new TestFixtureAssertionException(
				$"An advance stating the current position must be Advanced but was {outcome}.");
		}

		var readBack = await CreateStore().GetCheckpointAsync(name, CancellationToken.None)
			.ConfigureAwait(false);

		if (readBack != 9)
		{
			throw new TestFixtureAssertionException($"Expected 9 after the advance but got {readBack}.");
		}
	}

	/// <summary>An advance stating a position that is no longer current is REFUSED, and changes nothing.</summary>
	/// <remarks>
	/// The arm the contract exists for. A store that accepts this has let a stale instance rewind a live
	/// subscription, and every event between the two marks is redelivered.
	/// </remarks>
	public virtual async Task Advance_WithStaleExpectedPosition_ShouldBeSupersededAndChangeNothing()
	{
		var store = CreateStore();
		var name = GenerateSubscriptionName();

		_ = await store.AdvanceCheckpointAsync(name, null, 10, CancellationToken.None).ConfigureAwait(false);
		_ = await store.AdvanceCheckpointAsync(name, 10, 20, CancellationToken.None).ConfigureAwait(false);

		// A second instance still believes the checkpoint is 10 and tries to move it to 15.
		var outcome = await store.AdvanceCheckpointAsync(name, 10, 15, CancellationToken.None)
			.ConfigureAwait(false);

		if (outcome != CheckpointAdvanceOutcome.Superseded)
		{
			throw new TestFixtureAssertionException(
				$"An advance from a stale expected position must be Superseded but was {outcome}.");
		}

		var readBack = await CreateStore().GetCheckpointAsync(name, CancellationToken.None)
			.ConfigureAwait(false);

		if (readBack != 20)
		{
			throw new TestFixtureAssertionException(
				$"The refused advance must leave the checkpoint at 20 but it is now {readBack}. Reporting "
				+ "Superseded while still writing the value is worse than accepting it, because the "
				+ "caller is told its progress was not recorded when it overwrote someone else's.");
		}
	}

	/// <summary>
	/// An advance expecting NO checkpoint is refused once one exists, and changes nothing.
	/// </summary>
	/// <remarks>
	/// The two prior states are distinct: "I believe there is none" must lose to a writer that has since
	/// created one. An implementation that treats the null case as an upsert passes every other arm here
	/// and fails this one, which is why it is separate.
	/// </remarks>
	public virtual async Task Advance_ExpectingNoCheckpoint_WhenOneExists_ShouldBeSuperseded()
	{
		var store = CreateStore();
		var name = GenerateSubscriptionName();

		_ = await store.AdvanceCheckpointAsync(name, null, 42, CancellationToken.None).ConfigureAwait(false);

		var outcome = await store.AdvanceCheckpointAsync(name, null, 5, CancellationToken.None)
			.ConfigureAwait(false);

		if (outcome != CheckpointAdvanceOutcome.Superseded)
		{
			throw new TestFixtureAssertionException(
				$"An advance expecting no checkpoint must be Superseded once one exists but was {outcome}. "
				+ "An upsert here lets a late-starting instance reset a live subscription to its own "
				+ "position.");
		}

		var readBack = await CreateStore().GetCheckpointAsync(name, CancellationToken.None)
			.ConfigureAwait(false);

		if (readBack != 42)
		{
			throw new TestFixtureAssertionException(
				$"The refused advance must leave the checkpoint at 42 but it is now {readBack}.");
		}
	}

	/// <summary>
	/// Under concurrent advances from the same prior position, EXACTLY ONE is accepted.
	/// </summary>
	/// <remarks>
	/// The arm a mock cannot satisfy and a read-then-write implementation cannot pass reliably. Both
	/// callers read the same prior value and both believe they may advance; the store must decide. If
	/// more than one is told Advanced, two instances both believe they own the mark and the later write
	/// silently discards the other's progress.
	/// </remarks>
	public virtual async Task ConcurrentAdvances_FromTheSamePosition_ShouldAcceptExactlyOne()
	{
		const int Racers = 8;

		var store = CreateStore();
		var name = GenerateSubscriptionName();

		_ = await store.AdvanceCheckpointAsync(name, null, 100, CancellationToken.None).ConfigureAwait(false);

		var outcomes = await Task.WhenAll(
			Enumerable.Range(1, Racers).Select(i =>
				CreateStore().AdvanceCheckpointAsync(name, 100, 100 + i, CancellationToken.None)))
			.ConfigureAwait(false);

		var accepted = outcomes.Count(o => o == CheckpointAdvanceOutcome.Advanced);
		if (accepted != 1)
		{
			throw new TestFixtureAssertionException(
				$"Exactly one of {Racers} concurrent advances from position 100 must be Advanced, but "
				+ $"{accepted} were. More than one means the store is read-then-write rather than "
				+ "compare-and-set, and the losers' progress silently overwrites the winner's.");
		}

		var readBack = await CreateStore().GetCheckpointAsync(name, CancellationToken.None)
			.ConfigureAwait(false);

		if (readBack is not { } final || final <= 100 || final > 100 + Racers)
		{
			throw new TestFixtureAssertionException(
				$"The surviving checkpoint must be one of the racers' values but is "
				+ $"{readBack?.ToString() ?? "null"}.");
		}
	}

	/// <summary>Enumeration reports the checkpoints that exist, with their current positions.</summary>
	/// <remarks>
	/// LIVENESS for the arms above: a store that persisted nothing would satisfy several of the refusal
	/// assertions trivially, and this is what distinguishes "correctly refused" from "never wrote".
	/// </remarks>
	public virtual async Task Enumerate_ShouldReportStoredCheckpoints()
	{
		var store = CreateStore();
		var name = GenerateSubscriptionName();

		_ = await store.AdvanceCheckpointAsync(name, null, 55, CancellationToken.None).ConfigureAwait(false);

		var all = await CreateStore().EnumerateCheckpointsAsync(CancellationToken.None).ConfigureAwait(false);
		var mine = all.Where(c => string.Equals(c.SubscriptionName, name, StringComparison.Ordinal)).ToList();

		if (mine.Count != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected exactly one enumerated checkpoint for '{name}' but found {mine.Count}.");
		}

		if (mine[0].Position != 55)
		{
			throw new TestFixtureAssertionException(
				$"Expected the enumerated position to be 55 but was {mine[0].Position}.");
		}
	}
}
