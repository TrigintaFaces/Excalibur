// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Tests.Shared.Conformance.LeaderElection;

/// <summary>
/// Base class for ILeaderElection conformance tests.
/// Implementations must provide concrete ILeaderElection instances for testing.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit verifies that leader election implementations
/// correctly implement the ILeaderElection interface contract, including:
/// </para>
/// <list type="bullet">
///   <item>Lease acquisition and leadership assignment</item>
///   <item>Lease renewal while holding leadership</item>
///   <item>Lease release and leadership relinquishment</item>
///   <item>Leader change event notification</item>
///   <item>Concurrent contention between multiple candidates</item>
/// </list>
/// <para>
/// To create conformance tests for your own ILeaderElection implementation:
/// <list type="number">
///   <item>Inherit from LeaderElectionConformanceTestBase</item>
///   <item>Override CreateElectionAsync() to create an instance of your ILeaderElection implementation</item>
///   <item>Override CreateCompetingElectionAsync() to create a second competing instance</item>
///   <item>Override CleanupAsync() to properly clean up resources between tests</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Conformance")]
[Trait("Component", "LeaderElection")]
public abstract class LeaderElectionConformanceTestBase : IAsyncLifetime
{
	/// <summary>
	/// The primary leader election instance under test.
	/// </summary>
	protected ILeaderElection Election { get; private set; } = null!;

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		Election = await CreateElectionAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);

		await Election.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Creates a new instance of the ILeaderElection implementation under test.
	/// </summary>
	/// <returns>A configured ILeaderElection instance.</returns>
	protected abstract Task<ILeaderElection> CreateElectionAsync();

	/// <summary>
	/// Creates a competing ILeaderElection instance for contention tests.
	/// Must target the same resource as the primary election.
	/// </summary>
	/// <returns>A configured competing ILeaderElection instance.</returns>
	protected abstract Task<ILeaderElection> CreateCompetingElectionAsync();

	/// <summary>
	/// Cleans up resources after each test.
	/// </summary>
	protected abstract Task CleanupAsync();

	/// <summary>
	/// Gets the maximum time to wait for leader election events.
	/// Override to increase for slower providers.
	/// </summary>
	protected virtual TimeSpan EventTimeout => TimeSpan.FromSeconds(10);

	#region Interface Implementation Tests

	[Fact]
	public void Election_ShouldImplementILeaderElection()
	{
		// Assert
		_ = Election.ShouldBeAssignableTo<ILeaderElection>();
	}

	[Fact]
	public void CandidateId_ShouldReturnNonEmptyString()
	{
		// Assert
		Election.CandidateId.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion Interface Implementation Tests

	#region AcquireLease Tests

	[Fact]
	public async Task StartAsync_AcquiresLeadership_WhenNoCompetition()
	{
		// Act
		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - Wait for leadership to be acquired
		await WaitUntilAsync(() => Election.IsLeader, EventTimeout).ConfigureAwait(false);
		Election.IsLeader.ShouldBeTrue("Should become leader when there is no competition");
		Election.CurrentLeaderId.ShouldBe(Election.CandidateId);
	}

	[Fact]
	public async Task StartAsync_RaisesBecameLeaderEvent()
	{
		// Arrange
		var becameLeaderRaised = new TaskCompletionSource<LeaderElectionEventArgs>();
		Election.BecameLeader += (_, args) => becameLeaderRaised.TrySetResult(args);

		// Act
		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		var args = await WaitForTaskAsync(becameLeaderRaised.Task, EventTimeout).ConfigureAwait(false);
		args.ShouldNotBeNull("BecameLeader event should have been raised");
		args.CandidateId.ShouldBe(Election.CandidateId);
	}

	[Fact]
	public async Task StartAsync_RaisesLeaderChangedEvent()
	{
		// Arrange
		var leaderChangedRaised = new TaskCompletionSource<LeaderChangedEventArgs>();
		Election.LeaderChanged += (_, args) => leaderChangedRaised.TrySetResult(args);

		// Act
		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		var args = await WaitForTaskAsync(leaderChangedRaised.Task, EventTimeout).ConfigureAwait(false);
		args.ShouldNotBeNull("LeaderChanged event should have been raised");
		args.NewLeaderId.ShouldBe(Election.CandidateId);
	}

	[Fact]
	public async Task StartAsync_IsIdempotent()
	{
		// Act - Start twice
		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await WaitUntilAsync(() => Election.IsLeader, EventTimeout).ConfigureAwait(false);

		// Second start should not throw
		await Should.NotThrowAsync(
			() => Election.StartAsync(CancellationToken.None)).ConfigureAwait(false);

		Election.IsLeader.ShouldBeTrue();
	}

	#endregion AcquireLease Tests

	#region RenewLease Tests

	[Fact]
	public async Task Leader_MaintainsLeadership_OverTime()
	{
		// Arrange
		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await WaitUntilAsync(() => Election.IsLeader, EventTimeout).ConfigureAwait(false);

		// Act - Wait for some lease renewal cycles
		await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);

		// Assert - Should still be leader after renewals
		Election.IsLeader.ShouldBeTrue("Should maintain leadership through lease renewals");
		Election.CurrentLeaderId.ShouldBe(Election.CandidateId);
	}

	[Fact]
	public async Task Leader_DoesNotRaiseLostLeadership_WhileRenewing()
	{
		// Arrange
		var lostLeadership = false;
		Election.LostLeadership += (_, _) => lostLeadership = true;

		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await WaitUntilAsync(() => Election.IsLeader, EventTimeout).ConfigureAwait(false);

		// Act - Wait through several renewal cycles
		await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);

		// Assert
		lostLeadership.ShouldBeFalse("Should not lose leadership while actively renewing");
	}

	#endregion RenewLease Tests

	#region ReleaseLease Tests

	[Fact]
	public async Task StopAsync_RelinquishesLeadership()
	{
		// Arrange
		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await WaitUntilAsync(() => Election.IsLeader, EventTimeout).ConfigureAwait(false);

		// Act
		await Election.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		Election.IsLeader.ShouldBeFalse("Should no longer be leader after stopping");
	}

	[Fact]
	public async Task StopAsync_RaisesLostLeadershipEvent()
	{
		// Arrange
		var lostLeadershipRaised = new TaskCompletionSource<LeaderElectionEventArgs>();
		Election.LostLeadership += (_, args) => lostLeadershipRaised.TrySetResult(args);

		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await WaitUntilAsync(() => Election.IsLeader, EventTimeout).ConfigureAwait(false);

		// Act
		await Election.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		var args = await WaitForTaskAsync(lostLeadershipRaised.Task, EventTimeout).ConfigureAwait(false);
		args.ShouldNotBeNull("LostLeadership event should have been raised");
		args.CandidateId.ShouldBe(Election.CandidateId);
	}

	[Fact]
	public async Task StopAsync_IsIdempotent()
	{
		// Arrange
		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await WaitUntilAsync(() => Election.IsLeader, EventTimeout).ConfigureAwait(false);

		// Act - Stop twice
		await Election.StopAsync(CancellationToken.None).ConfigureAwait(false);
		await Should.NotThrowAsync(
			() => Election.StopAsync(CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task StopAsync_BeforeStart_DoesNotThrow()
	{
		// Act & Assert - Stop without starting should not throw
		await Should.NotThrowAsync(
			() => Election.StopAsync(CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion ReleaseLease Tests

	#region LeaderChange Tests

	[Fact]
	public async Task LeaderChange_NewCandidateBecomesLeader_WhenCurrentLeaderStops()
	{
		// Arrange
		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await WaitUntilAsync(() => Election.IsLeader, EventTimeout).ConfigureAwait(false);

		await using var competitor = await CreateCompetingElectionAsync().ConfigureAwait(false);
		await competitor.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act - Primary leader stops
		await Election.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - Competitor should eventually become leader
		await WaitUntilAsync(() => competitor.IsLeader, EventTimeout).ConfigureAwait(false);
		competitor.IsLeader.ShouldBeTrue("Competitor should become leader after primary stops");
		competitor.CurrentLeaderId.ShouldBe(competitor.CandidateId);
	}

	[Fact]
	public async Task LeaderChange_CompetitorReceivesLeaderChangedEvent()
	{
		// Arrange
		await using var competitor = await CreateCompetingElectionAsync().ConfigureAwait(false);
		var leaderChangedEvents = new List<LeaderChangedEventArgs>();
		competitor.LeaderChanged += (_, args) => leaderChangedEvents.Add(args);

		await Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await WaitUntilAsync(() => Election.IsLeader, EventTimeout).ConfigureAwait(false);

		await competitor.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act - Primary leader stops
		await Election.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		await WaitUntilAsync(() => competitor.IsLeader, EventTimeout).ConfigureAwait(false);
		leaderChangedEvents.ShouldNotBeEmpty("LeaderChanged should have been raised on competitor");
	}

	#endregion LeaderChange Tests

	#region ConcurrentContention Tests

	[Fact]
	public async Task ConcurrentContention_OnlyOneLeader()
	{
		// Arrange
		const int competitorCount = 3;
		var competitors = new List<ILeaderElection> { Election };

		for (int i = 0; i < competitorCount; i++)
		{
			competitors.Add(await CreateCompetingElectionAsync().ConfigureAwait(false));
		}

		try
		{
			// Act - Start all concurrently
			var startTasks = competitors.Select(c =>
				c.StartAsync(CancellationToken.None));
			await Task.WhenAll(startTasks).ConfigureAwait(false);

			// Wait for leadership to stabilize
			await WaitUntilAsync(
				() => competitors.Any(c => c.IsLeader),
				EventTimeout).ConfigureAwait(false);

			// Assert - Exactly one leader
			var leaderCount = competitors.Count(c => c.IsLeader);
			leaderCount.ShouldBe(1, "Exactly one candidate should be leader");
		}
		finally
		{
			// Cleanup all competitors (except Election which is handled by DisposeAsync)
			foreach (var competitor in competitors.Skip(1))
			{
				await competitor.StopAsync(CancellationToken.None).ConfigureAwait(false);
				await competitor.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	[Fact]
	public async Task ConcurrentContention_AllCandidatesAgreeOnLeader()
	{
		// Arrange
		var competitors = new List<ILeaderElection> { Election };

		for (int i = 0; i < 2; i++)
		{
			competitors.Add(await CreateCompetingElectionAsync().ConfigureAwait(false));
		}

		try
		{
			// Act
			var startTasks = competitors.Select(c =>
				c.StartAsync(CancellationToken.None));
			await Task.WhenAll(startTasks).ConfigureAwait(false);

			await WaitUntilAsync(
				() => competitors.Any(c => c.IsLeader),
				EventTimeout).ConfigureAwait(false);

			// Wait for all to have a consistent view
			await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

			// Assert - All agree on who the leader is
			var leaderIds = competitors
				.Where(c => c.CurrentLeaderId is not null)
				.Select(c => c.CurrentLeaderId)
				.Distinct()
				.ToList();

			leaderIds.Count.ShouldBe(1, "All candidates should agree on the same leader");
		}
		finally
		{
			foreach (var competitor in competitors.Skip(1))
			{
				await competitor.StopAsync(CancellationToken.None).ConfigureAwait(false);
				await competitor.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	#endregion ConcurrentContention Tests

	#region Helper Methods

	/// <summary>
	/// Polls until a condition is true or timeout is reached.
	/// </summary>
	protected static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
	{
		var deadline = DateTimeOffset.UtcNow + timeout;
		while (!condition() && DateTimeOffset.UtcNow < deadline)
		{
			await Task.Delay(100).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Waits for a task to complete within the specified timeout.
	/// Returns the result or null if timed out.
	/// </summary>
	private static async Task<T?> WaitForTaskAsync<T>(Task<T> task, TimeSpan timeout) where T : class
	{
		var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
		return completed == task ? await task.ConfigureAwait(false) : null;
	}

	#endregion Helper Methods
}
