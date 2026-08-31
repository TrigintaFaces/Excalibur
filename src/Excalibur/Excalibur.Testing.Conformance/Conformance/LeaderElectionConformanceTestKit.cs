// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ILeaderElection conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateElection"/> to verify that
/// your leader election implementation conforms to the ILeaderElection contract.
/// </para>
/// <para>
/// The test kit verifies core leader election operations including lifecycle management,
/// leadership acquisition, multi-candidate contention, event firing, and property behavior.
/// </para>
/// <para>
/// <strong>Two rules govern every arm here, because the contract is narrower than it first reads.</strong>
/// </para>
/// <para>
/// <strong>Acquisition is eventual, so leadership is only ever read after awaiting it.</strong>
/// <see cref="ILeaderElection.StartAsync"/> promises that the instance <em>starts participating</em> — not
/// that it has won. A provider is free to acquire inside <c>StartAsync</c> or to return once a background
/// loop is running and acquire a round trip later; both conform. An arm that reads
/// <see cref="ILeaderElection.IsLeader"/> on the statement after <c>StartAsync</c> therefore tests which
/// side of a round trip the reader happened to land on, and it encodes one provider's timing as the
/// contract. Arms here poll to a deadline instead — see <see cref="WaitUntilAsync"/>.
/// </para>
/// <para>
/// <strong><see cref="ILeaderElection.CurrentLeaderId"/> is asserted only on the instance that holds
/// leadership.</strong> It is guaranteed non-null there and best-effort everywhere else: a provider that
/// arbitrates through a primitive carrying no owner identity — an advisory lock answering only true or
/// false to the instance attempting it — has nothing to report to a follower and returns
/// <see langword="null"/>. That is conforming, not a bug, so <see langword="null"/> on a follower means
/// <em>unknown</em> and never <em>no leader</em>. What a follower may not do is name an instance that is
/// not the leader, and that is what the contention arms check.
/// </para>
/// <para>
/// <strong>CRITICAL:</strong> Each test uses a unique resource name to avoid static state
/// contamination between tests. The <see cref="GenerateResourceName"/> method provides
/// unique resource names for test isolation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class RedisLeaderElectionConformanceTests : LeaderElectionConformanceTestKit
/// {
///     private readonly RedisFixture _fixture;
///
///     protected override ILeaderElection CreateElection(string resourceName, string? candidateId) =>
///         new RedisLeaderElection(
///             resourceName,
///             Options.Create(new LeaderElectionOptions { InstanceId = candidateId ?? GenerateCandidateId() }),
///             _fixture.ConnectionMultiplexer,
///             NullLogger&lt;RedisLeaderElection&gt;.Instance);
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class LeaderElectionConformanceTestKit : ConformanceTestKit
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

	/// <summary>
	/// Gets the timeout for event verification.
	/// </summary>
	/// <value>The timeout duration for waiting on events.</value>
	protected virtual TimeSpan EventTimeout => TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets how long an incumbent leader is observed before a challenger is accepted as excluded.
	/// </summary>
	/// <value>The observation window for the contention arms.</value>
	/// <remarks>
	/// This bounds a negative wait — the arm is proving that a second candidate does <em>not</em> acquire —
	/// so it is spent in full on every run and is deliberately shorter than <see cref="EventTimeout"/>.
	/// Raise it for a provider whose acquisition retry is slower than this window, or the arm will conclude
	/// exclusion from a challenger that had simply not tried yet.
	/// </remarks>
	protected virtual TimeSpan ContentionSettleWindow => TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the number of candidates started simultaneously by the concurrent-contention arm.
	/// </summary>
	/// <value>The candidate count; must be at least two for the arm to contend at all.</value>
	protected virtual int ConcurrentCandidateCount => 4;

	/// <summary>
	/// Creates a fresh leader election instance for testing.
	/// </summary>
	/// <param name="resourceName">The name of the resource to elect a leader for.</param>
	/// <param name="candidateId">Optional candidate identifier. If not provided, a unique ID is generated.</param>
	/// <returns>An ILeaderElection implementation to test.</returns>
	protected abstract ILeaderElection CreateElection(string resourceName, string? candidateId);

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Clears residual election state before an arm runs. Defaults to <see cref="CleanupAsync"/>.
	/// </summary>
	/// <returns>A task that completes when no lease from a previous arm remains.</returns>
	/// <remarks>
	/// <para>
	/// Invoked once at the START of each arm rather than around each election, because an arm that
	/// contends two candidates for one resource would otherwise reset between them and delete the lease
	/// the first candidate had just taken — turning a contention arm into a test of nothing.
	/// </para>
	/// <para>
	/// Resetting before an arm is what makes the arm independent; resetting only afterwards makes every
	/// arm's starting state a function of whether its predecessor finished cleanly. Arms here also use a
	/// freshly generated resource name, so this is defence in depth rather than the sole isolation.
	/// </para>
	/// </remarks>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>
	/// Generates a unique resource name for test isolation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// CRITICAL: InMemoryLeaderElection uses static dictionaries keyed by resource name.
	/// Each test MUST use a unique resource name to prevent cross-test contamination.
	/// </para>
	/// </remarks>
	/// <returns>A unique resource name.</returns>
	protected virtual string GenerateResourceName() =>
		$"test-resource-{Guid.NewGuid():N}";

	/// <summary>
	/// Generates a unique candidate ID for test isolation.
	/// </summary>
	/// <returns>A unique candidate identifier.</returns>
	protected virtual string GenerateCandidateId() =>
		$"candidate-{Guid.NewGuid():N}";

	#region Helpers

	/// <summary>
	/// Polls a condition to a deadline.
	/// </summary>
	/// <param name="condition">The condition to observe.</param>
	/// <param name="timeout">How long to keep observing before giving up.</param>
	/// <returns><see langword="true"/> if the condition was observed within the timeout; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="condition"/> is null.</exception>
	/// <remarks>
	/// Returns a result rather than throwing, because a timeout is the expected outcome of the arms that
	/// prove something does <em>not</em> happen. Arms asserting that leadership <em>is</em> acquired call
	/// <see cref="AwaitLeadershipAsync"/>, which turns the timeout into a failure with the reason attached.
	/// </remarks>
	protected static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(condition);

		var start = TimeProvider.System.GetTimestamp();

		while (!condition())
		{
			if (TimeProvider.System.GetElapsedTime(start) >= timeout)
			{
				return false;
			}

			await Task.Delay(PollInterval, CancellationToken.None).ConfigureAwait(false);
		}

		return true;
	}

	/// <summary>
	/// Waits until an instance holds leadership, failing the arm if it never does.
	/// </summary>
	/// <param name="election">The instance expected to acquire leadership.</param>
	/// <param name="because">What the arm was relying on leadership for, used in the failure message.</param>
	/// <returns>A task that completes once leadership is observed.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="election"/> is null.</exception>
	protected async Task AwaitLeadershipAsync(ILeaderElection election, string because)
	{
		ArgumentNullException.ThrowIfNull(election);

		if (!await WaitUntilAsync(() => election.IsLeader, EventTimeout).ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException(
				$"{because} Waited {EventTimeout} for '{election.CandidateId}' to acquire leadership and it never did.");
		}
	}

	/// <summary>
	/// Stops and disposes every election an arm created.
	/// </summary>
	/// <param name="elections">The elections to tear down, in order.</param>
	/// <returns>A task that completes once all have been released.</returns>
	/// <remarks>
	/// <see cref="ILeaderElection"/> derives from <see cref="IAsyncDisposable"/>, so disposal is awaited
	/// rather than attempted through an <see cref="IDisposable"/> cast — a provider that implements only
	/// the async form would otherwise be left holding its lease and its connection for the rest of the run.
	/// </remarks>
	protected static async Task StopAndDisposeAsync(params ILeaderElection[] elections)
	{
		ArgumentNullException.ThrowIfNull(elections);

		foreach (var election in elections)
		{
			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			await election.DisposeAsync().ConfigureAwait(false);
		}
	}

	#endregion Helpers

	#region Lifecycle Tests

	/// <summary>
	/// Verifies that StartAsync initiates participation and leadership follows for an uncontended candidate.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The liveness half of the contract: an election that elects nobody violates nothing a safety
	/// assertion can see, so the arm that catches an inert provider is this one.
	/// </remarks>
	public virtual async Task StartAsync_ShouldInitiateParticipation()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			await AwaitLeadershipAsync(
				election,
				"A single uncontended candidate must eventually become leader after StartAsync.")
				.ConfigureAwait(false);
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that StopAsync ends election participation and relinquishes leadership.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual async Task StopAsync_ShouldRelinquishLeadership()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			await AwaitLeadershipAsync(
				election,
				"The candidate must hold leadership before StopAsync can be shown to relinquish it.")
				.ConfigureAwait(false);

			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);

			// StopAsync's postcondition is delivered by the awaited task, so this read is not a race.
			if (election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Should not be leader after StopAsync");
			}
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that StartAsync can be called after StopAsync to restart participation.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual async Task StartAsync_AfterStop_ShouldRestartElection()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);
			await AwaitLeadershipAsync(election, "Should acquire leadership after the first StartAsync.")
				.ConfigureAwait(false);

			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			if (election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Should not be leader after StopAsync");
			}

			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);
			await AwaitLeadershipAsync(
				election,
				"Should reacquire leadership after restarting; a stopped election that cannot restart is "
				+ "indistinguishable from one that never participates.")
				.ConfigureAwait(false);
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	#endregion Lifecycle Tests

	#region Single-Candidate Leadership Tests

	/// <summary>
	/// Verifies that a single candidate becomes leader after starting.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual async Task StartAsync_SingleCandidate_ShouldBecomeLeader()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			await AwaitLeadershipAsync(
				election,
				"Single candidate should become leader after StartAsync.")
				.ConfigureAwait(false);
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that IsLeader is false before starting and true once a single candidate has acquired.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual async Task StartAsync_SingleCandidate_IsLeaderShouldBeTrue()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			// Read before StartAsync, where nothing is in flight and the value cannot be a race.
			if (election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Should not be leader before StartAsync");
			}

			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			await AwaitLeadershipAsync(
				election,
				"IsLeader should become true after a single candidate starts.")
				.ConfigureAwait(false);
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that a leader names itself through CurrentLeaderId.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// This is the half of <see cref="ILeaderElection.CurrentLeaderId"/> the contract guarantees: it is
	/// non-null while the instance being asked is itself the leader. It is read only after leadership has
	/// been observed, so an empty value here is a provider that leads without publishing its own identity —
	/// not a provider that had not acquired yet.
	/// </remarks>
	public virtual async Task StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			await AwaitLeadershipAsync(
				election,
				"CurrentLeaderId is guaranteed only while this instance leads, so the arm waits for that first.")
				.ConfigureAwait(false);

			if (election.CurrentLeaderId != election.CandidateId)
			{
				throw new TestFixtureAssertionException(
					$"A leader must name itself through CurrentLeaderId. Expected: {election.CandidateId}, Actual: {election.CurrentLeaderId}");
			}
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	#endregion Single-Candidate Leadership Tests

	#region Multi-Candidate Tests

	/// <summary>
	/// Verifies that only one candidate leads when two candidates contend for one resource.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The candidates start in sequence here, which cannot produce the interleaving that breaks mutual
	/// exclusion — see <see cref="ConcurrentContention_ExactlyOneLeader"/> for the arm that can. Both arms
	/// assert that <em>someone</em> leads before counting, so a provider that returns nothing to anybody
	/// fails rather than passing on an empty set.
	/// </remarks>
	public virtual async Task MultipleCandidate_OnlyOneBecomesLeader()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();

		var election1 = CreateElection(resourceName, "candidate-1");
		var election2 = CreateElection(resourceName, "candidate-2");
		var candidates = new[] { election1, election2 };

		try
		{
			await election1.StartAsync(CancellationToken.None).ConfigureAwait(false);
			await election2.StartAsync(CancellationToken.None).ConfigureAwait(false);

			if (!await WaitUntilAsync(() => Array.Exists(candidates, c => c.IsLeader), EventTimeout).ConfigureAwait(false))
			{
				throw new TestFixtureAssertionException(
					$"No candidate acquired leadership within {EventTimeout}. An election that elects nobody "
					+ "satisfies mutual exclusion and is still broken.");
			}

			var leaders = Array.FindAll(candidates, c => c.IsLeader);

			if (leaders.Length != 1)
			{
				throw new TestFixtureAssertionException(
					$"Expected exactly 1 leader, found {leaders.Length}: {string.Join(", ", leaders.Select(l => l.CandidateId))}");
			}
		}
		finally
		{
			await StopAndDisposeAsync(election1, election2).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that a reported CurrentLeaderId names the instance that actually holds leadership.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// A follower is not required to name the leader at all. Leader discovery is an optional capability:
	/// a provider arbitrating through a primitive that carries no owner identity learns only whether it
	/// won, so it has nothing to report and returns <see langword="null"/>, which callers must read as
	/// <em>unknown</em>. Requiring every candidate to agree on a non-null value would fail those providers
	/// for conforming.
	/// </para>
	/// <para>
	/// What the contract does forbid is naming the wrong instance. So the arm requires the leader to name
	/// itself, and requires every follower's answer to be either <see langword="null"/> or that same
	/// identity — never a third value, and never an instance that is not leading.
	/// </para>
	/// </remarks>
	public virtual async Task MultipleCandidate_ReportedLeaderIdShouldNameTheLeader()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();

		var election1 = CreateElection(resourceName, "candidate-1");
		var election2 = CreateElection(resourceName, "candidate-2");
		var candidates = new[] { election1, election2 };

		try
		{
			await election1.StartAsync(CancellationToken.None).ConfigureAwait(false);
			await election2.StartAsync(CancellationToken.None).ConfigureAwait(false);

			if (!await WaitUntilAsync(() => Array.Exists(candidates, c => c.IsLeader), EventTimeout).ConfigureAwait(false))
			{
				throw new TestFixtureAssertionException(
					$"No candidate acquired leadership within {EventTimeout}, so there was no leader for "
					+ "CurrentLeaderId to name.");
			}

			var leader = Array.Find(candidates, c => c.IsLeader)!;

			if (leader.CurrentLeaderId != leader.CandidateId)
			{
				throw new TestFixtureAssertionException(
					$"The leader must name itself through CurrentLeaderId. Expected: {leader.CandidateId}, Actual: {leader.CurrentLeaderId}");
			}

			foreach (var follower in candidates.Where(c => !ReferenceEquals(c, leader)))
			{
				var reported = follower.CurrentLeaderId;

				// null is "unknown", which is conforming for a provider whose primitive carries no owner
				// identity. A non-null value is a claim, and the claim must be the actual leader.
				if (reported is not null && reported != leader.CandidateId)
				{
					throw new TestFixtureAssertionException(
						$"Candidate '{follower.CandidateId}' reports '{reported}' as leader, but leadership is held "
						+ $"by '{leader.CandidateId}'. A follower may report null for unknown; it may not name a "
						+ "non-leader.");
				}
			}
		}
		finally
		{
			await StopAndDisposeAsync(election1, election2).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that an incumbent leader keeps leadership and excludes a candidate that starts against it.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// This arm does not require the first candidate to <em>start</em> first — start order is not part of
	/// the contract, and no provider owes first-come-first-served arbitration. It waits for one candidate
	/// to actually hold leadership, and only then starts the second, which is what makes the first an
	/// incumbent rather than merely the earlier caller.
	/// </para>
	/// <para>
	/// Two properties are then checked over the settle window: the challenger never leads while the
	/// incumbent does (safety), and the incumbent is not displaced while it is running and renewing
	/// (liveness). Without the second, a provider whose leases silently lapse would pass — the challenger
	/// would be excluded because nobody was leading at all.
	/// </para>
	/// </remarks>
	public virtual async Task MultipleCandidate_IncumbentShouldExcludeLaterCandidate()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();

		var incumbent = CreateElection(resourceName, "candidate-1");
		var challenger = CreateElection(resourceName, "candidate-2");

		try
		{
			await incumbent.StartAsync(CancellationToken.None).ConfigureAwait(false);

			await AwaitLeadershipAsync(
				incumbent,
				"The first candidate must hold leadership before a challenger can be excluded by it.")
				.ConfigureAwait(false);

			await challenger.StartAsync(CancellationToken.None).ConfigureAwait(false);

			var bothLed = await WaitUntilAsync(
				() => incumbent.IsLeader && challenger.IsLeader,
				ContentionSettleWindow).ConfigureAwait(false);

			if (bothLed)
			{
				throw new TestFixtureAssertionException(
					"Both candidates held leadership at the same time. Mutual exclusion is the one guarantee "
					+ "every provider owes.");
			}

			if (!incumbent.IsLeader)
			{
				throw new TestFixtureAssertionException(
					$"The incumbent lost leadership within {ContentionSettleWindow} while still running and "
					+ "renewing. Its lease is lapsing, and any exclusion this arm observed was vacuous — there "
					+ "was no leader to exclude anyone.");
			}

			if (challenger.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"The second candidate is leader while the first still holds leadership.");
			}
		}
		finally
		{
			await StopAndDisposeAsync(incumbent, challenger).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that exactly one candidate leads when several start simultaneously.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// Mutual exclusion is a claim about what happens when candidates race, and candidates started one
	/// after another do not race: the first has finished acquiring before the second begins, so no
	/// interleaving that could produce two leaders is ever attempted. An arm that starts sequentially is
	/// not evidence for exclusion however green it is. This one starts every candidate under a single
	/// <see cref="Task.WhenAll(System.Collections.Generic.IEnumerable{Task})"/> so their acquisitions
	/// overlap.
	/// </para>
	/// <para>
	/// Both halves are asserted: someone leads (liveness — a provider that elects nobody cannot pass by
	/// being trivially safe), and exactly one does (safety).
	/// </para>
	/// </remarks>
	public virtual async Task ConcurrentContention_ExactlyOneLeader()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var count = Math.Max(2, ConcurrentCandidateCount);

		var candidates = new ILeaderElection[count];

		for (var i = 0; i < count; i++)
		{
			candidates[i] = CreateElection(resourceName, $"candidate-{i + 1}");
		}

		try
		{
			await Task.WhenAll(candidates.Select(c => c.StartAsync(CancellationToken.None))).ConfigureAwait(false);

			if (!await WaitUntilAsync(() => Array.Exists(candidates, c => c.IsLeader), EventTimeout).ConfigureAwait(false))
			{
				throw new TestFixtureAssertionException(
					$"None of {count} simultaneous candidates acquired leadership within {EventTimeout}. "
					+ "An election that elects nobody is inert, not safe.");
			}

			var leaders = Array.FindAll(candidates, c => c.IsLeader);

			if (leaders.Length != 1)
			{
				throw new TestFixtureAssertionException(
					$"Expected exactly 1 leader among {count} simultaneous candidates, found {leaders.Length}: "
					+ string.Join(", ", leaders.Select(l => l.CandidateId)));
			}
		}
		finally
		{
			await StopAndDisposeAsync(candidates).ConfigureAwait(false);
		}
	}

	#endregion Multi-Candidate Tests

	#region Event Tests

	/// <summary>
	/// Verifies that BecameLeader event fires when a candidate becomes leader.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual async Task BecameLeader_ShouldFireWhenElected()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);
		var tcs = new TaskCompletionSource<LeaderElectionEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

		election.BecameLeader += (s, e) => tcs.TrySetResult(e);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			var completedTask = await Task.WhenAny(
				tcs.Task,
				Task.Delay(EventTimeout, CancellationToken.None)
			).ConfigureAwait(false);

			if (completedTask != tcs.Task)
			{
				throw new TestFixtureAssertionException(
					"BecameLeader event was not fired within timeout");
			}

			var args = await tcs.Task.ConfigureAwait(false);

			if (args.CandidateId != election.CandidateId)
			{
				throw new TestFixtureAssertionException(
					$"BecameLeader CandidateId mismatch. Expected: {election.CandidateId}, Actual: {args.CandidateId}");
			}
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that LostLeadership event fires when a leader stops.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual async Task LostLeadership_ShouldFireWhenStopped()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);
		var tcs = new TaskCompletionSource<LeaderElectionEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

		election.LostLeadership += (s, e) => tcs.TrySetResult(e);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			await AwaitLeadershipAsync(
				election,
				"Leadership must be held before stopping can be shown to lose it.")
				.ConfigureAwait(false);

			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);

			var completedTask = await Task.WhenAny(
				tcs.Task,
				Task.Delay(EventTimeout, CancellationToken.None)
			).ConfigureAwait(false);

			if (completedTask != tcs.Task)
			{
				throw new TestFixtureAssertionException(
					"LostLeadership event was not fired within timeout");
			}

			var args = await tcs.Task.ConfigureAwait(false);

			if (args.CandidateId != election.CandidateId)
			{
				throw new TestFixtureAssertionException(
					$"LostLeadership CandidateId mismatch. Expected: {election.CandidateId}, Actual: {args.CandidateId}");
			}
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that LeaderChanged event fires when leadership changes.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual async Task LeaderChanged_ShouldFireOnLeadershipChange()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);
		var tcs = new TaskCompletionSource<LeaderChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

		election.LeaderChanged += (s, e) => tcs.TrySetResult(e);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			var completedTask = await Task.WhenAny(
				tcs.Task,
				Task.Delay(EventTimeout, CancellationToken.None)
			).ConfigureAwait(false);

			if (completedTask != tcs.Task)
			{
				throw new TestFixtureAssertionException(
					"LeaderChanged event was not fired within timeout");
			}

			var args = await tcs.Task.ConfigureAwait(false);

			if (args.NewLeaderId != election.CandidateId)
			{
				throw new TestFixtureAssertionException(
					$"LeaderChanged NewLeaderId should be {election.CandidateId}, got {args.NewLeaderId}");
			}
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	#endregion Event Tests

	#region Property Tests

	/// <summary>
	/// Verifies that CandidateId is unique per instance.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	public virtual async Task CandidateId_ShouldBeUniquePerInstance()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName1 = GenerateResourceName();
		var resourceName2 = GenerateResourceName();

		var election1 = CreateElection(resourceName1, candidateId: null);
		var election2 = CreateElection(resourceName2, candidateId: null);

		try
		{
			if (election1.CandidateId == election2.CandidateId)
			{
				throw new TestFixtureAssertionException(
					"CandidateId should be unique per instance");
			}

			if (string.IsNullOrEmpty(election1.CandidateId))
			{
				throw new TestFixtureAssertionException(
					"CandidateId should not be null or empty");
			}

			if (string.IsNullOrEmpty(election2.CandidateId))
			{
				throw new TestFixtureAssertionException(
					"CandidateId should not be null or empty");
			}
		}
		finally
		{
			await election1.DisposeAsync().ConfigureAwait(false);
			await election2.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that leadership is held while leading and gone after stopping.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// This binds <see cref="ILeaderElection.CurrentLeadership"/>, which the contract calls the
	/// authoritative check: non-null if and only if this instance currently considers itself the leader,
	/// carrying the fencing token for the tenure.
	/// </para>
	/// <para>
	/// It is deliberately not an assertion about <see cref="ILeaderElection.CurrentLeaderId"/> after
	/// stopping. That property is best-effort, and on an instance that is not leading its value is a cache
	/// of a past observation — a provider that still returns a stale identity there has not broken any
	/// promise, so requiring it to be null would fail conforming providers for something the contract
	/// never asked of them.
	/// </para>
	/// </remarks>
	public virtual async Task CurrentLeadership_AfterStop_ShouldBeNull()
	{
		await ResetDataAsync().ConfigureAwait(false);

		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			await AwaitLeadershipAsync(
				election,
				"Leadership must be held before stopping can be shown to release it.")
				.ConfigureAwait(false);

			if (election.CurrentLeadership is null)
			{
				throw new TestFixtureAssertionException(
					"CurrentLeadership must be non-null while this instance is the leader; it is the "
					+ "authoritative leadership check and carries the tenure's fencing token.");
			}

			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);

			if (election.CurrentLeadership is not null)
			{
				throw new TestFixtureAssertionException(
					"CurrentLeadership must be null after StopAsync; a non-null value claims a tenure this "
					+ "instance has relinquished.");
			}
		}
		finally
		{
			await StopAndDisposeAsync(election).ConfigureAwait(false);
		}
	}

	#endregion Property Tests

}
