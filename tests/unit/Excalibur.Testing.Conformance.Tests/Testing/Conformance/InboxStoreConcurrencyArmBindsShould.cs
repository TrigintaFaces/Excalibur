// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Proves that the concurrency arm in <see cref="InboxStoreConformanceTestKit"/> actually BINDS -- that it
/// goes RED against a store carrying the defect it names, RED against a store that evades the defect by
/// refusing everyone, and GREEN against one that is genuinely correct.
/// </summary>
/// <remarks>
/// <para>
/// A concurrency assertion is the easiest kind to write and the hardest kind to trust. It can pass because
/// the store is correct, or because the race never happened; the two are indistinguishable in a result
/// line. So the arm is pinned here against fakes whose one varied decision is known in advance, and the
/// broken fake is broken DETERMINISTICALLY -- its unguarded write is held until the write it is going to
/// destroy has landed. The test that proves a race detector works must not itself be a race.
/// </para>
/// <para>
/// The verdict matrix each test below pins, one cell at a time:
/// </para>
/// <code>
///                        Race safety   Uncontested liveness
///   Atomic               GREEN         GREEN
///   UnguardedAcrossPaths RED           green
///   RefusesEveryClaim    green         RED
/// </code>
/// <para>
/// The lower-case cells are the load-bearing ones. A store whose acquisition paths destroy each other's
/// records still grants an uncontested acquisition, so the liveness half alone would certify it. A store
/// that refuses every caller can never elect two winners, so the safety half alone would certify THAT.
/// Neither half detects the other's defect, which is why the arm asserts both and why both are pinned here.
/// </para>
/// <para>
/// The fakes implement <see cref="IInboxStore"/> and <see cref="IClaimableInboxStore"/> DIRECTLY, inheriting
/// no first-party base, so the arm binds the interfaces' own requirement rather than re-testing an inherited
/// convenience.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxStoreConcurrencyArmBindsShould
{
	#region Race safety

	/// <summary>
	/// DETECTION: the arm must FAIL against a store whose acquisition paths are not atomic against one
	/// another, and must say which operations were told they had both won.
	/// </summary>
	[Fact]
	public async Task DetectTwoWinnersWhenTheAcquisitionPathsAreNotAtomicAgainstEachOther()
	{
		var probe = new ArmProbe(FakeMode.UnguardedAcrossPaths);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.RunConcurrencyArmAsync().ConfigureAwait(false)).ConfigureAwait(false);

		// Everything below asserts the SHAPE of the diagnosis, never a specific instance of it. Which
		// callers win, how many win, and which pair of paths straddled each other are all decided by the
		// scheduler, and they genuinely differ run to run - the unguarded probe has been observed losing
		// to Claim/LeaseClaim on one run and MarkProcessed/LeaseClaim on the next. Pinning any of those
		// literals makes this cell fail for a reason that has nothing to do with the arm being wrong.
		thrown.Message.ShouldMatch(
			@"told [2-8] of 8 concurrent callers",
			"the arm must name HOW MANY callers were each told they hold the message - 'a concurrency "
			+ "problem' is not actionable. The count itself is scheduling-dependent, so only its presence "
			+ "and plausible range are pinned here.");

		var winners = thrown.Message
			.Split(" -> WON", StringSplitOptions.None)[..^1]
			.Select(segment => segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[^1])
			.ToArray();

		winners.Length.ShouldBeGreaterThanOrEqualTo(
			2,
			"the arm must name the operations that won. Which PAIR straddled each other is the whole of "
			+ "the diagnosis and cannot be recovered by re-running, because the next run interleaves "
			+ "differently - so a report naming fewer than both halves is unusable.");

		winners.ShouldBeSubsetOf(
			["Claim", "LeaseClaim", "MarkProcessed"],
			"every named winner must be one of the three acquisition paths the arm drives; anything else "
			+ "means the arm is reporting an operation it did not run.");
	}

	/// <summary>
	/// LIVENESS: the arm must PASS against a store whose acquisition paths share one atomic step.
	/// </summary>
	/// <remarks>
	/// Without this cell the arm could assert something no store can satisfy, and a permanently red arm
	/// teaches a reader to ignore it. It also pins the post-race half: a claim winner finalises its message
	/// and the store must then report it processed.
	/// </remarks>
	[Fact]
	public async Task PassAgainstAStoreWhoseAcquisitionPathsShareOneAtomicStep()
	{
		var probe = new ArmProbe(FakeMode.Atomic);

		await probe.RunConcurrencyArmAsync().ConfigureAwait(false);
	}

	#endregion Race safety

	#region Uncontested liveness

	/// <summary>
	/// ANTI-OVERREACH: the arm must FAIL against a store that refuses every claim, and must attribute the
	/// failure to refusal rather than to contention.
	/// </summary>
	/// <remarks>
	/// A store that grants nothing satisfies "at most one winner" perfectly, because a caller that never
	/// wins can never win twice. Such a store stalls every message on its first delivery, and the arm must
	/// catch it -- from an uncontested acquisition, so the verdict does not depend on a schedule.
	/// </remarks>
	[Fact]
	public async Task DetectAStoreThatRefusesEveryClaimBeforeAnyRaceIsRun()
	{
		var probe = new ArmProbe(FakeMode.RefusesEveryClaim);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.RunConcurrencyArmAsync().ConfigureAwait(false)).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"with no competing caller",
			Case.Sensitive,
			"the failure must say the refusal happened uncontested, so a refusing store is not misdiagnosed "
			+ "as a concurrency fault and sent to the wrong fix");

		thrown.Message.ShouldNotContain(
			"concurrent callers that each of them holds",
			Case.Sensitive,
			"a refusing store must never reach the race assertions - it would satisfy them vacuously");
	}

	#endregion Uncontested liveness

	#region Harness

	/// <summary>The single decision each fake varies.</summary>
	private enum FakeMode
	{
		/// <summary>Every acquisition path decides and writes in one atomic step. The conformant shape.</summary>
		Atomic,

		/// <summary>
		/// The lease claim reads, decides, then writes unconditionally, and the mark path does not
		/// participate in whatever guards that sequence. Its write therefore lands on top of a marker
		/// another caller was just promised.
		/// </summary>
		UnguardedAcrossPaths,

		/// <summary>Atomic, but no claim is ever granted to anyone.</summary>
		RefusesEveryClaim,
	}

	/// <summary>
	/// Drives the real kit arm against a supplied fake. Subclassing is the only way in: calling the arm
	/// THROUGH the kit is the point -- a reimplemented copy would prove things about the copy.
	/// </summary>
	private sealed class ArmProbe(FakeMode mode) : InboxStoreConformanceTestKit
	{
		// ONE backing set shared by every store this probe hands out, so a kit that called CreateStore more
		// than once could not satisfy the arm by instance separation.
		private readonly ConcurrentDictionary<string, InboxStatus> _state = new(StringComparer.Ordinal);
		private readonly ConcurrentDictionary<string, ManualResetEventSlim> _markLanded = new(StringComparer.Ordinal);

		protected override IInboxStore CreateStore() => new FakeInboxStore(mode, _state, _markLanded);

		public Task RunConcurrencyArmAsync() =>
			ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker();
	}

	/// <summary>
	/// A minimal inbox store whose atomicity decision is fixed by construction.
	/// </summary>
	/// <remarks>
	/// Only the members the concurrency arm actually calls are implemented. The rest refuse loudly rather
	/// than returning a plausible value: a fake that answers a question it was never designed to answer is
	/// how a lock comes to prove something about the fake instead of about the contract.
	/// </remarks>
	private sealed class FakeInboxStore(
		FakeMode mode,
		ConcurrentDictionary<string, InboxStatus> state,
		ConcurrentDictionary<string, ManualResetEventSlim> markLanded) : IInboxStore, IClaimableInboxStore, ILeasedInboxStore
	{
		/// <summary>
		/// Bounds the deterministic handshake below, so a mode change that stopped producing a marker
		/// surfaces as a slow test rather than a hung one.
		/// </summary>
		/// <remarks>
		/// Paid in full exactly once per run, by the kit's uncontested check for lease support -- the one
		/// lease claim with no mark racing it, whose signal therefore never arrives. Inside a race the wait
		/// resolves in microseconds, because the mark it waits for was released by the same barrier. The
		/// bound is four orders of magnitude above that, so a loaded machine cannot turn this assertion
		/// back into the probability it exists to replace.
		/// </remarks>
		private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(2);

		public ValueTask<bool> TryMarkAsProcessedAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken)
		{
			var key = Key(messageId, handlerType);
			var won = state.TryAdd(key, InboxStatus.Processed);

			if (won)
			{
				// Releases the unguarded claim below. Set only on a genuine first write, so the claim waits
				// for a marker that actually exists rather than for the mere attempt to write one.
				Signal(key).Set();
			}

			return ValueTask.FromResult(won);
		}

		/// <summary>
		/// The two-argument claim is atomic in every mode, mirroring a real store where one acquisition
		/// path is guarded and another is not. A fake that broke both paths at once would not distinguish
		/// "these two operations straddle each other" from "this store has no concurrency control at all."
		/// </summary>
		public ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			ValueTask.FromResult(
				mode is not FakeMode.RefusesEveryClaim
				&& state.TryAdd(Key(messageId, handlerType), InboxStatus.Processing));

		/// <summary>
		/// THE ONE EXPRESSION UNDER EXPERIMENT. Under <see cref="FakeMode.UnguardedAcrossPaths"/> the read,
		/// the decision, and the write are three separate steps, and nothing the mark path respects sits
		/// between them -- so the write lands on top of a marker a mark caller was just promised, and both
		/// callers believe they hold the message.
		/// </summary>
		public ValueTask<LeaseToken?> TryAcquireLeaseAsync(
			string messageId,
			string handlerType,
			TimeSpan leaseDuration,
			CancellationToken cancellationToken)
		{
			if (mode is FakeMode.RefusesEveryClaim)
			{
				return ValueTask.FromResult<LeaseToken?>(null);
			}

			var key = Key(messageId, handlerType);

			if (mode is not FakeMode.UnguardedAcrossPaths)
			{
				return ValueTask.FromResult(state.TryAdd(key, InboxStatus.Processing) ? (LeaseToken?)new LeaseToken(key) : null);
			}

			// READ, then DECIDE.
			if (state.ContainsKey(key))
			{
				return ValueTask.FromResult<LeaseToken?>(null);
			}

			// The window is held open until the write it is going to destroy has landed. A real store's
			// window is nanoseconds wide and opens only sometimes; that is exactly what makes the defect
			// hard to catch, and exactly what must NOT be reproduced in the test proving the arm binds.
			// Widened here on purpose so this test states a fact rather than a probability.
			_ = Signal(key).Wait(HandshakeTimeout);

			// WRITE, unconditionally. Whatever the mark path recorded in the meantime is now gone.
			state[key] = InboxStatus.Processing;
			return ValueTask.FromResult<LeaseToken?>(new LeaseToken(key));
		}

		public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
		{
			var key = Key(messageId, handlerType);

			if (!state.ContainsKey(key))
			{
				throw new InvalidOperationException(
					$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
			}

			state[key] = InboxStatus.Processed;
			return ValueTask.CompletedTask;
		}

		public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			ValueTask.FromResult(
				state.TryGetValue(Key(messageId, handlerType), out var status)
				&& status == InboxStatus.Processed);

		public ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
		{
			_ = state.TryRemove(Key(messageId, handlerType), out _);
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
			new($"{member} is outside the concurrency arm's reach. This fake exists to fix ONE decision -- "
				+ "whether the acquisition paths are atomic against one another -- and answering an "
				+ "unrelated call would let a future arm pass on the fake's behaviour rather than the "
				+ "contract's. If an arm now needs this member, implement it deliberately.");

		private ManualResetEventSlim Signal(string key) =>
			markLanded.GetOrAdd(key, static _ => new ManualResetEventSlim(initialState: false));
	}

	#endregion Harness
}
