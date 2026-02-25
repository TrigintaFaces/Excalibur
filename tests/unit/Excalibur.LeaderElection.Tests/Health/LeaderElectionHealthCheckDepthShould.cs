// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.LeaderElection.Tests.Health;

/// <summary>
/// Depth coverage tests for <see cref="LeaderElectionHealthCheck"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LeaderElectionHealthCheckDepthShould
{
	[Fact]
	public void ThrowWhenLeaderElectionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new LeaderElectionHealthCheck(null!));
	}

	[Fact]
	public async Task ReturnHealthyWhenThisInstanceIsLeader()
	{
		// Arrange
		var le = A.Fake<ILeaderElection>();
		A.CallTo(() => le.IsLeader).Returns(true);
		A.CallTo(() => le.CurrentLeaderId).Returns("node-1");
		A.CallTo(() => le.CandidateId).Returns("node-1");

		var sut = new LeaderElectionHealthCheck(le);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("node-1");
		result.Description.ShouldContain("is the leader");
		result.Data["IsLeader"].ShouldBe(true);
		result.Data["CandidateId"].ShouldBe("node-1");
	}

	[Fact]
	public async Task ReturnHealthyWhenAnotherLeaderObserved()
	{
		// Arrange
		var le = A.Fake<ILeaderElection>();
		A.CallTo(() => le.IsLeader).Returns(false);
		A.CallTo(() => le.CurrentLeaderId).Returns("other-node");
		A.CallTo(() => le.CandidateId).Returns("my-node");

		var sut = new LeaderElectionHealthCheck(le);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("other-node");
		result.Data["IsLeader"].ShouldBe(false);
		result.Data["CurrentLeaderId"].ShouldBe("other-node");
	}

	[Fact]
	public async Task ReturnDegradedWhenNoLeaderDetected()
	{
		// Arrange
		var le = A.Fake<ILeaderElection>();
		A.CallTo(() => le.IsLeader).Returns(false);
		A.CallTo(() => le.CurrentLeaderId).Returns((string?)null);
		A.CallTo(() => le.CandidateId).Returns("my-node");

		var sut = new LeaderElectionHealthCheck(le);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldBe("No leader detected");
		result.Data["CurrentLeaderId"].ShouldBe("(none)");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenExceptionOccurs()
	{
		// Arrange
		var le = A.Fake<ILeaderElection>();
		A.CallTo(() => le.IsLeader).Throws(new InvalidOperationException("connection failed"));

		var sut = new LeaderElectionHealthCheck(le);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldBe("Leader election health check failed");
		result.Exception.ShouldBeOfType<InvalidOperationException>();
	}
}
