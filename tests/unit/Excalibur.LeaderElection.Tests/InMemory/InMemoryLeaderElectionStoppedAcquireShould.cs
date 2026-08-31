// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// Binds the lifecycle half of mutual exclusion: <b>a candidate holds the resource only while it is
/// running.</b> A candidate that has stopped must not be left holding it, whatever it was doing at the
/// moment it stopped.
/// </summary>
/// <remarks>
/// <para>
/// Every acquisition in this provider is a <c>TryAdd</c> into a process-local dictionary, reached from
/// two callers that can both be racing a shutdown: the start path, because a host may start and stop
/// concurrently, and the lease-renewal callback, because a callback already dispatched to the thread
/// pool keeps running after the timer is disarmed. An entry check cannot establish the invariant -- the
/// state can go stopped between the check and the add.
/// </para>
/// <para>
/// The interleaving, with the re-check absent: the renewal callback (or the start path) reads that it
/// is neither leader nor unhealthy and is about to acquire; the shutdown then runs to completion -- it
/// marks the candidate stopped, disarms the timer, finds nothing to release because the candidate did
/// not yet hold anything, and deregisters the candidate; the pending add then succeeds. The resource is
/// now held by a candidate that has stopped and deregistered. Disposal does not correct it: disposal
/// stops first, and a second stop returns immediately without releasing. This provider has no lease
/// expiry, so nothing ever reclaims the resource and every other candidate fails forever.
/// </para>
/// <para>
/// The add and the lifecycle read are adjacent with no interposition point, so the safety arms drive
/// the window by contention rather than by ordering, and report a rate. See the liveness arms -- a
/// guard tightened until it released every acquisition would satisfy the safety arms here and hand the
/// resource away from a perfectly healthy leader on its next renewal tick.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryLeaderElectionStoppedAcquireShould
{
	private const string Resource = "stopped-acquire-resource";
	private const string RacingCandidate = "racing-candidate";
	private const int RaceIterations = 200_000;
	private const int RenewalRaceIterations = 2_000;

	[Fact]
	public async Task NeverLeaveAStoppedCandidateHoldingTheResource_WhenStartRacesStop()
	{
		var violations = new List<int>();

		for (var iteration = 0; iteration < RaceIterations; iteration++)
		{
			if (await RunStartStopRaceAsync())
			{
				violations.Add(iteration);
			}
		}

		violations.ShouldBeEmpty(
			$"the resource was left held by a stopped candidate on {violations.Count} of {RaceIterations} "
			+ "iterations: the acquisition landed after the shutdown had already run its release, and this "
			+ "provider has no expiry, so no candidate can ever take the resource again");
	}

	[Fact]
	public async Task NeverLeaveAStoppedCandidateHoldingTheResource_WhenTheRenewalTickRacesStop()
	{
		var violations = new List<int>();

		for (var iteration = 0; iteration < RenewalRaceIterations; iteration++)
		{
			if (await RunRenewalStopRaceAsync())
			{
				violations.Add(iteration);
			}
		}

		violations.ShouldBeEmpty(
			$"the resource was left held by a stopped candidate on {violations.Count} of "
			+ $"{RenewalRaceIterations} iterations: a lease-renewal callback already dispatched to the "
			+ "thread pool acquired after the shutdown had run, and disarming a timer does not recall a "
			+ "callback already in flight");
	}

	// -------------------------------------------------------------------------------------------------
	// Liveness: the guard must refuse only an acquisition by a candidate that has stopped. A guard that
	// released unconditionally, or that read the lifecycle state wrongly, would satisfy every safety arm
	// above while leaving the resource permanently unheld -- the same outage from the other side.
	// -------------------------------------------------------------------------------------------------

	[Fact]
	public async Task StillAcquire_WhenACandidateSimplyStarts()
	{
		var state = new InMemoryLeaderElectionSharedState();
		await using var candidate = NewCandidate("A", state);

		await candidate.StartAsync(CancellationToken.None);

		candidate.IsLeader.ShouldBeTrue("a running candidate must be able to take a free resource");
	}

	[Fact]
	public async Task StillHoldTheResourceAcrossRenewalTicks_WhenTheCandidateIsRunning()
	{
		var state = new InMemoryLeaderElectionSharedState();
		await using var candidate = NewCandidate("A", state, renewInterval: TimeSpan.FromMilliseconds(1));

		await candidate.StartAsync(CancellationToken.None);
		candidate.IsLeader.ShouldBeTrue();

		// Long enough for many renewal callbacks to run against a live candidate.
		await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

		candidate.IsLeader.ShouldBeTrue(
			"a running leader must keep the resource across renewals; the lifecycle guard applies to an "
			+ "acquisition by a stopped candidate, never to a live tenure");
	}

	[Fact]
	public async Task StillAcquire_WhenACandidateStandsAgainAfterStopping()
	{
		var state = new InMemoryLeaderElectionSharedState();
		await using var candidate = NewCandidate("A", state);

		await candidate.StartAsync(CancellationToken.None);
		await candidate.StopAsync(CancellationToken.None);
		candidate.IsLeader.ShouldBeFalse();

		await candidate.StartAsync(CancellationToken.None);

		candidate.IsLeader.ShouldBeTrue("stopping must not permanently bar a candidate from standing again");
	}

	[Fact]
	public async Task StillHandTheResourceOn_WhenTheStoppedCandidateIsReplaced()
	{
		var state = new InMemoryLeaderElectionSharedState();
		var outgoing = NewCandidate("A", state);

		await outgoing.StartAsync(CancellationToken.None);
		await outgoing.StopAsync(CancellationToken.None);
		await outgoing.DisposeAsync();

		await using var successor = NewCandidate("B", state);
		await successor.StartAsync(CancellationToken.None);

		successor.IsLeader.ShouldBeTrue("a stopped candidate must leave the resource free for the next one");
	}

	// -------------------------------------------------------------------------------------------------

	/// <summary>
	/// Races one candidate start against its own stop, then asks whether the resource is still held by
	/// that candidate once every documented cleanup path has run.
	/// </summary>
	/// <remarks>
	/// Disposal is the last word on cleanup, so the probe runs after it. That is also where the defect
	/// becomes permanent rather than transient: disposal stops first, and a stop that finds the candidate
	/// already stopped returns without releasing, so a tenure begun after the shutdown survives its own
	/// dispose.
	/// </remarks>
	private static async Task<bool> RunStartStopRaceAsync()
	{
		var state = new InMemoryLeaderElectionSharedState();
		var candidate = NewCandidate(RacingCandidate, state);

		using (var barrier = new Barrier(2))
		{
			var starting = Task.Run(async () =>
			{
				barrier.SignalAndWait();
				await candidate.StartAsync(CancellationToken.None);
			});

			var stopping = Task.Run(async () =>
			{
				barrier.SignalAndWait();
				await candidate.StopAsync(CancellationToken.None);
			});

			await Task.WhenAll(starting, stopping);
		}

		await candidate.DisposeAsync();

		return HoldsResource(state, RacingCandidate);
	}

	/// <summary>
	/// Races a lease-renewal tick against a stop. The contender is kept off the resource while its timer
	/// runs, so its callbacks take the acquisition branch rather than the already-leader branch, and the
	/// resource is freed at the same instant the contender is stopped.
	/// </summary>
	private static async Task<bool> RunRenewalStopRaceAsync()
	{
		var state = new InMemoryLeaderElectionSharedState();
		var incumbent = NewCandidate("incumbent", state);
		var contender = NewCandidate(RacingCandidate, state, renewInterval: TimeSpan.FromMilliseconds(1));

		await incumbent.StartAsync(CancellationToken.None);
		await contender.StartAsync(CancellationToken.None);

		// The contender is now retrying on its renewal timer, taking the acquisition branch each tick.
		await Task.Delay(TimeSpan.FromMilliseconds(5), TestContext.Current.CancellationToken);

		using (var barrier = new Barrier(2))
		{
			var freeing = Task.Run(async () =>
			{
				barrier.SignalAndWait();
				await incumbent.StopAsync(CancellationToken.None);
			});

			var stopping = Task.Run(async () =>
			{
				barrier.SignalAndWait();
				await contender.StopAsync(CancellationToken.None);
			});

			await Task.WhenAll(freeing, stopping);
		}

		await contender.DisposeAsync();
		await incumbent.DisposeAsync();

		return HoldsResource(state, RacingCandidate);
	}

	/// <summary>
	/// Reports whether the named candidate still holds the resource, read through the same public surface
	/// a consumer would read it through.
	/// </summary>
	private static bool HoldsResource(InMemoryLeaderElectionSharedState state, string candidateId)
	{
		using var probe = NewCandidate("probe", state);
		return string.Equals(probe.CurrentLeaderId, candidateId, StringComparison.Ordinal);
	}

	private static InMemoryLeaderElection NewCandidate(
		string instanceId,
		InMemoryLeaderElectionSharedState state,
		TimeSpan? renewInterval = null)
	{
		var options = new LeaderElectionOptions
		{
			InstanceId = instanceId,

			// An hour by default, so only explicit start and stop contend and a violation is attributable
			// to the lifecycle race rather than to a background renewal reacquiring behind it.
			RenewInterval = renewInterval ?? TimeSpan.FromHours(1),
		};

		return new InMemoryLeaderElection(
			Resource,
			Options.Create(options),
			NullLogger<InMemoryLeaderElection>.Instance,
			state);
	}
}
