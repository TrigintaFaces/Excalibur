// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// Binds the renewal cadence to the injected <see cref="TimeProvider"/>: a candidate that is handed a
/// controllable clock must take its renewal ticks from that clock and no other.
/// </summary>
/// <remarks>
/// <para>
/// The class already accepts a <see cref="TimeProvider"/> and uses it for event timestamps. If the
/// renewal timer is built from the ambient system clock instead, the dependency is honoured for one of
/// its two uses of time: a caller supplying a controllable provider gets deterministic timestamps and a
/// renewal cadence it cannot observe or drive, which is the harder half to test and the half that
/// decides whether a free resource is ever picked up.
/// </para>
/// <para>
/// Both arms are needed. The safety arm alone -- "not leader before the clock is advanced" -- is
/// satisfied by a renewal that never fires at all. The liveness arm alone -- "leader eventually" -- is
/// satisfied by a wall-clock timer that would have fired regardless of the advance. Together they say
/// the advance, and only the advance, produced the acquisition.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryLeaderElectionRenewalClockShould
{
	private const string Resource = "renewal-clock-resource";

	private static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(5);

	[Fact]
	public async Task AcquireAFreedResourceOnARenewalTickDrivenByTheInjectedClock()
	{
		var sharedState = new InMemoryLeaderElectionSharedState();
		var clock = new FakeTimeProvider();

		await using var holder = Create("holder", sharedState, TimeProvider.System);
		await using var waiter = Create("waiter", sharedState, clock);

		await holder.StartAsync(TestContext.Current.CancellationToken);
		holder.IsLeader.ShouldBeTrue("the first candidate to start takes the free resource");

		await waiter.StartAsync(TestContext.Current.CancellationToken);
		waiter.IsLeader.ShouldBeFalse("the resource was already held when the second candidate started");

		// The resource is now free, and nothing but a renewal tick can hand it to the waiting candidate.
		await holder.StopAsync(TestContext.Current.CancellationToken);

		// Safety arm: no renewal has been driven, so the waiting candidate must still be empty-handed.
		// Give a wall-clock timer more than one renewal interval to betray itself here.
		await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
		waiter.IsLeader.ShouldBeFalse(
			"no renewal tick has been driven on the injected clock, so the candidate must not have "
			+ "acquired -- if it has, the renewal cadence is coming from the ambient system clock");

		// Liveness arm: advancing the injected clock past one renewal interval must produce the tick.
		clock.Advance(RenewInterval);

		await WaitUntilAsync(() => waiter.IsLeader);

		waiter.IsLeader.ShouldBeTrue(
			"advancing the injected clock by one renewal interval must fire the renewal callback, which "
			+ "acquires the freed resource");
	}

	private static InMemoryLeaderElection Create(
		string instanceId,
		InMemoryLeaderElectionSharedState sharedState,
		TimeProvider timeProvider)
	{
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = instanceId,
			RenewInterval = RenewInterval,

			// Health-based step-down would put a second reason in front of the acquisition and blunt what
			// this test measures; the subject here is only where the renewal tick comes from.
			StepDownWhenUnhealthy = false,
		});

		return new InMemoryLeaderElection(
			Resource,
			options,
			NullLogger<InMemoryLeaderElection>.Instance,
			sharedState,
			timeProvider);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		// The renewal callback starts the acquisition without awaiting it, so the observable effect can
		// land just after Advance returns.
		for (var attempt = 0; attempt < 200 && !condition(); attempt++)
		{
			await Task.Delay(10, TestContext.Current.CancellationToken);
		}
	}
}
