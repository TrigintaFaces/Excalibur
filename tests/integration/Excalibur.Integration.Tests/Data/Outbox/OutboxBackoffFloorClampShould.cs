// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using Excalibur.Dispatch;

using Shouldly;

using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure locks on the property that a configured failure-backoff floor F is honoured on the
/// path the outbox processor actually PREFERS, not only on the one the guarantee contract describes.
/// </summary>
/// <remarks>
/// <para>
/// When a store advertises <see cref="IBackoffSchedulableOutboxStore"/> the processor stops calling
/// <c>MarkFailedAsync</c> and calls <c>MarkFailedWithBackoffAsync</c> instead, handing it a next-attempt
/// instant it computed itself. That instant used to be bound verbatim. The framework backoff calculator
/// yields roughly a second at the first attempt, so a consumer who configured a floor of several minutes got
/// a retry a second later: the floor was accepted and ignored, and the capability meant to REFINE the
/// schedule instead weakened the guarantee below what the same failure gets without it.
/// </para>
/// <para>
/// The two arms below are the two halves that make the clamp real rather than merely present. Safety: a
/// computed delay SHORTER than F cannot pull the retry inside F. Liveness: a computed delay LONGER than F is
/// still honoured, so the clamp did not collapse every schedule onto the floor and flatten the backoff curve
/// it exists to preserve. A clamp asserted only on its safety half is satisfied by pinning every retry to F.
/// </para>
/// <para>
/// Asserted through the CLAIM, not through the stored column: the property a consumer cares about is that
/// the drain does not hand the message back early, and the claim predicate is what decides that.
/// </para>
/// </remarks>
public abstract class OutboxBackoffFloorClampShould
{
	/// <summary>The configured floor F. Long enough that an unclamped ~1s backoff is unambiguously inside it.</summary>
	protected const int FloorSeconds = 30;

	/// <summary>
	/// How long the liveness arm will keep asking for a message whose floor has elapsed before it calls the
	/// message lost.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Generous on purpose, and not a tolerance on the store's behaviour. The floor is measured on the
	/// STORE's clock; any wait a test performs is measured on the TEST HOST's. Those are two different
	/// clocks, and on a loaded machine a containerised store's clock does not advance in step with the
	/// host's -- it stalls and then catches up. Measured on the machine this suite runs on, sampling a
	/// container's clock across fifty host-side 2.5 second waits: six of the fifty advanced the container
	/// clock by only 345 to 541 ms. A store whose clock moved 345 ms has not seen a two second floor
	/// elapse, however long the host waited.
	/// </para>
	/// <para>
	/// A single sample taken after a fixed sleep therefore asserts that the STORE has seen the floor elapse
	/// when only the HOST has, and reports a store that is behaving correctly -- deferring a retry because
	/// its own clock says the floor has not passed -- as a message that never came back. Deferring is the
	/// safe direction for an at-least-once outbox, so the store is right and the sample is wrong.
	/// </para>
	/// <para>
	/// Polling asserts the same property the single sample was written to assert -- the message DOES come
	/// back -- without assuming the two clocks agree. A stalled or rewound store clock defers the
	/// observation instead of failing it, and a store that genuinely strands the message still fails when
	/// this window expires, because a clock that stalls always catches up.
	/// </para>
	/// </remarks>
	private static readonly TimeSpan ReclaimWindow = TimeSpan.FromSeconds(30);

	/// <summary>How often the liveness arm re-asks the store while the window is open.</summary>
	private static readonly TimeSpan ReclaimPollInterval = TimeSpan.FromMilliseconds(250);

	/// <summary>Builds a store over the live container with the given failure-backoff floor.</summary>
	/// <param name="floorSeconds">The floor F to configure.</param>
	/// <returns>A store bound to the fixture database.</returns>
	protected abstract Task<IOutboxStore> CreateStoreAsync(int floorSeconds);

	/// <summary>Empties the outbox table between arms.</summary>
	/// <returns>A task that completes when the table is empty.</returns>
	protected abstract Task CleanupAsync();

	/// <summary>
	/// SAFETY. A computed backoff shorter than the configured floor must not make the message re-claimable
	/// before the floor elapses.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task NotReclaimBeforeTheFloor_WhenTheComputedBackoffIsShorterThanIt()
	{
		var ct = TestContext.Current.CancellationToken;
		await CleanupAsync().ConfigureAwait(false);

		var store = await CreateStoreAsync(FloorSeconds).ConfigureAwait(false);
		var schedulable = store.GetService(typeof(IBackoffSchedulableOutboxStore)) as IBackoffSchedulableOutboxStore;
		schedulable.ShouldNotBeNull(
			"this provider must advertise the backoff capability, otherwise the processor would never take " +
			"the path under test and this lock would be vacuous.");

		var message = NewMessage();
		await store.StageMessageAsync(message, ct).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();

		// Exactly what the processor computes at the first attempt: about one second out, from its own clock.
		await schedulable.MarkFailedWithBackoffAsync(
			message.Id, "boom", 1, DateTimeOffset.UtcNow.AddSeconds(1), ct).ConfigureAwait(false);

		// Well past the computed delay, nowhere near the floor.
		await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);

		var claimed = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		claimed.ShouldNotContain(
			m => m.Id == message.Id,
			$"a configured floor of {FloorSeconds}s must hold on the backoff path too. The computed delay was " +
			"about a second, so binding it verbatim makes the message re-claimable here — and this is the " +
			"path the processor PREFERS whenever the store advertises the capability, so the floor a " +
			"consumer configured is the one production ignores.");
	}

	/// <summary>
	/// LIVENESS. A computed backoff LONGER than the floor is still honoured, and the message does come back.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Without this arm the clamp above is satisfied by discarding the caller schedule entirely and pinning
	/// every retry to exactly F, which would flatten the exponential curve the capability exists to apply.
	/// The second half of the arm then waits the short floor out, so the clamp is shown to DEFER the retry
	/// rather than cancel it.
	/// </remarks>
	[Fact]
	public async Task StillHonourALongerComputedBackoff_AndReturnTheMessageOnceItElapses()
	{
		var ct = TestContext.Current.CancellationToken;
		await CleanupAsync().ConfigureAwait(false);

		// A deliberately SHORT floor, so the caller schedule is the binding constraint rather than F.
		const int ShortFloorSeconds = 2;
		var store = await CreateStoreAsync(ShortFloorSeconds).ConfigureAwait(false);
		var schedulable = (IBackoffSchedulableOutboxStore)store.GetService(typeof(IBackoffSchedulableOutboxStore))!;

		var deferred = NewMessage();
		await store.StageMessageAsync(deferred, ct).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();

		// A computed delay well BEYOND the floor -- a later attempt on the exponential curve.
		await schedulable.MarkFailedWithBackoffAsync(
			deferred.Id, "boom", 5, DateTimeOffset.UtcNow.AddSeconds(30), ct).ConfigureAwait(false);

		await Task.Delay(TimeSpan.FromSeconds(ShortFloorSeconds + 2), ct).ConfigureAwait(false);

		var afterFloor = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		afterFloor.ShouldNotContain(
			m => m.Id == deferred.Id,
			"the floor is a LOWER bound, not the schedule. A computed backoff beyond F must still be " +
			"honoured, or the clamp has flattened the exponential curve onto a constant F.");

		// And a message whose schedule HAS elapsed is genuinely handed back, so neither bound strands it.
		var due = NewMessage();
		await store.StageMessageAsync(due, ct).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		await schedulable.MarkFailedWithBackoffAsync(
			due.Id, "boom", 1, DateTimeOffset.UtcNow.AddSeconds(1), ct).ConfigureAwait(false);

		await Task.Delay(TimeSpan.FromSeconds(ShortFloorSeconds + 2), ct).ConfigureAwait(false);

		// Asked repeatedly rather than sampled once: see ReclaimWindow for why one sample after a fixed
		// wait cannot tell a store that is withholding the message from a store whose own clock has not
		// yet reached the floor.
		var returned = await WaitHelpers.WaitUntilAsync(
			async () => (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false))
				.Any(m => m.Id == due.Id),
			ReclaimWindow,
			ReclaimPollInterval,
			ct).ConfigureAwait(false);

		returned.ShouldBeTrue(
			"once both the floor and the computed schedule have elapsed the message must be re-claimed. " +
			"A clamp that withheld it forever would satisfy the safety arm by dropping the message. This " +
			$"arm kept asking for {ReclaimWindow.TotalSeconds:0} seconds, which is far longer than any " +
			"clock stall observed on this host, so the message was never handed back.");
	}

	private static OutboundMessage NewMessage() =>
		new("Test.MessageType", "test-payload"u8.ToArray(), "test-queue") { Id = Guid.NewGuid().ToString() };
}
