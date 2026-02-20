// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// Extended unit tests for <see cref="InMemoryLeaderElection"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class InMemoryLeaderElectionExtendedShould
{
	[Fact]
	public async Task AcquireLeadershipOnStart()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInstance("resource-1", "candidate-1", sharedState);

		// Act
		await le.StartAsync(CancellationToken.None);

		// Assert
		le.IsLeader.ShouldBeTrue();
		le.CurrentLeaderId.ShouldBe("candidate-1");
	}

	[Fact]
	public async Task RaiseEventsOnLeadershipAcquisition()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInstance("resource-1", "candidate-1", sharedState);

		var becameLeader = false;
		var leaderChanged = false;
		le.BecameLeader += (_, _) => becameLeader = true;
		le.LeaderChanged += (_, _) => leaderChanged = true;

		// Act
		await le.StartAsync(CancellationToken.None);

		// Assert
		becameLeader.ShouldBeTrue();
		leaderChanged.ShouldBeTrue();
	}

	[Fact]
	public async Task NotAcquireLeadershipWhenAnotherCandidateIsLeader()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var leader = CreateInstance("resource-1", "candidate-1", sharedState);
		await using var follower = CreateInstance("resource-1", "candidate-2", sharedState);

		// Act
		await leader.StartAsync(CancellationToken.None);
		await follower.StartAsync(CancellationToken.None);

		// Assert
		leader.IsLeader.ShouldBeTrue();
		follower.IsLeader.ShouldBeFalse();
		follower.CurrentLeaderId.ShouldBe("candidate-1");
	}

	[Fact]
	public async Task ReleaseLeadershipOnStop()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInstance("resource-1", "candidate-1", sharedState);
		await le.StartAsync(CancellationToken.None);

		var lostLeadership = false;
		le.LostLeadership += (_, _) => lostLeadership = true;

		// Act
		await le.StopAsync(CancellationToken.None);

		// Assert
		le.IsLeader.ShouldBeFalse();
		lostLeadership.ShouldBeTrue();
	}

	[Fact]
	public async Task NotStopWhenNotRunning()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInstance("resource-1", "candidate-1", sharedState);

		// Act & Assert - should not throw
		await le.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ThrowOnStartAfterDispose()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		var le = CreateInstance("resource-1", "candidate-1", sharedState);
		await le.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			le.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task NotStartTwice()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInstance("resource-1", "candidate-1", sharedState);

		var eventCount = 0;
		le.BecameLeader += (_, _) => eventCount++;

		// Act
		await le.StartAsync(CancellationToken.None);
		await le.StartAsync(CancellationToken.None);

		// Assert - event should only fire once
		eventCount.ShouldBe(1);
	}

#pragma warning disable CA2012 // Use ValueTasks correctly - test needs to verify dispose pattern
	[Fact]
	public async Task HandleDisposeAsync()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		var le = CreateInstance("resource-1", "candidate-1", sharedState);
		await le.StartAsync(CancellationToken.None);

		// Act
		await le.DisposeAsync();
		await le.DisposeAsync(); // Should be safe to call twice

		// Assert
		le.IsLeader.ShouldBeFalse();
	}

	[Fact]
	public async Task HandleDispose()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		var le = CreateInstance("resource-1", "candidate-1", sharedState);
		await le.StartAsync(CancellationToken.None);

		// Act
		le.Dispose();
		le.Dispose(); // Should be safe to call twice

		// Assert
		le.IsLeader.ShouldBeFalse();
	}
#pragma warning restore CA2012

	[Fact]
	public async Task StepDownWhenUnhealthyAndConfigured()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		var options = new LeaderElectionOptions
		{
			InstanceId = "candidate-1",
			StepDownWhenUnhealthy = true,
		};
		await using var le = new InMemoryLeaderElection(
			"resource-1",
			Options.Create(options),
			NullLogger<InMemoryLeaderElection>.Instance,
			sharedState);

		await le.StartAsync(CancellationToken.None);
		le.IsLeader.ShouldBeTrue();

		var lostLeadership = false;
		le.LostLeadership += (_, _) => lostLeadership = true;

		// Act
		await le.UpdateHealthAsync(isHealthy: false, metadata: null);

		// Assert
		le.IsLeader.ShouldBeFalse();
		lostLeadership.ShouldBeTrue();
	}

	[Fact]
	public async Task UpdateHealthWithMetadata()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInstance("resource-1", "candidate-1", sharedState);
		await le.StartAsync(CancellationToken.None);

		// Act
		await le.UpdateHealthAsync(true, new Dictionary<string, string> { ["key"] = "value" });

		// Assert
		var healthList = await le.GetCandidateHealthAsync(CancellationToken.None);
		healthList.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task ReturnEmptyHealthWhenResourceNotTracked()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInstance("unknown-resource", "candidate-1", sharedState);

		// Act
		var healthList = await le.GetCandidateHealthAsync(CancellationToken.None);

		// Assert
		healthList.ShouldNotBeNull();
	}

	[Fact]
	public async Task NotUpdateHealthWhenNotRunning()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInstance("resource-1", "candidate-1", sharedState);

		// Act & Assert - should not throw
		await le.UpdateHealthAsync(true, null);
	}

	private static InMemoryLeaderElection CreateInstance(
		string resourceName,
		string candidateId,
		InMemoryLeaderElectionSharedState? sharedState = null)
	{
		var options = new LeaderElectionOptions { InstanceId = candidateId };
		return new InMemoryLeaderElection(
			resourceName,
			Options.Create(options),
			NullLogger<InMemoryLeaderElection>.Instance,
			sharedState);
	}
}
