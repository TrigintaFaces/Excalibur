// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Contract tests for <see cref="ILeaderElection"/> verifying the interface
/// is correctly consumable through FakeItEasy fakes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ILeaderElectionContractShould : UnitTestBase
{
	[Fact]
	public void HaveCandidateIdProperty()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();
		A.CallTo(() => election.CandidateId).Returns("candidate-1");

		// Assert
		election.CandidateId.ShouldBe("candidate-1");
	}

	[Fact]
	public void HaveIsLeaderProperty()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();
		A.CallTo(() => election.IsLeader).Returns(true);

		// Assert
		election.IsLeader.ShouldBeTrue();
	}

	[Fact]
	public void HaveCurrentLeaderIdProperty()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();
		A.CallTo(() => election.CurrentLeaderId).Returns("leader-1");

		// Assert
		election.CurrentLeaderId.ShouldBe("leader-1");
	}

	[Fact]
	public void HaveNullCurrentLeaderId_WhenNoLeader()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();
		A.CallTo(() => election.CurrentLeaderId).Returns(null);

		// Assert
		election.CurrentLeaderId.ShouldBeNull();
	}

	[Fact]
	public async Task StartAsync_ShouldBeCallable()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();

		// Act
		await election.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => election.StartAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StopAsync_ShouldBeCallable()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();

		// Act
		await election.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => election.StopAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsync_ShouldBeCallable()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();

		// Act
		await election.DisposeAsync();

		// Assert
		A.CallTo(() => election.DisposeAsync())
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void BecameLeaderEvent_ShouldBeSubscribable()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();
		var eventRaised = false;

		// Act
		election.BecameLeader += (_, _) => eventRaised = true;

		// Assert - no exception thrown during subscription
		eventRaised.ShouldBeFalse();
	}

	[Fact]
	public void LostLeadershipEvent_ShouldBeSubscribable()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();
		var eventRaised = false;

		// Act
		election.LostLeadership += (_, _) => eventRaised = true;

		// Assert - no exception thrown during subscription
		eventRaised.ShouldBeFalse();
	}

	[Fact]
	public void LeaderChangedEvent_ShouldBeSubscribable()
	{
		// Arrange
		var election = A.Fake<ILeaderElection>();
		var eventRaised = false;

		// Act
		election.LeaderChanged += (_, _) => eventRaised = true;

		// Assert - no exception thrown during subscription
		eventRaised.ShouldBeFalse();
	}
}
