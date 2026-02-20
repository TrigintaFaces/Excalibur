// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- FakeItEasy fakes do not require disposal

using Excalibur.LeaderElection.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.LeaderElection.Tests.Health;

/// <summary>
/// Unit tests for <see cref="LeaderElectionHealthCheck"/>.
/// Validates Healthy, Degraded, and Unhealthy health check semantics per AD-535.4.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class LeaderElectionHealthCheckShould : UnitTestBase
{
	private readonly ILeaderElection _innerFake;
	private readonly LeaderElectionHealthCheck _sut;

	public LeaderElectionHealthCheckShould()
	{
		_innerFake = A.Fake<ILeaderElection>();
		A.CallTo(() => _innerFake.CandidateId).Returns("node-1");
		_sut = new LeaderElectionHealthCheck(_innerFake);
	}

	// --- Constructor ---

	[Fact]
	public void Throw_When_LeaderElection_Is_Null()
	{
		_ = Should.Throw<ArgumentNullException>(() => new LeaderElectionHealthCheck(null!));
	}

	// --- Healthy scenarios ---

	[Fact]
	public async Task Return_Healthy_When_IsLeader()
	{
		// Arrange
		A.CallTo(() => _innerFake.IsLeader).Returns(true);
		A.CallTo(() => _innerFake.CurrentLeaderId).Returns("node-1");

		// Act
		var result = await _sut.CheckHealthAsync(
			new HealthCheckContext { Registration = new HealthCheckRegistration("le", _sut, null, null) },
			CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("node-1");
		result.Data["IsLeader"].ShouldBe(true);
		result.Data["CandidateId"].ShouldBe("node-1");
	}

	[Fact]
	public async Task Return_Healthy_When_Follower_With_Known_Leader()
	{
		// Arrange
		A.CallTo(() => _innerFake.IsLeader).Returns(false);
		A.CallTo(() => _innerFake.CurrentLeaderId).Returns("node-2");

		// Act
		var result = await _sut.CheckHealthAsync(
			new HealthCheckContext { Registration = new HealthCheckRegistration("le", _sut, null, null) },
			CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("node-2");
		result.Data["IsLeader"].ShouldBe(false);
		result.Data["CurrentLeaderId"].ShouldBe("node-2");
	}

	// --- Degraded scenario ---

	[Fact]
	public async Task Return_Degraded_When_No_Leader_Detected()
	{
		// Arrange
		A.CallTo(() => _innerFake.IsLeader).Returns(false);
		A.CallTo(() => _innerFake.CurrentLeaderId).Returns(null as string);

		// Act
		var result = await _sut.CheckHealthAsync(
			new HealthCheckContext { Registration = new HealthCheckRegistration("le", _sut, null, null) },
			CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("No leader detected");
		result.Data["IsLeader"].ShouldBe(false);
		result.Data["CurrentLeaderId"].ShouldBe("(none)");
	}

	// --- Unhealthy scenario ---

	[Fact]
	public async Task Return_Unhealthy_When_Exception_Thrown()
	{
		// Arrange
		A.CallTo(() => _innerFake.IsLeader).Throws(new InvalidOperationException("Connection failed"));

		// Act
		var result = await _sut.CheckHealthAsync(
			new HealthCheckContext { Registration = new HealthCheckRegistration("le", _sut, null, null) },
			CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("failed");
		result.Exception.ShouldNotBeNull();
		result.Exception.ShouldBeOfType<InvalidOperationException>();
	}

	// --- Data dictionary ---

	[Fact]
	public async Task Include_CandidateId_In_Data()
	{
		// Arrange
		A.CallTo(() => _innerFake.IsLeader).Returns(true);
		A.CallTo(() => _innerFake.CurrentLeaderId).Returns("node-1");
		A.CallTo(() => _innerFake.CandidateId).Returns("my-candidate");

		// Act
		var result = await _sut.CheckHealthAsync(
			new HealthCheckContext { Registration = new HealthCheckRegistration("le", _sut, null, null) },
			CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("CandidateId");
		result.Data["CandidateId"].ShouldBe("my-candidate");
	}
}
