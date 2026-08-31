// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// Binds mutual exclusion against a <i>stale releaser</i>: a candidate that observes itself leader and
/// then relinquishes must remove <b>its own</b> leadership record, never whichever record occupies the
/// resource at the moment of removal.
/// </summary>
/// <remarks>
/// <para>
/// The invariant: <b>of N candidates contending for one resource, at most one observes itself to be
/// leader at any instant.</b> This provider has no lease expiry and no fencing token, so the
/// expiry-plus-grace window that the guarantee permits elsewhere does not exist here — any interleaving
/// that produces two self-observed leaders violates it outright, and nothing downstream ever corrects it.
/// </para>
/// <para>
/// The interleaving that breaks it, with a removal that does not compare the value:
/// candidate A holds the resource and two of its own release paths run concurrently (a shutdown and a
/// dispose, or a shutdown and an unhealthy step-down). Both read "I am the leader". The first removes A's
/// record; candidate B acquires the now-free resource and is told it became leader. The second release
/// then removes <i>B's</i> record — a record it never read. B is never told it lost anything, so it goes
/// on believing it leads, while the resource is free for candidate C to acquire and be told the same.
/// </para>
/// <para>
/// The read and the removal are adjacent instructions with no interposition point, so these arms drive
/// the window by contention rather than by ordering: a spin of fresh candidates contends for the
/// resource across the release race, and a violation is recorded whenever two candidates simultaneously
/// hold an unrevoked belief that they lead. See the file's liveness arms — a release predicate can be
/// tightened until it never releases at all, and every safety arm here would still pass.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryLeaderElectionStaleReleaseShould
{
	private const string Resource = "stale-release-resource";
	private const int RaceIterations = 200_000;

	[Fact]
	public async Task NeverLeaveTwoCandidatesBelievingTheyLead_WhenTwoOfTheLeadersOwnReleasePathsRace()
	{
		var violations = new List<int>();

		for (var iteration = 0; iteration < RaceIterations; iteration++)
		{
			if (await RunReleaseRaceAsync(alsoStepDownUnhealthy: false))
			{
				violations.Add(iteration);
			}
		}

		violations.ShouldBeEmpty(
			$"mutual exclusion broke on {violations.Count} of {RaceIterations} iterations: two candidates each "
			+ "held an unrevoked belief that they led, because the outgoing leader's second release removed a "
			+ "successor's record instead of its own");
	}

	[Fact]
	public async Task NeverLeaveTwoCandidatesBelievingTheyLead_WhenShutdownRacesTheUnhealthyStepDown()
	{
		var violations = new List<int>();

		for (var iteration = 0; iteration < RaceIterations; iteration++)
		{
			if (await RunReleaseRaceAsync(alsoStepDownUnhealthy: true))
			{
				violations.Add(iteration);
			}
		}

		violations.ShouldBeEmpty(
			$"mutual exclusion broke on {violations.Count} of {RaceIterations} iterations: the unhealthy "
			+ "step-down and the shutdown are separate release paths and neither excludes the other, so one "
			+ "of them removed a successor's leadership record");
	}

	// -------------------------------------------------------------------------------------------------
	// Liveness: a release must still release. A predicate tightened until it never removes anything
	// satisfies every safety arm above while wedging leadership shut forever.
	// -------------------------------------------------------------------------------------------------

	[Fact]
	public async Task HandLeadershipToTheNextCandidate_WhenTheLeaderStops()
	{
		var state = new InMemoryLeaderElectionSharedState();
		await using var leader = Candidate("A", state);
		await using var successor = Candidate("B", state);

		await leader.StartAsync(CancellationToken.None);
		await successor.StartAsync(CancellationToken.None);
		leader.IsLeader.ShouldBeTrue();
		successor.IsLeader.ShouldBeFalse();

		await leader.StopAsync(CancellationToken.None);
		leader.IsLeader.ShouldBeFalse("stopping must actually relinquish the resource");

		// The successor's next acquisition attempt must find the resource free.
		await Candidate("B2", state).StartAsync(CancellationToken.None);
		state.ShouldHaveALeader();
	}

	[Fact]
	public async Task HandLeadershipToTheNextCandidate_WhenTheLeaderIsDisposed()
	{
		var state = new InMemoryLeaderElectionSharedState();
		var leader = Candidate("A", state);
		await leader.StartAsync(CancellationToken.None);
		leader.IsLeader.ShouldBeTrue();

		leader.Dispose();

		await using var successor = Candidate("B", state);
		await successor.StartAsync(CancellationToken.None);
		successor.IsLeader.ShouldBeTrue("disposing must actually relinquish the resource");
	}

	[Fact]
	public async Task RelinquishAndAnnounceIt_WhenTheLeaderBecomesUnhealthy()
	{
		var state = new InMemoryLeaderElectionSharedState();
		await using var leader = Candidate("A", state, stepDownWhenUnhealthy: true);

		var lostLeadership = 0;
		leader.LostLeadership += (_, _) => Interlocked.Increment(ref lostLeadership);

		await leader.StartAsync(CancellationToken.None);
		leader.IsLeader.ShouldBeTrue();

		await leader.UpdateHealthAsync(isHealthy: false, metadata: null, CancellationToken.None);

		leader.IsLeader.ShouldBeFalse("an unhealthy leader configured to step down must relinquish");
		lostLeadership.ShouldBe(1, "the step-down must still announce the loss");

		await using var successor = Candidate("B", state);
		await successor.StartAsync(CancellationToken.None);
		successor.IsLeader.ShouldBeTrue();
	}

	[Fact]
	public async Task LeaveTheIncumbentAlone_WhenANonLeaderStopsAndDisposes()
	{
		var state = new InMemoryLeaderElectionSharedState();
		await using var leader = Candidate("A", state);
		var follower = Candidate("B", state);

		await leader.StartAsync(CancellationToken.None);
		await follower.StartAsync(CancellationToken.None);
		follower.IsLeader.ShouldBeFalse();

		await follower.StopAsync(CancellationToken.None);
		follower.Dispose();

		leader.IsLeader.ShouldBeTrue("a candidate that never led must not evict the incumbent on the way out");
	}

	// -------------------------------------------------------------------------------------------------

	/// <summary>
	/// Runs one release race and reports whether two candidates were left simultaneously believing they
	/// lead. A candidate "believes it leads" from the moment it is told it became leader until it is told
	/// it lost leadership — which is the only signal a consumer has, since this provider never expires a
	/// tenure on its own.
	/// </summary>
	private static async Task<bool> RunReleaseRaceAsync(bool alsoStepDownUnhealthy)
	{
		var state = new InMemoryLeaderElectionSharedState();

		var incumbent = Candidate("incumbent", state, stepDownWhenUnhealthy: alsoStepDownUnhealthy);
		var successor = Candidate("successor", state);

		// The successor believes it leads from the moment it is told it became leader until it is told it
		// lost leadership. That belief is the only signal a consumer has: this provider never expires a
		// tenure, so nothing else will ever revoke it.
		var successorBelievesItLeads = false;
		successor.BecameLeader += (_, _) => successorBelievesItLeads = true;
		successor.LostLeadership += (_, _) => successorBelievesItLeads = false;

		// A supervisor starting a replacement the moment the incumbent announces it stepped down. This
		// runs inline on whichever release path removed the record, so the successor acquires inside the
		// window between the incumbent's two releases rather than waiting to stumble into it.
		var successorStarted = 0;
		incumbent.LostLeadership += (_, _) =>
		{
			if (Interlocked.Exchange(ref successorStarted, 1) == 0)
			{
				_ = successor.StartAsync(CancellationToken.None);
			}
		};

		await incumbent.StartAsync(CancellationToken.None);
		incumbent.IsLeader.ShouldBeTrue();

		using (var barrier = new Barrier(2))
		{
			var first = Task.Run(async () =>
			{
				barrier.SignalAndWait();
				await incumbent.StopAsync(CancellationToken.None);
			});

			var second = Task.Run(async () =>
			{
				barrier.SignalAndWait();
				if (alsoStepDownUnhealthy)
				{
					await incumbent.UpdateHealthAsync(isHealthy: false, metadata: null, CancellationToken.None);
				}
				else
				{
					incumbent.Dispose();
				}
			});

			await Task.WhenAll(first, second);
		}

		// The violation: the successor was told it became leader, was never told otherwise, and yet the
		// resource no longer carries its record — so it believes it leads while the resource sits free for
		// the next candidate to acquire and be told exactly the same thing.
		var violated = successorBelievesItLeads && !successor.IsLeader;

		successor.Dispose();
		incumbent.Dispose();

		return violated;
	}

	private static InMemoryLeaderElection Candidate(
		string instanceId,
		InMemoryLeaderElectionSharedState state,
		bool stepDownWhenUnhealthy = false)
	{
		var options = new LeaderElectionOptions
		{
			InstanceId = instanceId,
			StepDownWhenUnhealthy = stepDownWhenUnhealthy,

			// Only explicit acquisition contends, so a violation is attributable to the release race
			// rather than to a background renewal reacquiring behind it.
			RenewInterval = TimeSpan.FromHours(1),
		};

		return new InMemoryLeaderElection(
			Resource,
			Options.Create(options),
			NullLogger<InMemoryLeaderElection>.Instance,
			state);
	}
}

internal static class SharedStateAssertions
{
	public static void ShouldHaveALeader(this InMemoryLeaderElectionSharedState state)
	{
		var probe = new InMemoryLeaderElection(
			"stale-release-resource",
			Options.Create(new LeaderElectionOptions { InstanceId = "probe", RenewInterval = TimeSpan.FromHours(1) }),
			NullLogger<InMemoryLeaderElection>.Instance,
			state);

		using (probe)
		{
			probe.CurrentLeaderId.ShouldNotBeNull("the resource must be held by someone after a successful takeover");
		}
	}
}
