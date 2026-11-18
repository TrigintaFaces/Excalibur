// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Watch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Watch;

/// <summary>
/// Binds the promise the watcher's constructor makes about its injected time provider: that advancing
/// that clock drives the next poll, instead of a test waiting out a real interval.
/// </summary>
/// <remarks>
/// <para>
/// The watcher stamped every emitted event from the injected provider while waiting between polls on the
/// ambient clock, so the promise was kept for the timestamps and silently broken for the cadence — which
/// is worse than never offering it, because a consumer has no way to see which is which. Every other arm
/// in this folder constructs the watcher with three arguments, so they all run on the system clock and
/// none of them can observe the difference.
/// </para>
/// <para>
/// The poll interval is <b>one hour</b>, and that choice is what makes this arm honest rather than lucky.
/// A watcher waiting on the ambient clock cannot reach its second poll inside a unit test at all, so the
/// arm reddens by construction; a watcher waiting on the injected clock reaches it the moment the fake is
/// advanced, so the arm is fast by construction. Nothing here waits on real time, and no real-time
/// threshold is asserted. An hour is also the largest interval the options validator accepts, so this is
/// a configuration a consumer can really reach rather than a value invented for the test.
/// </para>
/// <para>
/// Synchronisation is the enumerator's own pending read -- a real synchronisation point the poll loop
/// completes when it writes. The clock is advanced in a bounded pump because a fake timer that has not
/// been registered yet cannot be fired: the loop writes its event before it registers the next wait, so
/// the test can resume first. Each pump step advances a whole interval, so an advance that lands before
/// registration only moves the deadline and the following step still fires it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class DefaultLeaderElectionWatcherHonoursTimeProviderShould
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

	/// <summary>
	/// Cooperative yields spent giving the poll loop a chance to misbehave while the clock is frozen.
	/// An attempt count is sound HERE and only here: this bound can only cause the arm to notice a
	/// forbidden extra poll less often, never to report one that did not happen.
	/// </summary>
	private const int FrozenClockYields = 2_000;

	/// <summary>
	/// SAFETY and LIVENESS in one arm: the loop does not poll before its interval has elapsed on the
	/// injected clock (so it is clock-gated, not spinning), and advancing that clock by the interval is
	/// what produces the next poll.
	/// </summary>
	[Fact]
	public async Task Drive_its_next_poll_from_the_injected_provider()
	{
		// Arrange
		var clock = new FakeTimeProvider();
		var leader = "node-1";
		var election = A.Fake<ILeaderElection>();
		A.CallTo(() => election.CurrentLeaderId).ReturnsLazily(() => Volatile.Read(ref leader));

		var watcher = new DefaultLeaderElectionWatcher(
			election,
			Options.Create(new LeaderWatchOptions { PollInterval = PollInterval }),
			NullLogger<DefaultLeaderElectionWatcher>.Instance,
			clock);

		// The real-time ceiling. A correct watcher reaches its second poll in about a millisecond, so this
		// is never approached; it exists so a regression fails on the assertion below instead of wedging.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var events = watcher.WatchAsync(cts.Token).GetAsyncEnumerator();

		// Act -- the first poll reports initial state and needs no clock movement.
		(await events.MoveNextAsync()).ShouldBeTrue("the watcher must emit its initial state.");
		events.Current.NewLeader.ShouldBe("node-1");
		var initialStamp = events.Current.ChangedAt;

		// A leader change made visible between advances. Only a second poll can observe it.
		Volatile.Write(ref leader, "node-2");

		// AsTask, not the raw ValueTask: this is polled for completion and then awaited, and a ValueTask
		// may be consumed only once. The pending read is the same either way.
		var next = events.MoveNextAsync().AsTask();

		// Assert (SAFETY) -- frozen clock, no second poll. A loop that ignored its interval would emit here.
		for (var attempt = 0; attempt < FrozenClockYields && !next.IsCompleted; attempt++)
		{
			await Task.Yield();
		}

		next.IsCompleted.ShouldBeFalse(
			"the watcher polled again without its interval elapsing on the injected clock, so advancing " +
			"that clock is not what gates the loop.");

		// Assert (LIVENESS) -- advancing the injected clock is what produces the poll.
		//
		// Bounded by the real-time ceiling and NOT by an attempt count, deliberately. An attempt count
		// is the one mechanism in this arm that can produce a FALSE red: the loop's continuation is
		// scheduled on the thread pool, so a contended pool can outlast any fixed number of yields
		// while the code under test is perfectly correct. Measured: one such failure in 93 runs. The
		// ceiling cannot do that -- a starved pool makes this arm slower, never red -- and the mutant
		// still fails, because no amount of waiting moves a real one-hour delay.
		while (!next.IsCompleted && !cts.IsCancellationRequested)
		{
			clock.Advance(PollInterval);
			await Task.Yield();
		}

		// Cancellation retires the pending read, so the enumerator is safe to dispose either way. Read
		// the token rather than `next.IsCompleted`: on expiry the read completes by FAULTING, which
		// would otherwise look like the poll having arrived.
		cts.IsCancellationRequested.ShouldBeFalse(
			"advancing the injected clock by the poll interval did not reach the next poll, so the wait " +
			"between polls is not on the injected clock and a consumer cannot drive this loop under test.");

		(await next).ShouldBeTrue("the watcher completed its channel instead of emitting the change.");
		events.Current.PreviousLeader.ShouldBe("node-1");
		events.Current.NewLeader.ShouldBe("node-2");

		// The stamp moved WITH the fake clock, by at least the interval that was waited out.
		//
		// Deliberately a range and not `ShouldBe(clock.GetUtcNow())`: the pump above keeps advancing
		// until it OBSERVES completion, so by the time this runs the clock has usually moved past the
		// instant the event was stamped -- by a load-dependent number of intervals. Pinning the stamp to
		// the clock's final value is a race, and it is the one that actually flaked.
		events.Current.ChangedAt.ShouldBeGreaterThanOrEqualTo(
			initialStamp + PollInterval,
			"the change was stamped earlier than the interval the watcher waited out.");

		// And it is a FAKE instant, not a real one: the fake's epoch is 2000-01-01, decades behind any
		// real clock, so an ambient-clock read lands far above this ceiling and cannot pass by accident.
		events.Current.ChangedAt.ShouldBeLessThanOrEqualTo(
			clock.GetUtcNow(),
			"the stamp is ahead of the injected clock, so it did not come from that clock.");

		// The election really was consulted again -- the event is not a replay of the first poll.
		A.CallTo(() => election.CurrentLeaderId).MustHaveHappened(2, Times.OrMore);

		await cts.CancelAsync();
	}
}
