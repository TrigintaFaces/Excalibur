// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Time.Testing;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Proves that the three lease-reclaim arms in <see cref="InboxStoreConformanceTestKit"/> actually BIND --
/// that each goes RED against the specific defect it names, and stays GREEN against the other two.
/// </summary>
/// <remarks>
/// <para>
/// The verdict matrix each test below pins, one cell at a time:
/// </para>
/// <code>
///                    ExpiredLease   LiveLease   LeaselessClaim
///   Correct          GREEN          GREEN       GREEN
///   NeverReclaims    RED            green       green
///   AlwaysReclaims   green          RED         RED
///   Undeclared       green          green       green      (arms do not run)
///   Advertised       RED            RED         RED        (declares nothing, claims the protocol)
/// </code>
/// <para>
/// The lower-case cells carry the argument. A store that never reclaims satisfies both SAFETY cells
/// perfectly -- a claim it refuses to hand over can never be handed to two callers -- so the safety cells
/// alone would certify a store that strands every message whose processor died. A store that reclaims
/// anything satisfies the LIVENESS cell perfectly, so that cell alone would certify a store that hands a
/// live claim to a second processor. Neither direction detects the other's defect, which is the whole
/// reason the contract needs all three and the reason each is asserted separately here: a single arm
/// bundling them could not distinguish "tightened until it refuses" from "loosened until it grants".
/// </para>
/// <para>
/// The last two rows are one property each, and bundling them is the hole this file was extended to
/// close. "The arms did not run" is what the Undeclared row pins; "the store is not certified as
/// leased" is what the Advertised row pins, and only the second of them fails when a store claims a
/// protocol it never implemented.
/// </para>
/// <para>
/// The fakes implement <see cref="IInboxStore"/>, <see cref="IClaimableInboxStore"/> and
/// <see cref="ILeasedInboxStore"/> DIRECTLY,
/// inheriting no first-party base, so the arms bind the interfaces' own requirement rather than
/// re-testing an inherited convenience. Their clock is driven, never waited on, so nothing here is a
/// function of machine load.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxStoreLeaseArmsBindShould
{
	#region Correct store

	/// <summary>
	/// LIVENESS: all three arms must PASS against a store with correct lease semantics.
	/// </summary>
	/// <remarks>
	/// Without this cell the arms could assert something no store can satisfy, and a permanently red arm
	/// teaches a reader to ignore it.
	/// </remarks>
	[Fact]
	public async Task PassEveryCellAgainstAStoreWithCorrectLeaseSemantics()
	{
		var probe = new ArmProbe(FakeMode.Correct);

		await probe.RunExpiredLeaseArmAsync().ConfigureAwait(false);
		await probe.RunLiveLeaseArmAsync().ConfigureAwait(false);
		await probe.RunLeaselessClaimArmAsync().ConfigureAwait(false);
	}

	#endregion Correct store

	#region Tightened until it refuses

	/// <summary>
	/// DETECTION: the liveness arm must FAIL against a store that never reclaims an expired lease, and must
	/// say the message is stranded rather than merely that a call returned false.
	/// </summary>
	[Fact]
	public async Task DetectAStoreThatNeverReclaimsAnExpiredLease()
	{
		var probe = new ArmProbe(FakeMode.NeverReclaims);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.RunExpiredLeaseArmAsync().ConfigureAwait(false)).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"stuck forever",
			Case.Sensitive,
			"the failure must name the CONSEQUENCE. 'A claim was refused' reads like a safety property "
			+ "working correctly, which is exactly how this defect survives review");
	}

	/// <summary>
	/// ANTI-OVERREACH: the two safety arms must PASS against that same store.
	/// </summary>
	/// <remarks>
	/// This is the cell that proves the liveness arm is load-bearing rather than redundant. A store that
	/// refuses every reclaim is perfectly safe by both safety measures -- it hands nothing to anyone twice
	/// -- so if the safety arms failed here too, the liveness arm would be detecting something they already
	/// caught and the contract would not need it. They pass, so it is not redundant: without it, this
	/// store's defect has nothing in the kit that can see it.
	/// </remarks>
	[Fact]
	public async Task PassBothSafetyCellsAgainstAStoreThatNeverReclaims()
	{
		var probe = new ArmProbe(FakeMode.NeverReclaims);

		await probe.RunLiveLeaseArmAsync().ConfigureAwait(false);
		await probe.RunLeaselessClaimArmAsync().ConfigureAwait(false);
	}

	#endregion Tightened until it refuses

	#region Loosened until it grants

	/// <summary>
	/// DETECTION: the live-lease arm must FAIL against a store that reclaims a lease inside its window.
	/// </summary>
	[Fact]
	public async Task DetectAStoreThatReclaimsALiveLease()
	{
		var probe = new ArmProbe(FakeMode.AlwaysReclaims);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.RunLiveLeaseArmAsync().ConfigureAwait(false)).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"still running",
			Case.Sensitive,
			"the failure must say the first holder's handler is still executing -- that is what makes this "
			+ "a duplicate side effect rather than a bookkeeping discrepancy");
	}

	/// <summary>
	/// DETECTION: the lease-less arm must FAIL against a store that reclaims a claim carrying no expiry.
	/// </summary>
	/// <remarks>
	/// The cell that was broken in shipped stores by two unrelated mechanisms -- an aggregation comparison
	/// that sorted a null below every date, and a dictionary miss read as an expiry -- so it is pinned on
	/// the DIAGNOSIS, not merely on the failure. Both stores were correct about expired leases and correct
	/// about live ones; only the absent case was wrong.
	/// </remarks>
	[Fact]
	public async Task DetectAStoreThatReclaimsALeaselessClaim()
	{
		var probe = new ArmProbe(FakeMode.AlwaysReclaims);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.RunLeaselessClaimArmAsync().ConfigureAwait(false)).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"Absent is not expired",
			Case.Sensitive,
			"the failure must name the confusion that causes it. A store author reading only 'a claim was "
			+ "granted twice' looks for a race, and there is no race here to find");
	}

	/// <summary>
	/// ANTI-OVERREACH: the liveness arm must PASS against that same store.
	/// </summary>
	/// <remarks>
	/// The mirror of the safety-cells cell above, and it earns its place for the same reason: a store that
	/// grants every reclaim is perfectly live, so the liveness arm cannot be what catches these defects.
	/// </remarks>
	[Fact]
	public async Task PassTheLivenessCellAgainstAStoreThatAlwaysReclaims()
	{
		var probe = new ArmProbe(FakeMode.AlwaysReclaims);

		await probe.RunExpiredLeaseArmAsync().ConfigureAwait(false);
	}

	#endregion Loosened until it grants

	#region Declares no lease protocol

	/// <summary>
	/// The arms must not RUN against a store that declares no lease surface — the protocol is optional.
	/// </summary>
	/// <remarks>
	/// Non-vacuous by construction rather than by assertion: every member of the fake throws, so an arm
	/// that got as far as touching the store fails here. Three quiet returns are the only way this passes.
	/// </remarks>
	[Fact]
	public async Task RunNoArmAgainstAStoreThatDeclaresNoLeaseProtocol()
	{
		var probe = new ArmProbe(new UnleasedInboxStore());

		await probe.RunExpiredLeaseArmAsync().ConfigureAwait(false);
		await probe.RunLiveLeaseArmAsync().ConfigureAwait(false);
		await probe.RunLeaselessClaimArmAsync().ConfigureAwait(false);

		// The arms not running is only half of it. A runner reports three passes either way, so the run
		// has to say somewhere that nothing was verified -- otherwise "did not run" and "passed" remain
		// the same observation, which is the defect this whole file exists to remove.
		var unverified = ConformanceArmLedger.Skipped.Select(static s => s.Arm).ToList();

		unverified.ShouldContain(nameof(InboxStoreConformanceTestKit.ExpiredLease_MustBeReclaimableByAnotherProcessor));
		unverified.ShouldContain(nameof(InboxStoreConformanceTestKit.LiveLease_MustNotBeReclaimableByAnotherProcessor));
		unverified.ShouldContain(nameof(InboxStoreConformanceTestKit.LeaselessClaim_MustNotBeReclaimableByTheLeasePath));
	}

	/// <summary>
	/// DETECTION: a store that ADVERTISES the lease capability while declaring no lease surface must go
	/// RED on every arm, rather than certifying off three arms that returned.
	/// </summary>
	/// <remarks>
	/// The cell that separates the two properties the previous test bundles. Not running the arms is not
	/// the same as not certifying as leased: a host does not read this kit, it reads the capability, and
	/// a store answering yes there is admitted by a startup guard that requires leasing and then has no
	/// lease surface to call. Without this cell that store passes the lease arms exactly as a correct one
	/// does, which is the shape the exception-catch discovery it replaced had before it.
	/// </remarks>
	[Fact]
	public async Task DetectAStoreThatAdvertisesALeaseCapabilityItDoesNotDeclare()
	{
		foreach (var arm in new Func<ArmProbe, Task>[]
		{
			static p => p.RunExpiredLeaseArmAsync(),
			static p => p.RunLiveLeaseArmAsync(),
			static p => p.RunLeaselessClaimArmAsync(),
		})
		{
			var probe = new ArmProbe(new AdvertisingUnleasedInboxStore());

			var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
				async () => await arm(probe).ConfigureAwait(false)).ConfigureAwait(false);

			thrown.Message.ShouldContain(
				"advertises a protocol it cannot execute",
				Case.Sensitive,
				"the failure must name the claim, not the absence. 'This store has no lease surface' is "
				+ "true of every store that declines the protocol legitimately");
		}
	}

	#endregion Declares no lease protocol

	#region Harness

	/// <summary>The single decision each fake varies: what makes a Processing entry reclaimable.</summary>
	private enum FakeMode
	{
		/// <summary>A real expiry, actually in the past. The conformant shape.</summary>
		Correct,

		/// <summary>Nothing is ever reclaimable. A predicate tightened until it admits nothing.</summary>
		NeverReclaims,

		/// <summary>Any Processing entry is reclaimable, expiry present or not.</summary>
		AlwaysReclaims,
	}

	/// <summary>
	/// Drives the real kit arms against a supplied fake. Subclassing is the only way in: calling the arms
	/// THROUGH the kit is the point -- a reimplemented copy would prove things about the copy.
	/// </summary>
	private sealed class ArmProbe : InboxStoreConformanceTestKit
	{
		private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
		private readonly IInboxStore _store;

		public ArmProbe(FakeMode mode) => _store = new FakeLeaseInboxStore(mode, _clock);

		/// <summary>Drives the same arms against a store that declares no lease surface at all.</summary>
		public ArmProbe(IInboxStore store) => _store = store;

		protected override IInboxStore CreateStore() => _store;

		/// <summary>
		/// Drives the clock instead of waiting, so every cell here is decided rather than timed.
		/// </summary>
		protected override Task ExpireLeaseAsync(TimeSpan leaseDuration, CancellationToken cancellationToken)
		{
			_clock.Advance(leaseDuration + TimeSpan.FromMilliseconds(1));

			return Task.CompletedTask;
		}

		/// <summary>
		/// Shortened because the only cell that reaches this deadline is the one asserting a store never
		/// reclaims, and against a fake that answer is already final. The production default is sized to
		/// absorb a real server clock's skew, which no fake has.
		/// </summary>
		protected override TimeSpan LeaseReclaimDeadline => TimeSpan.FromSeconds(2);

		public Task RunExpiredLeaseArmAsync() => ExpiredLease_MustBeReclaimableByAnotherProcessor();

		public Task RunLiveLeaseArmAsync() => LiveLease_MustNotBeReclaimableByAnotherProcessor();

		public Task RunLeaselessClaimArmAsync() => LeaselessClaim_MustNotBeReclaimableByTheLeasePath();
	}

	/// <summary>
	/// A minimal inbox store whose reclaim decision is fixed by construction.
	/// </summary>
	/// <remarks>
	/// Only the members the lease arms actually call are implemented. The rest refuse loudly rather than
	/// returning a plausible value: a fake that answers a question it was never designed to answer is how a
	/// lock comes to prove something about the fake instead of about the contract.
	/// </remarks>
	private sealed class FakeLeaseInboxStore(FakeMode mode, TimeProvider clock)
		: IInboxStore, IClaimableInboxStore, ILeasedInboxStore
	{
		private readonly ConcurrentDictionary<string, InboxStatus> _state = new(StringComparer.Ordinal);

		/// <summary>
		/// Expiries live HERE and not on the entry, so a claim taken through the lease-less overload leaves
		/// no expiry at all -- which is the state the third arm exists to exercise.
		/// </summary>
		private readonly ConcurrentDictionary<string, long> _leases = new(StringComparer.Ordinal);

		private readonly Lock _gate = new();

		/// <summary>
		/// The lease-less claim: first writer wins, and it records NO expiry. Identical in all three modes,
		/// mirroring a real store where this path is not the one carrying the defect.
		/// </summary>
		public ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
		{
			lock (_gate)
			{
				return ValueTask.FromResult(_state.TryAdd(Key(messageId, handlerType), InboxStatus.Processing));
			}
		}

		/// <summary>
		/// THE ONE EXPRESSION UNDER EXPERIMENT: what makes an existing Processing entry reclaimable.
		/// </summary>
		public ValueTask<LeaseToken?> TryAcquireLeaseAsync(
			string messageId,
			string handlerType,
			TimeSpan leaseDuration,
			CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

			var key = Key(messageId, handlerType);
			var nowMs = clock.GetUtcNow().ToUnixTimeMilliseconds();

			lock (_gate)
			{
				var exists = _state.TryGetValue(key, out var status);

				var claimable = !exists
					|| status is InboxStatus.Received or InboxStatus.Failed
					|| (status == InboxStatus.Processing && Reclaimable(key, nowMs));

				if (!claimable)
				{
					return ValueTask.FromResult<LeaseToken?>(null);
				}

				_state[key] = InboxStatus.Processing;
				_leases[key] = nowMs + (long)leaseDuration.TotalMilliseconds;

				return ValueTask.FromResult<LeaseToken?>(new LeaseToken(nowMs.ToString(System.Globalization.CultureInfo.InvariantCulture)));
			}
		}

		private bool Reclaimable(string key, long nowMs) => mode switch
		{
			// Requires a real expiry to be PRESENT before comparing it. The absence of one is "no expiry",
			// not "expired infinitely long ago".
			FakeMode.Correct => _leases.TryGetValue(key, out var expiry) && expiry < nowMs,

			// A predicate tightened until it admits nothing.
			FakeMode.NeverReclaims => false,

			// Any Processing entry, live lease or none at all.
			_ => true,
		};

		public ValueTask<bool> TryMarkAsProcessedAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(_state.TryAdd(Key(messageId, handlerType), InboxStatus.Processed));

		public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			ValueTask.FromResult(
				_state.TryGetValue(Key(messageId, handlerType), out var status) && status == InboxStatus.Processed);

		public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
		{
			_state[Key(messageId, handlerType)] = InboxStatus.Processed;

			return ValueTask.CompletedTask;
		}

		public ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
		{
			var key = Key(messageId, handlerType);
			_ = _state.TryRemove(key, out _);
			_ = _leases.TryRemove(key, out _);

			return ValueTask.CompletedTask;
		}

		public ValueTask<InboxEntry> CreateEntryAsync(
			string messageId,
			string handlerType,
			string messageType,
			byte[] payload,
			IDictionary<string, object> metadata,
			CancellationToken cancellationToken) => throw NotReached(nameof(CreateEntryAsync));

		public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			throw NotReached(nameof(GetEntryAsync));

		public ValueTask MarkFailedAsync(
			string messageId,
			string handlerType,
			string errorMessage,
			CancellationToken cancellationToken) => throw NotReached(nameof(MarkFailedAsync));

		public ValueTask<bool> CompleteAsync(
			string messageId,
			string handlerType,
			LeaseToken lease,
			CancellationToken cancellationToken) => throw NotReached(nameof(CompleteAsync));

		public ValueTask<bool> FailAsync(
			string messageId,
			string handlerType,
			LeaseToken lease,
			string errorMessage,
			CancellationToken cancellationToken) => throw NotReached(nameof(FailAsync));

		private static string Key(string messageId, string handlerType) => $"{messageId}|{handlerType}";

		private static NotSupportedException NotReached(string member) =>
			new($"{member} is outside the lease arms' reach. This fake exists to fix ONE decision -- what "
				+ "makes a Processing entry reclaimable -- and answering an unrelated call would let a "
				+ "future arm pass on the fake's behaviour rather than the contract's. If an arm now needs "
				+ "this member, implement it deliberately.");
	}


	/// <summary>
	/// A store with no lease surface whatsoever, and no member that answers anything.
	/// </summary>
	/// <remarks>
	/// Every member throws. The arms are supposed to return before touching this store at all, so a fake
	/// that answered plausibly would let a future arm reach past the capability check and still pass.
	/// </remarks>
	private class UnleasedInboxStore : IInboxStore, IClaimableInboxStore
	{
		public ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			throw NotReached(nameof(TryClaimAsync));

		public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			throw NotReached(nameof(TryMarkAsProcessedAsync));

		public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			throw NotReached(nameof(IsProcessedAsync));

		public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			throw NotReached(nameof(MarkProcessedAsync));

		public ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			throw NotReached(nameof(ReleaseAsync));

		public ValueTask<InboxEntry> CreateEntryAsync(
			string messageId,
			string handlerType,
			string messageType,
			byte[] payload,
			IDictionary<string, object> metadata,
			CancellationToken cancellationToken) => throw NotReached(nameof(CreateEntryAsync));

		public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			throw NotReached(nameof(GetEntryAsync));

		public ValueTask MarkFailedAsync(
			string messageId,
			string handlerType,
			string errorMessage,
			CancellationToken cancellationToken) => throw NotReached(nameof(MarkFailedAsync));

		private static NotSupportedException NotReached(string member) =>
			new($"{member} is outside the reach of a store that declares no lease protocol. The lease arms "
				+ "must return before touching it.");
	}

	/// <summary>
	/// The same store, now REPORTING the lease capability it does not implement.
	/// </summary>
	/// <remarks>
	/// The one difference from its base is the answer a host actually reads, which is what makes this the
	/// mutation the detection cell binds.
	/// </remarks>
	private sealed class AdvertisingUnleasedInboxStore : UnleasedInboxStore, IInboxStoreCapabilities
	{
		public bool SupportsClaim => true;

		public bool SupportsLeasedClaim => true;

		public bool SupportsProcessingTracking => false;

		public bool SupportsTransactional => false;

public bool SupportsScopedTransactional => false;

		public bool SupportsBackoffScheduling => false;
	}

	#endregion Harness
}
