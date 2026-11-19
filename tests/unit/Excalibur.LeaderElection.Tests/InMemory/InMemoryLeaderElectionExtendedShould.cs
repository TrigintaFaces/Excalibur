// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Infrastructure;

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
		// Acquisition and loss notifications are QUEUED transition history, not a synchronous callback
		// inside StartAsync: ARCHITECTURE.md records that retirement "queues the loss notification;
		// consumer callbacks may delay delivery", and the provider enqueues the raise from under the
		// candidate gate rather than invoking a consumer handler while holding it. So the arrival is
		// awaited, never assumed to have happened by the time StartAsync returns -- asserting
		// immediately passed on an idle machine and failed whenever the suite ran under load.
		(await WaitHelpers.WaitUntilAsync(
			() => becameLeader && leaderChanged,
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue(
			"acquisition should deliver both the became-leader and leader-changed notifications");
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
		using var callbackBarrier = new InMemoryCallbackBarrier(le, relinquished: true);
        await le.StopAsync(CancellationToken.None);
        await callbackBarrier.Completion.WaitAsync(TimeSpan.FromSeconds(5));

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
		le.BecameLeader += (_, _) => Interlocked.Increment(ref eventCount);

		// Act
		await le.StartAsync(CancellationToken.None);
		await le.StartAsync(CancellationToken.None);

		// Assert - the second start must not re-notify.
		// Two arms, because delivery is queued and an immediate count reads whatever has arrived so
		// far: first wait for the one notification to ARRIVE (a bare count of 0 would otherwise
		// satisfy "not more than one" while proving nothing), then prove a second never follows it.
		(await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref eventCount) >= 1,
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue(
			"the first acquisition should deliver exactly one became-leader notification");
		(await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref eventCount) > 1,
			TimeSpan.FromMilliseconds(250),
			TimeSpan.FromMilliseconds(10),
			CancellationToken.None).ConfigureAwait(false)).ShouldBeFalse(
			"a second StartAsync on an instance that already leads must not re-notify");
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
		using var callbackBarrier = new InMemoryCallbackBarrier(le, relinquished: true);
        await le.UpdateHealthAsync(isHealthy: false, metadata: null, CancellationToken.None);
        await callbackBarrier.Completion.WaitAsync(TimeSpan.FromSeconds(5));

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
		await le.UpdateHealthAsync(true, new Dictionary<string, string> { ["key"] = "value" }, CancellationToken.None);

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
		await le.UpdateHealthAsync(true, null, CancellationToken.None);
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
