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

	/// <summary>
	/// The projection key the arms that exercise ONE cursor address it under.
	/// </summary>
	/// <remarks>
	/// A cursor is addressed by the PAIR -- the subscription name and the projection key -- so an arm
	/// about compare-and-set still has to name a projection. The arms that care about two cursors name
	/// their own keys instead.
	/// </remarks>
	protected const string DefaultProjectionKey = "Proj.A";

	/// <summary>Builds one roster member with a distinguishable fingerprint.</summary>
	/// <param name="projectionKey">The member's projection key.</param>
	/// <param name="fingerprintFill">A single hexadecimal character repeated to form the fingerprint.</param>
	/// <param name="destination">The destination identity to record.</param>
	/// <returns>A valid member.</returns>
	protected static SubscriptionMember Member(string projectionKey, char fingerprintFill, string destination = "dest-a") =>
		new(projectionKey, new string(fingerprintFill, 64), destination);

    /// <summary>Retired work cannot recreate a cursor or remove a replacement lifetime.</summary>
    public virtual async Task RetiredBindings_ShouldNeverAcknowledgeOrRetireAReplacement()
    {
        var name = GenerateSubscriptionName();
        var store = CreateStore();
        var definition = Member("A", 'A');
        var old = (await store.BindSubscriptionAsync(name, [definition], CancellationToken.None).ConfigureAwait(false)).Members.Single();
        if (old.BindingId == Guid.Empty || !await store.ReleaseSubscriptionMemberAsync(name, "A", old.BindingId, CancellationToken.None).ConfigureAwait(false))
        {
            throw new TestFixtureAssertionException("A bound lifetime must have a nonempty identity and be releasable.");
        }
        var orphan = await CreateStore().AdvanceCheckpointAsync(name, "A", old.BindingId, null, 99, CancellationToken.None).ConfigureAwait(false);
        var replacement = (await CreateStore().BindSubscriptionAsync(name, [definition], CancellationToken.None).ConfigureAwait(false)).Members.Single();
        var stale = await store.AdvanceCheckpointAsync(name, "A", old.BindingId, null, 99, CancellationToken.None).ConfigureAwait(false);
        var retired = await store.ReleaseSubscriptionMemberAsync(name, "A", old.BindingId, CancellationToken.None).ConfigureAwait(false);
        var live = await store.AdvanceCheckpointAsync(name, "A", replacement.BindingId, null, 7, CancellationToken.None).ConfigureAwait(false);
        var current = (await CreateStore().ReadSubscriptionAsync(name, CancellationToken.None).ConfigureAwait(false)).Members.Single();
        if (orphan != CheckpointAdvanceOutcome.Superseded || stale != CheckpointAdvanceOutcome.Superseded
            || retired || live != CheckpointAdvanceOutcome.Advanced || replacement.BindingId == old.BindingId
            || current.BindingId != replacement.BindingId || current.Position != 7)
        {
            throw new TestFixtureAssertionException("Retired work crossed the binding lifetime boundary.");
        }
    }

	/// <summary>An active reader pins its lifetime; switching is gated and rejects stale revisions including rollback ABA.</summary>
	public virtual async Task GenerationPointer_ShouldGateSwitchAndProtectReferencedLifetime()
	{
		var name = GenerateSubscriptionName();
		var store = CreateStore();
		var routes = (IProjectionGenerationStore)store;
		var roster = await store.BindSubscriptionAsync(name, [Member("A", 'A') with { DestinationScope = "cluster/A" }, Member("B", 'B') with { DestinationScope = "cluster/B" }], CancellationToken.None).ConfigureAwait(false);
		var a = roster.Members.Single(m => m.ProjectionKey == "A");
		var b = roster.Members.Single(m => m.ProjectionKey == "B");
		if (await routes.TrySetGenerationAsync(name, "reader", null, "A", a.BindingId, 0, CancellationToken.None).ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException("An absent cursor must never qualify, even at a zero threshold.");
		}
		_ = await store.AdvanceCheckpointAsync(name, "A", a.BindingId, null, 10, CancellationToken.None).ConfigureAwait(false);
		_ = await store.AdvanceCheckpointAsync(name, "B", b.BindingId, null, 9, CancellationToken.None).ConfigureAwait(false);
		if (!await routes.TrySetGenerationAsync(name, "reader", null, "A", a.BindingId, 10, CancellationToken.None).ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException("A caught-up lifetime must be routable.");
		}
        // SQL Server pads trailing spaces during equality comparisons. Reject those names
        // consistently before I/O so no provider aliases two logically different readers.
        foreach (var invalidName in new[] { " reader", "reader " })
        {
            var rejected = false;
            try { _ = await routes.ReadGenerationAsync(name, invalidName, CancellationToken.None).ConfigureAwait(false); }
            catch (ArgumentException) { rejected = true; }
            if (!rejected) { throw new TestFixtureAssertionException("Whitespace-padded reader names must be rejected."); }
        }
        if (await routes.TrySetGenerationAsync(GenerateSubscriptionName(), "reader", null, "A", a.BindingId, 10, CancellationToken.None).ConfigureAwait(false))
        {
            throw new TestFixtureAssertionException("A pointer must not borrow another subscription's lifetime.");
        }
		var first = await routes.ReadGenerationAsync(name, "reader", CancellationToken.None).ConfigureAwait(false)
			?? throw new TestFixtureAssertionException("Installed pointer was missing.");
		if (first.Member.DestinationScope != "cluster/A" || first.Member.BindingId != a.BindingId || first.Member.Position != 10
			|| await store.ReleaseSubscriptionMemberAsync(name, "A", a.BindingId, CancellationToken.None).ConfigureAwait(false)
			|| await routes.TrySetGenerationAsync(name, "reader", first.Revision, "B", b.BindingId, 10, CancellationToken.None).ConfigureAwait(false)
			|| await routes.TrySetGenerationAsync(name, "reader", first.Revision, "B", Guid.NewGuid(), 9, CancellationToken.None).ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException("Routing accepted an unqualified lifetime or allowed active retirement.");
		}
		if (!await routes.TrySetGenerationAsync(name, "reader", first.Revision, "B", b.BindingId, 9, CancellationToken.None).ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException("Matching pointer transition was refused.");
		}
		var second = (await routes.ReadGenerationAsync(name, "reader", CancellationToken.None).ConfigureAwait(false))!;
		if (!await routes.TrySetGenerationAsync(name, "reader", second.Revision, "A", a.BindingId, 10, CancellationToken.None).ConfigureAwait(false)
			|| await routes.TrySetGenerationAsync(name, "reader", first.Revision, "B", b.BindingId, 9, CancellationToken.None).ConfigureAwait(false)
			|| await routes.TryRemoveGenerationAsync(name, "reader", first.Revision, CancellationToken.None).ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException("Rollback reused a revision and accepted a stale operation.");
		}
        if (!await routes.TrySetGenerationAsync(name, "second-reader", null, "A", a.BindingId, 10, CancellationToken.None).ConfigureAwait(false))
        {
            throw new TestFixtureAssertionException("Independent readers must be able to pin the same generation.");
        }
		var current = (await routes.ReadGenerationAsync(name, "reader", CancellationToken.None).ConfigureAwait(false))!;
        if (!await routes.TryRemoveGenerationAsync(name, "reader", current.Revision, CancellationToken.None).ConfigureAwait(false)
            || await store.ReleaseSubscriptionMemberAsync(name, "A", a.BindingId, CancellationToken.None).ConfigureAwait(false))
        {
            throw new TestFixtureAssertionException("Removing one reader must not remove another reader's lifetime pin.");
        }
        var other = (await routes.ReadGenerationAsync(name, "second-reader", CancellationToken.None).ConfigureAwait(false))!;
        // A lost acknowledgment must be reconciled by reading; replaying an old create/update
        // cannot overwrite the winner, including after deletion and recreation of the same name.
        if (!await routes.TrySetGenerationAsync(name, "reader", null, "A", a.BindingId, 10, CancellationToken.None).ConfigureAwait(false))
        {
            throw new TestFixtureAssertionException("A decommissioned reader name must be reusable.");
        }
        var recreated = (await routes.ReadGenerationAsync(name, "reader", CancellationToken.None).ConfigureAwait(false))!;
        if (recreated.Revision == current.Revision
            || await routes.TrySetGenerationAsync(name, "reader", null, "B", b.BindingId, 9, CancellationToken.None).ConfigureAwait(false)
            || await routes.TrySetGenerationAsync(name, "reader", current.Revision, "B", b.BindingId, 9, CancellationToken.None).ConfigureAwait(false)
            || await routes.TryRemoveGenerationAsync(name, "reader", current.Revision, CancellationToken.None).ConfigureAwait(false)
            || !await routes.TryRemoveGenerationAsync(name, "reader", recreated.Revision, CancellationToken.None).ConfigureAwait(false))
        {
            throw new TestFixtureAssertionException("Deletion and recreation accepted a stale or duplicate request.");
        }
        if (!await routes.TryRemoveGenerationAsync(name, "second-reader", other.Revision, CancellationToken.None).ConfigureAwait(false)
            || !await store.ReleaseSubscriptionMemberAsync(name, "A", a.BindingId, CancellationToken.None).ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException("Decommissioned reader left an unreachable lifetime pin.");
		}
	}

	/// <summary>A switch racing retirement may win or lose, but cannot leave a dangling pointer.</summary>
	public virtual async Task GenerationPointer_ConcurrentRetirement_ShouldNeverLeaveDanglingReference()
	{
		var name = GenerateSubscriptionName();
		var store = CreateStore();
		var routes = (IProjectionGenerationStore)CreateStore();
		var member = (await store.BindSubscriptionAsync(name, [Member("A", 'A')], CancellationToken.None).ConfigureAwait(false)).Members.Single();
		_ = await store.AdvanceCheckpointAsync(name, "A", member.BindingId, null, 10, CancellationToken.None).ConfigureAwait(false);
		#pragma warning disable RS0030 // Conformance actors deliberately race independent store operations.
		var install = Task.Run(() => routes.TrySetGenerationAsync(name, "reader", null, "A", member.BindingId, 10, CancellationToken.None));
		var retire = Task.Run(() => store.ReleaseSubscriptionMemberAsync(name, "A", member.BindingId, CancellationToken.None));
		#pragma warning restore RS0030
		var outcomes = await Task.WhenAll(install, retire).ConfigureAwait(false);
		if (outcomes[0] == outcomes[1])
		{
			throw new TestFixtureAssertionException("Exactly one of pointer installation and lifetime retirement must win.");
		}
		var pointer = await routes.ReadGenerationAsync(name, "reader", CancellationToken.None).ConfigureAwait(false);
		var remaining = await store.ReadSubscriptionAsync(name, CancellationToken.None).ConfigureAwait(false);
		if ((outcomes[0] && (pointer?.Member.BindingId != member.BindingId || remaining.Members.Count != 1))
			|| (outcomes[1] && (pointer is not null || remaining.Members.Count != 0)))
		{
			throw new TestFixtureAssertionException("Pointer and member lifetimes diverged after concurrent retirement.");
		}
	}

	/// <summary>Identical processors may join without inventing a checkpoint at zero.</summary>
	public virtual async Task Bind_IdenticalRoster_ShouldJoinWithoutCreatingCheckpoint()
	{
		var name = GenerateSubscriptionName();
		SubscriptionMember[] roster = [Member("Proj.A", 'A')];
		_ = await CreateStore().BindSubscriptionAsync(name, roster, CancellationToken.None).ConfigureAwait(false);
		_ = await CreateStore().BindSubscriptionAsync(name, roster, CancellationToken.None).ConfigureAwait(false);

		var checkpoint = await CreateStore()
			.GetCheckpointAsync(name, "Proj.A", CancellationToken.None)
			.ConfigureAwait(false);
		if (checkpoint is not null)
		{
			throw new TestFixtureAssertionException("Binding a roster must not create a checkpoint.");
		}
	}

	/// <summary>
	/// ADDING A PROJECTION IS ADDITIVE. The new member binds, and no sibling's cursor moves.
	/// </summary>
	/// <remarks>
	/// This is the arm the whole design exists for. Under a set-wide fingerprint, adding a projection
	/// refused every cooperating host and the only remedy re-folded every projection from zero. Here the
	/// sibling's checkpoint must be observably untouched — not merely unrefused.
	/// </remarks>
	public virtual async Task Bind_NewMember_ShouldBeAdditive_AndLeaveSiblingCheckpointsUntouched()
	{
		var name = GenerateSubscriptionName();
		var aCursor = "Proj.A";

		_ = await CreateStore().BindSubscriptionAsync(name, [Member("Proj.A", 'A')], CancellationToken.None)
			.ConfigureAwait(false);
		_ = await CreateStore().AdvanceBoundCheckpointAsync(name, aCursor, null, 41, CancellationToken.None).ConfigureAwait(false);

		// The second deployment hosts both. The first member's fingerprint is unchanged.
		var roster = await CreateStore()
			.BindSubscriptionAsync(name, [Member("Proj.A", 'A'), Member("Proj.B", 'B')], CancellationToken.None)
			.ConfigureAwait(false);

		if (roster.Members.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Adding a projection must bind additively; the roster reported {roster.Members.Count} member(s), expected 2.");
		}

		if (await CreateStore().GetCheckpointAsync(name, aCursor, CancellationToken.None).ConfigureAwait(false) != 41)
		{
			throw new TestFixtureAssertionException(
				"Adding a projection must leave an existing projection's cursor exactly where it was.");
		}

		if (await CreateStore()
			.GetCheckpointAsync(name, "Proj.B", CancellationToken.None)
			.ConfigureAwait(false) is not null)
		{
			throw new TestFixtureAssertionException("A newly bound member must have no checkpoint, not a zero.");
		}
	}

	/// <summary>
	/// A changed fingerprint refuses THAT projection, names it, and leaves its siblings bound.
	/// </summary>
	public virtual async Task Bind_ChangedMemberFingerprint_ShouldRefuseNamingThatProjection_AndLeaveOthersBound()
	{
		var name = GenerateSubscriptionName();
		var aCursor = "Proj.A";
		_ = await CreateStore()
			.BindSubscriptionAsync(name, [Member("Proj.A", 'A'), Member("Proj.B", 'B')], CancellationToken.None)
			.ConfigureAwait(false);
		_ = await CreateStore().AdvanceBoundCheckpointAsync(name, aCursor, null, 7, CancellationToken.None).ConfigureAwait(false);

		try
		{
			// Proj.B's destination moved; Proj.A is unchanged.
			_ = await CreateStore().BindSubscriptionAsync(
				name,
				[Member("Proj.A", 'A'), Member("Proj.B", 'C', "dest-moved")],
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException ex)
		{
			// The message must name the projection, which is the diagnosis a set-wide fingerprint could
			// never give: it held two opaque hashes and left the consumer to bisect their registrations.
			if (!ex.Message.Contains("Proj.B", StringComparison.Ordinal))
			{
				throw new TestFixtureAssertionException(
					$"A refusal must name the diverged projection. Message was: {ex.Message}");
			}

			// The matching sibling stays bound and its cursor is untouched.
			_ = await CreateStore().BindSubscriptionAsync(name, [Member("Proj.A", 'A')], CancellationToken.None)
				.ConfigureAwait(false);
			if (await CreateStore().GetCheckpointAsync(name, aCursor, CancellationToken.None).ConfigureAwait(false) != 7)
			{
				throw new TestFixtureAssertionException("A refused member must not alter a sibling's checkpoint.");
			}

			return;
		}

		throw new TestFixtureAssertionException("A changed member fingerprint must be refused.");
	}

	/// <summary>
	/// The roster returns members the caller never supplied — the only way a process learns of work it
	/// does not host, and therefore cannot attest.
	/// </summary>
	public virtual async Task Bind_ShouldReturnRosterIncludingMembersTheCallerDidNotSupply()
	{
		var name = GenerateSubscriptionName();
		_ = await CreateStore()
			.BindSubscriptionAsync(name, [Member("Proj.A", 'A'), Member("Proj.B", 'B')], CancellationToken.None)
			.ConfigureAwait(false);

		// A second deployment hosting only one of them must still be told the other exists.
		var roster = await CreateStore()
			.BindSubscriptionAsync(name, [Member("Proj.A", 'A')], CancellationToken.None).ConfigureAwait(false);

		if (!roster.Members.Any(static member => string.Equals(member.ProjectionKey, "Proj.B", StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				"The roster must include members the caller did not supply; otherwise a process cannot "
				+ "discover work it is unable to attest.");
		}
	}

	/// <summary>Independent instances cannot both establish different fingerprints for ONE member.</summary>
	public virtual async Task ConcurrentBindings_OfOneMember_WithDifferentFingerprints_ShouldAcceptExactlyOne()
	{
		var name = GenerateSubscriptionName();
		var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		async Task<bool> Attempt(char fill)
		{
			await start.Task.ConfigureAwait(false);
			try
			{
				_ = await CreateStore().BindSubscriptionAsync(name, [Member("Proj.A", fill)], CancellationToken.None)
					.ConfigureAwait(false);
				return true;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}

		var first = Attempt('A');
		var second = Attempt('B');
		start.SetResult();
		var answers = await Task.WhenAll(first, second).ConfigureAwait(false);
		if (answers.Count(static accepted => accepted) != 1)
		{
			throw new TestFixtureAssertionException(
				"Exactly one fingerprint must win the atomic claim for a member; a claim is not an upsert.");
		}
	}

	/// <summary>
	/// Release removes the member AND its checkpoint, in one durable step.
	/// </summary>
	/// <remarks>
	/// The atomicity is the property. A checkpoint surviving its member is a cursor nothing validates and
	/// no operation on this contract can clear; a member surviving its checkpoint re-folds from zero onto
	/// a destination still holding the previous generation's rows.
	/// </remarks>
	public virtual async Task Release_ShouldRemoveMemberAndItsCheckpoint_Atomically()
	{
		var name = GenerateSubscriptionName();
		var aCursor = "Proj.A";
		var bCursor = "Proj.B";
		_ = await CreateStore()
			.BindSubscriptionAsync(name, [Member("Proj.A", 'A'), Member("Proj.B", 'B')], CancellationToken.None)
			.ConfigureAwait(false);
		_ = await CreateStore().AdvanceBoundCheckpointAsync(name, aCursor, null, 11, CancellationToken.None).ConfigureAwait(false);
		_ = await CreateStore().AdvanceBoundCheckpointAsync(name, bCursor, null, 22, CancellationToken.None).ConfigureAwait(false);

		if (!await CreateStore().ReleaseBoundMemberAsync(name, "Proj.A", CancellationToken.None)
			.ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException("Releasing a bound member must report true.");
		}

		if (await CreateStore().GetCheckpointAsync(name, aCursor, CancellationToken.None).ConfigureAwait(false) is not null)
		{
			throw new TestFixtureAssertionException(
				"Release must remove the member's checkpoint; a surviving cursor is an orphan nothing can clear.");
		}

		if (await CreateStore().GetCheckpointAsync(name, bCursor, CancellationToken.None).ConfigureAwait(false) != 22)
		{
			throw new TestFixtureAssertionException("Release must not touch a sibling's checkpoint.");
		}

		// The released member re-binds as new, which is what makes a per-projection rebuild expressible.
		// 'F', not 'Z': a fingerprint is 64 UPPERCASE HEXADECIMAL characters, and the contract validates
		// that before touching storage. This arm originally used 'Z' and was refused by our own
		// validation — the first run of these arms caught it, which is the point of running them.
		var roster = await CreateStore()
			.BindSubscriptionAsync(name, [Member("Proj.A", 'F')], CancellationToken.None).ConfigureAwait(false);
		if (!roster.Members.Any(static member =>
			string.Equals(member.ProjectionKey, "Proj.A", StringComparison.Ordinal)
			&& member.DefinitionHash[0] == 'F'))
		{
			throw new TestFixtureAssertionException(
				"A released member must re-bind with a new fingerprint rather than resurrecting the old one.");
		}
	}

	/// <summary>Releasing an absent member reports false and writes nothing.</summary>
	public virtual async Task Release_AbsentMember_ShouldReturnFalse_AndWriteNothing()
	{
		var name = GenerateSubscriptionName();
		var aCursor = "Proj.A";
		_ = await CreateStore().BindSubscriptionAsync(name, [Member("Proj.A", 'A')], CancellationToken.None)
			.ConfigureAwait(false);
		_ = await CreateStore().AdvanceBoundCheckpointAsync(name, aCursor, null, 5, CancellationToken.None).ConfigureAwait(false);

		if (await CreateStore().ReleaseBoundMemberAsync(name, "Proj.NotBound", CancellationToken.None)
			.ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException("Releasing an unbound member must report false.");
		}

		if (await CreateStore().GetCheckpointAsync(name, aCursor, CancellationToken.None).ConfigureAwait(false) != 5)
		{
			throw new TestFixtureAssertionException(
				"A release that found nothing must write nothing, including no checkpoint deletion.");
		}
	}

	/// <summary>Subscription names are ordinal keys even for roster binding.</summary>
	public virtual async Task Bind_CaseDistinctNames_ShouldRemainIndependent()
	{
		var name = "case-" + GenerateSubscriptionName();
		_ = await CreateStore()
			.BindSubscriptionAsync(name.ToUpperInvariant(), [Member("Proj.A", 'A')], CancellationToken.None)
			.ConfigureAwait(false);
		_ = await CreateStore()
			.BindSubscriptionAsync(name, [Member("Proj.A", 'B')], CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Projection keys are ordinal too, which a provider gets wrong by accepting a default collation.
	/// </summary>
	/// <remarks>
	/// A case-insensitive key column collapses two legal members into one row, so the second member's
	/// fingerprint is compared against the first's and refused — a refusal naming a projection the
	/// consumer never changed.
	/// </remarks>
	public virtual async Task Bind_CaseDistinctProjectionKeys_ShouldRemainIndependent()
	{
		var name = GenerateSubscriptionName();
		var roster = await CreateStore()
			.BindSubscriptionAsync(name, [Member("Proj.Casing", 'A'), Member("proj.casing", 'B')], CancellationToken.None)
			.ConfigureAwait(false);

		if (roster.Members.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Case-distinct projection keys are distinct members; the roster reported {roster.Members.Count}, expected 2. "
				+ "A case-insensitive key collation collapses them.");
		}
	}

	/// <summary>An unknown subscription has no checkpoint, reported as null rather than zero.</summary>
	/// <remarks>
	/// Zero is the before-first-event position. A stored zero and an absent checkpoint are distinct
	/// compare-and-set states; a create must not overwrite an existing zero.
	/// </remarks>
	public virtual async Task GetCheckpoint_ForUnknownSubscription_ShouldReturnNull()
	{
		var checkpoint = await CreateStore()
			.GetCheckpointAsync(GenerateSubscriptionName(), DefaultProjectionKey, CancellationToken.None)
			.ConfigureAwait(false);

		if (checkpoint is not null)
		{
			throw new TestFixtureAssertionException(
				$"An unknown subscription must report null, not {checkpoint}. Reporting 0 would be "
				+ "indistinguishable from a subscription with a stored before-first-event checkpoint.");
		}
	}

	/// <summary>A first advance, expecting no prior checkpoint, is accepted and is durable.</summary>
	public virtual async Task Advance_FromNoCheckpoint_ShouldBeAcceptedAndPersist()
	{
		var store = CreateStore();
		var name = GenerateSubscriptionName();

		var outcome = await store
			.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, expectedPosition: null, newPosition: 7, CancellationToken.None)
			.ConfigureAwait(false);

		if (outcome != CheckpointAdvanceOutcome.Advanced)
		{
			throw new TestFixtureAssertionException(
				$"A first advance over no prior checkpoint must be Advanced but was {outcome}.");
		}

		// Read back through a SEPARATE instance. A store that kept the value in a field would satisfy
		// the outcome assertion above and fail here, which is the whole point of a durable store.
		var readBack = await CreateStore().GetCheckpointAsync(name, DefaultProjectionKey, CancellationToken.None)
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

		_ = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, null, 3, CancellationToken.None).ConfigureAwait(false);

		var outcome = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, 3, 9, CancellationToken.None)
			.ConfigureAwait(false);

		if (outcome != CheckpointAdvanceOutcome.Advanced)
		{
			throw new TestFixtureAssertionException(
				$"An advance stating the current position must be Advanced but was {outcome}.");
		}

		var readBack = await CreateStore().GetCheckpointAsync(name, DefaultProjectionKey, CancellationToken.None)
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

		_ = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, null, 10, CancellationToken.None).ConfigureAwait(false);
		_ = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, 10, 20, CancellationToken.None).ConfigureAwait(false);

		// A second instance still believes the checkpoint is 10 and tries to move it to 15.
		var outcome = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, 10, 15, CancellationToken.None)
			.ConfigureAwait(false);

		if (outcome != CheckpointAdvanceOutcome.Superseded)
		{
			throw new TestFixtureAssertionException(
				$"An advance from a stale expected position must be Superseded but was {outcome}.");
		}

		var readBack = await CreateStore().GetCheckpointAsync(name, DefaultProjectionKey, CancellationToken.None)
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

		_ = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, null, 42, CancellationToken.None).ConfigureAwait(false);

		var outcome = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, null, 5, CancellationToken.None)
			.ConfigureAwait(false);

		if (outcome != CheckpointAdvanceOutcome.Superseded)
		{
			throw new TestFixtureAssertionException(
				$"An advance expecting no checkpoint must be Superseded once one exists but was {outcome}. "
				+ "An upsert here lets a late-starting instance reset a live subscription to its own "
				+ "position.");
		}

		var readBack = await CreateStore().GetCheckpointAsync(name, DefaultProjectionKey, CancellationToken.None)
			.ConfigureAwait(false);

		if (readBack != 42)
		{
			throw new TestFixtureAssertionException(
				$"The refused advance must leave the checkpoint at 42 but it is now {readBack}.");
		}
	}

	/// <summary>
	/// Under concurrent strictly increasing advances from the same prior position, EXACTLY ONE is accepted.
	/// </summary>
	/// <remarks>
	/// The arm a mock cannot satisfy and a read-then-write implementation cannot pass reliably. Both
	/// callers read the same prior value and both believe they may advance; the store must decide. If
	/// more than one strict advance is told Advanced, the comparison was not atomic. Equal-position
	/// proposals may all succeed; none of these outcomes grants exclusive processing ownership.
	/// </remarks>
	public virtual async Task ConcurrentAdvances_FromTheSamePosition_ShouldAcceptExactlyOne()
	{
		const int Racers = 8;

		var store = CreateStore();
		var name = GenerateSubscriptionName();

		_ = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, null, 100, CancellationToken.None).ConfigureAwait(false);

		var outcomes = await Task.WhenAll(
			Enumerable.Range(1, Racers).Select(i =>
				CreateStore().AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, 100, 100 + i, CancellationToken.None)))
			.ConfigureAwait(false);

		var accepted = outcomes.Count(o => o == CheckpointAdvanceOutcome.Advanced);
		if (accepted != 1)
		{
			throw new TestFixtureAssertionException(
				$"Exactly one of {Racers} concurrent advances from position 100 must be Advanced, but "
				+ $"{accepted} were. More than one means the store is read-then-write rather than "
				+ "compare-and-set, and the losers' progress silently overwrites the winner's.");
		}

		var readBack = await CreateStore().GetCheckpointAsync(name, DefaultProjectionKey, CancellationToken.None)
			.ConfigureAwait(false);

		if (readBack is not { } final || final <= 100 || final > 100 + Racers)
		{
			throw new TestFixtureAssertionException(
				$"The surviving checkpoint must be one of the racers' values but is "
				+ $"{readBack?.ToString() ?? "null"}.");
		}
	}

	/// <summary>Invalid proposals throw without creating or changing a checkpoint.</summary>
	public virtual async Task Advance_WithInvalidPositions_ShouldThrowAndChangeNothing()
	{
		var store = CreateStore();
		var existing = GenerateSubscriptionName();
		var absent = GenerateSubscriptionName();
		_ = await store.AdvanceBoundCheckpointAsync(existing, DefaultProjectionKey, null, 10, CancellationToken.None).ConfigureAwait(false);
		(long? Expected, long Next, string Parameter)[] invalid =
		[
			(null, -1, "newPosition"),
			(-1, 0, "expectedPosition"),
			(10, 5, "newPosition"),
			(20, 5, "newPosition"),
		];
		foreach (var name in new[] { existing, absent })
		{
			foreach (var proposal in invalid)
			{
				try
				{
					_ = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, proposal.Expected, proposal.Next, CancellationToken.None)
						.ConfigureAwait(false);
				}
				catch (ArgumentOutOfRangeException exception) when (exception.ParamName == proposal.Parameter)
				{
					continue;
				}

				throw new TestFixtureAssertionException($"Invalid proposal {proposal.Expected} -> {proposal.Next} was not rejected.");
			}
		}

		if (await CreateStore().GetCheckpointAsync(existing, DefaultProjectionKey, CancellationToken.None).ConfigureAwait(false) != 10
			|| await CreateStore().GetCheckpointAsync(absent, DefaultProjectionKey, CancellationToken.None).ConfigureAwait(false) is not null)
		{
			throw new TestFixtureAssertionException("Invalid proposals changed checkpoint storage.");
		}
	}

	/// <summary>Equal proposals compare atomically, including zero and the largest position.</summary>
	public virtual async Task Advance_WithEqualPositions_ShouldCompareWithoutClaimingOwnership()
	{
		var store = CreateStore();
		var name = GenerateSubscriptionName();
		var created = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, null, 0, CancellationToken.None).ConfigureAwait(false);
		var duplicateCreate = await CreateStore().AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, null, 0, CancellationToken.None).ConfigureAwait(false);
		var equal = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
			CreateStore().AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, 0, 0, CancellationToken.None))).ConfigureAwait(false);
		var moved = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, 0, long.MaxValue, CancellationToken.None).ConfigureAwait(false);
		var staleEqual = await CreateStore().AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, 0, 0, CancellationToken.None).ConfigureAwait(false);
		var maximum = await CreateStore().AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, long.MaxValue, long.MaxValue, CancellationToken.None)
			.ConfigureAwait(false);
		var readBack = await CreateStore().GetCheckpointAsync(name, DefaultProjectionKey, CancellationToken.None).ConfigureAwait(false);

		if (created != CheckpointAdvanceOutcome.Advanced || duplicateCreate != CheckpointAdvanceOutcome.Superseded
			|| equal.Any(outcome => outcome != CheckpointAdvanceOutcome.Advanced)
			|| moved != CheckpointAdvanceOutcome.Advanced || staleEqual != CheckpointAdvanceOutcome.Superseded
			|| maximum != CheckpointAdvanceOutcome.Advanced || readBack != long.MaxValue)
		{
			throw new TestFixtureAssertionException("Equal-position proposals must compare the actual stored state without overflow or exclusive ownership.");
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

		_ = await store.AdvanceBoundCheckpointAsync(name, DefaultProjectionKey, null, 55, CancellationToken.None).ConfigureAwait(false);

		var all = await CreateStore().EnumerateCheckpointsAsync(CancellationToken.None).ConfigureAwait(false);
		var mine = all
			.Where(c => string.Equals(c.SubscriptionName, name, StringComparison.Ordinal)
				&& string.Equals(c.ProjectionKey, DefaultProjectionKey, StringComparison.Ordinal))
			.ToList();

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
	/// <summary>
	/// A pair whose two parts are each legal but whose CONCATENATION exceeds the subscription-name
	/// width round-trips a cursor.
	/// </summary>
	/// <remarks>
	/// <para>
	/// THIS IS THE DEFECT'S OWN SHAPE, which is why it is an arm rather than a comment. The contract
	/// bounds the subscription name at 255 characters and the projection key at 512, so a composed
	/// '{name}:{key}' reaches 768 -- past every column wide enough for the name alone. A store that
	/// addresses its cursor by one composed string therefore BINDS the member successfully (both inputs
	/// are individually legal and that table's columns are 255 and 512) and can then never write its
	/// cursor. The member is bound, its progress is unrecordable, and nothing reports a fault: a
	/// permanent stall rather than a refusal.
	/// </para>
	/// <para>
	/// Where the provider truncates instead of refusing -- SQL Server with ANSI_WARNINGS OFF is the
	/// case in point -- it is worse than a stall: two projections whose composed names agree on their
	/// first 255 characters share one cursor, and each skips whatever the other consumed.
	/// </para>
	/// </remarks>
	public virtual async Task Advance_WhenNameAndKeyTogetherExceedTheNameWidth_ShouldRoundTrip()
	{
		// At the contract's own caps: a 255-character name and a 512-character key, composing to 768.
		var name = ("long-" + GenerateSubscriptionName()).PadRight(255, 'n')[..255];
		var projectionKey = ("Deeply.Namespaced.Generic.Projection." + Guid.NewGuid().ToString("N"))
			.PadRight(512, 'k')[..512];

		_ = await CreateStore()
			.BindSubscriptionAsync(name, [Member(projectionKey, 'A')], CancellationToken.None)
			.ConfigureAwait(false);

		var created = await CreateStore()
			.AdvanceBoundCheckpointAsync(name, projectionKey, null, 17, CancellationToken.None)
			.ConfigureAwait(false);
		if (created != CheckpointAdvanceOutcome.Advanced)
		{
			throw new TestFixtureAssertionException(
				$"A cursor whose name and key are each within the contract's caps must be creatable; the "
				+ $"store reported {created}. Composed into one column the pair is 768 characters, which "
				+ "no column sized for the name alone can hold.");
		}

		var readBack = await CreateStore()
			.GetCheckpointAsync(name, projectionKey, CancellationToken.None)
			.ConfigureAwait(false);
		if (readBack != 17)
		{
			throw new TestFixtureAssertionException(
				$"The cursor must read back as 17 but was {readBack?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"}. "
				+ "A null here is a bound member whose progress can never be recorded; a different number "
				+ "is a truncated key sharing another projection's cursor.");
		}

		var advanced = await CreateStore()
			.AdvanceBoundCheckpointAsync(name, projectionKey, 17, 29, CancellationToken.None)
			.ConfigureAwait(false);
		if (advanced != CheckpointAdvanceOutcome.Advanced
			|| await CreateStore().GetCheckpointAsync(name, projectionKey, CancellationToken.None)
				.ConfigureAwait(false) != 29)
		{
			throw new TestFixtureAssertionException(
				"The compare-and-set must hold for an oversize pair exactly as it does for a short one.");
		}

		if (!await CreateStore().ReleaseBoundMemberAsync(name, projectionKey, CancellationToken.None)
			.ConfigureAwait(false)
			|| await CreateStore().GetCheckpointAsync(name, projectionKey, CancellationToken.None)
				.ConfigureAwait(false) is not null)
		{
			throw new TestFixtureAssertionException(
				"Release must remove an oversize pair's cursor too; a key the delete cannot address is an "
				+ "orphan no operation on this contract can clear.");
		}
	}

	/// <summary>
	/// Two case-distinct projection keys are two CURSORS, not one.
	/// </summary>
	/// <remarks>
	/// The key half of the cursor is compared ordinally, exactly as the member row's is. A provider that
	/// accepts a case-insensitive default collation on this column collapses two legal cursors into one,
	/// and each projection then skips whatever the other folded -- events that are real, in order, and
	/// already consumed, so nothing downstream can detect it.
	/// </remarks>
	public virtual async Task Advance_CaseDistinctProjectionKeys_ShouldRemainDistinctCursors()
	{
		var name = GenerateSubscriptionName();
		_ = await CreateStore()
			.BindSubscriptionAsync(
				name, [Member("Proj.Casing", 'A'), Member("proj.casing", 'B')], CancellationToken.None)
			.ConfigureAwait(false);

		_ = await CreateStore().AdvanceBoundCheckpointAsync(name, "Proj.Casing", null, 3, CancellationToken.None)
			.ConfigureAwait(false);
		var lower = await CreateStore()
			.AdvanceBoundCheckpointAsync(name, "proj.casing", null, 9, CancellationToken.None)
			.ConfigureAwait(false);

		if (lower != CheckpointAdvanceOutcome.Advanced)
		{
			throw new TestFixtureAssertionException(
				$"A case-distinct projection key is a distinct cursor and its creation must be accepted; "
				+ $"the store reported {lower}, which means the two keys collided on one row.");
		}

		var upperMark = await CreateStore()
			.GetCheckpointAsync(name, "Proj.Casing", CancellationToken.None).ConfigureAwait(false);
		var lowerMark = await CreateStore()
			.GetCheckpointAsync(name, "proj.casing", CancellationToken.None).ConfigureAwait(false);

		if (upperMark != 3 || lowerMark != 9)
		{
			throw new TestFixtureAssertionException(
				$"Case-distinct keys must hold independent cursors; read back {upperMark} and {lowerMark}, "
				+ "expected 3 and 9. A case-insensitive key collation shares one cursor between them.");
		}
	}
}

// Test setup for the pre-existing position-CAS arms. Production callers must retain the
// binding they observed before processing; they must never resolve a fresh token to acknowledge old work.
internal static class SubscriptionCheckpointConformanceSetup
{
    internal static async Task<CheckpointAdvanceOutcome> AdvanceBoundCheckpointAsync(
        this ISubscriptionCheckpointStore store, string subscriptionName, string projectionKey,
        long? expectedPosition, long newPosition, CancellationToken cancellationToken)
    {
        var roster = await store.ReadSubscriptionAsync(subscriptionName, cancellationToken).ConfigureAwait(false);
        var member = roster.Members.FirstOrDefault(m => m.ProjectionKey == projectionKey);
        if (member is null)
        {
            roster = await store.BindSubscriptionAsync(subscriptionName,
                [new SubscriptionMember(projectionKey, new string('A', 64), "dest-a")], cancellationToken).ConfigureAwait(false);
            member = roster.Members.Single(m => m.ProjectionKey == projectionKey);
        }
        return await store.AdvanceCheckpointAsync(subscriptionName, projectionKey, member.BindingId,
            expectedPosition, newPosition, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<bool> ReleaseBoundMemberAsync(this ISubscriptionCheckpointStore store,
        string subscriptionName, string projectionKey, CancellationToken cancellationToken)
    {
        var roster = await store.ReadSubscriptionAsync(subscriptionName, cancellationToken).ConfigureAwait(false);
        var member = roster.Members.FirstOrDefault(m => m.ProjectionKey == projectionKey);
        var bindingId = member?.BindingId ?? Guid.NewGuid();
        var released = await store.ReleaseSubscriptionMemberAsync(subscriptionName, projectionKey, bindingId, cancellationToken).ConfigureAwait(false);
        if (released)
        {
            var stale = await store.AdvanceCheckpointAsync(subscriptionName, projectionKey, bindingId, null, 999, cancellationToken).ConfigureAwait(false);
            if (stale != CheckpointAdvanceOutcome.Superseded)
            {
                throw new TestFixtureAssertionException("A retired binding recreated an orphan checkpoint.");
            }
        }
        return released;
    }
}
