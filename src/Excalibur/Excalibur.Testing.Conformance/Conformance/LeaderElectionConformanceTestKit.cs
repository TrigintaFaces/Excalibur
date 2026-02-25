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
public abstract class LeaderElectionConformanceTestKit
{
	/// <summary>
	/// Gets the timeout for event verification.
	/// </summary>
	/// <value>The timeout duration for waiting on events.</value>
	protected virtual TimeSpan EventTimeout => TimeSpan.FromSeconds(5);

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

	#region Lifecycle Tests

	/// <summary>
	/// Verifies that StartAsync initiates election participation.
	/// </summary>
	public virtual async Task StartAsync_ShouldInitiateParticipation()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			// Single candidate should become leader after start
			if (!election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Single candidate should become leader after StartAsync");
			}
		}
		finally
		{
			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that StopAsync ends election participation and relinquishes leadership.
	/// </summary>
	public virtual async Task StopAsync_ShouldRelinquishLeadership()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			// Verify leader before stop
			if (!election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Single candidate should be leader before stop");
			}

			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);

			// After stop, should no longer be leader
			if (election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Should not be leader after StopAsync");
			}
		}
		finally
		{
			(election as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that StartAsync can be called after StopAsync to restart participation.
	/// </summary>
	public virtual async Task StartAsync_AfterStop_ShouldRestartElection()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			// First start
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);
			if (!election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Should be leader after first StartAsync");
			}

			// Stop
			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			if (election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Should not be leader after StopAsync");
			}

			// Restart
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);
			if (!election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Should be leader after restart");
			}
		}
		finally
		{
			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election as IDisposable)?.Dispose();
		}
	}

	#endregion Lifecycle Tests

	#region Single-Candidate Leadership Tests

	/// <summary>
	/// Verifies that a single candidate becomes leader after starting.
	/// </summary>
	public virtual async Task StartAsync_SingleCandidate_ShouldBecomeLeader()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			if (!election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Single candidate should become leader after StartAsync");
			}
		}
		finally
		{
			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that IsLeader returns true for a single candidate that has started.
	/// </summary>
	public virtual async Task StartAsync_SingleCandidate_IsLeaderShouldBeTrue()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			// Before start, should not be leader
			if (election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Should not be leader before StartAsync");
			}

			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			// After start, should be leader
			if (!election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"IsLeader should be true after single candidate starts");
			}
		}
		finally
		{
			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that CurrentLeaderId matches CandidateId for a single candidate leader.
	/// </summary>
	public virtual async Task StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			if (election.CurrentLeaderId != election.CandidateId)
			{
				throw new TestFixtureAssertionException(
					$"CurrentLeaderId should match CandidateId. Expected: {election.CandidateId}, Actual: {election.CurrentLeaderId}");
			}
		}
		finally
		{
			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election as IDisposable)?.Dispose();
		}
	}

	#endregion Single-Candidate Leadership Tests

	#region Multi-Candidate Tests

	/// <summary>
	/// Verifies that only one candidate becomes leader when multiple candidates start.
	/// </summary>
	public virtual async Task MultipleCandidate_OnlyOneBecomesLeader()
	{
		var resourceName = GenerateResourceName();

		var election1 = CreateElection(resourceName, "candidate-1");
		var election2 = CreateElection(resourceName, "candidate-2");

		try
		{
			await election1.StartAsync(CancellationToken.None).ConfigureAwait(false);
			await election2.StartAsync(CancellationToken.None).ConfigureAwait(false);

			var leaders = new[] { election1, election2 }
				.Where(e => e.IsLeader)
				.ToList();

			if (leaders.Count != 1)
			{
				throw new TestFixtureAssertionException(
					$"Expected exactly 1 leader, found {leaders.Count}");
			}
		}
		finally
		{
			await election1.StopAsync(CancellationToken.None).ConfigureAwait(false);
			await election2.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election1 as IDisposable)?.Dispose();
			(election2 as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that all candidates see the same CurrentLeaderId.
	/// </summary>
	public virtual async Task MultipleCandidate_AllSeeSameCurrentLeaderId()
	{
		var resourceName = GenerateResourceName();

		var election1 = CreateElection(resourceName, "candidate-1");
		var election2 = CreateElection(resourceName, "candidate-2");

		try
		{
			await election1.StartAsync(CancellationToken.None).ConfigureAwait(false);
			await election2.StartAsync(CancellationToken.None).ConfigureAwait(false);

			var leaderId1 = election1.CurrentLeaderId;
			var leaderId2 = election2.CurrentLeaderId;

			if (leaderId1 != leaderId2)
			{
				throw new TestFixtureAssertionException(
					$"All candidates should see same CurrentLeaderId. election1: {leaderId1}, election2: {leaderId2}");
			}

			if (string.IsNullOrEmpty(leaderId1))
			{
				throw new TestFixtureAssertionException(
					"CurrentLeaderId should not be null or empty when candidates have started");
			}
		}
		finally
		{
			await election1.StopAsync(CancellationToken.None).ConfigureAwait(false);
			await election2.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election1 as IDisposable)?.Dispose();
			(election2 as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that the first candidate to start becomes leader (first-come-first-served).
	/// </summary>
	public virtual async Task MultipleCandidate_FirstToStartBecomesLeader()
	{
		var resourceName = GenerateResourceName();

		var election1 = CreateElection(resourceName, "candidate-1");
		var election2 = CreateElection(resourceName, "candidate-2");

		try
		{
			// Start election1 first
			await election1.StartAsync(CancellationToken.None).ConfigureAwait(false);

			// election1 should be leader
			if (!election1.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"First candidate to start should become leader");
			}

			// Start election2 second
			await election2.StartAsync(CancellationToken.None).ConfigureAwait(false);

			// election1 should still be leader
			if (!election1.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"First candidate should remain leader after second candidate starts");
			}

			// election2 should not be leader
			if (election2.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Second candidate should not be leader when first candidate is still active");
			}
		}
		finally
		{
			await election1.StopAsync(CancellationToken.None).ConfigureAwait(false);
			await election2.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election1 as IDisposable)?.Dispose();
			(election2 as IDisposable)?.Dispose();
		}
	}

	#endregion Multi-Candidate Tests

	#region Event Tests

	/// <summary>
	/// Verifies that BecameLeader event fires when a candidate becomes leader.
	/// </summary>
	public virtual async Task BecameLeader_ShouldFireWhenElected()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);
		var tcs = new TaskCompletionSource<LeaderElectionEventArgs>();

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
			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that LostLeadership event fires when a leader stops.
	/// </summary>
	public virtual async Task LostLeadership_ShouldFireWhenStopped()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);
		var tcs = new TaskCompletionSource<LeaderElectionEventArgs>();

		election.LostLeadership += (s, e) => tcs.TrySetResult(e);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			// Verify we're the leader
			if (!election.IsLeader)
			{
				throw new TestFixtureAssertionException(
					"Should be leader before testing LostLeadership");
			}

			// Stop to trigger LostLeadership
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
			(election as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that LeaderChanged event fires when leadership changes.
	/// </summary>
	public virtual async Task LeaderChanged_ShouldFireOnLeadershipChange()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);
		var tcs = new TaskCompletionSource<LeaderChangedEventArgs>();

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
			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);
			(election as IDisposable)?.Dispose();
		}
	}

	#endregion Event Tests

	#region Property Tests

	/// <summary>
	/// Verifies that CandidateId is unique per instance.
	/// </summary>
	public virtual async Task CandidateId_ShouldBeUniquePerInstance()
	{
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
			(election1 as IDisposable)?.Dispose();
			(election2 as IDisposable)?.Dispose();
			await Task.CompletedTask.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that CurrentLeaderId is null after leader stops.
	/// </summary>
	public virtual async Task CurrentLeaderId_AfterStop_ShouldBeNullOrEmpty()
	{
		var resourceName = GenerateResourceName();
		var election = CreateElection(resourceName, candidateId: null);

		try
		{
			await election.StartAsync(CancellationToken.None).ConfigureAwait(false);

			// Verify CurrentLeaderId is set
			if (string.IsNullOrEmpty(election.CurrentLeaderId))
			{
				throw new TestFixtureAssertionException(
					"CurrentLeaderId should be set after StartAsync");
			}

			await election.StopAsync(CancellationToken.None).ConfigureAwait(false);

			// After stop with single candidate, CurrentLeaderId should be null
			if (!string.IsNullOrEmpty(election.CurrentLeaderId))
			{
				throw new TestFixtureAssertionException(
					$"CurrentLeaderId should be null after single leader stops, but was: {election.CurrentLeaderId}");
			}
		}
		finally
		{
			(election as IDisposable)?.Dispose();
		}
	}

	#endregion Property Tests
}
