// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.LeaderElection.Tests.Health;

/// <summary>
/// Unit tests for <see cref="LeaderElectionHealthCheckExtensions"/>.
/// Validates DI registration of the leader election health check.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class LeaderElectionHealthCheckExtensionsShould : UnitTestBase
{
	[Fact]
	public void Register_HealthCheck_With_Default_Name()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var innerFake = A.Fake<ILeaderElection>();
		A.CallTo(() => innerFake.CandidateId).Returns("node-1");
		A.CallTo(() => innerFake.IsLeader).Returns(false);
		services.AddSingleton(innerFake);

		// Act
		services.AddHealthChecks().AddLeaderElectionHealthCheck();

		// Assert — verify the health check registration is present
		var sp = services.BuildServiceProvider();
		var healthCheckService = sp.GetService<HealthCheckService>();
		healthCheckService.ShouldNotBeNull();
	}

	[Fact]
	public void Register_HealthCheck_With_Custom_Name()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var innerFake = A.Fake<ILeaderElection>();
		A.CallTo(() => innerFake.CandidateId).Returns("node-1");
		A.CallTo(() => innerFake.IsLeader).Returns(false);
		services.AddSingleton(innerFake);

		// Act
		services.AddHealthChecks().AddLeaderElectionHealthCheck(name: "my-le-check");

		// Assert
		var sp = services.BuildServiceProvider();
		var healthCheckService = sp.GetService<HealthCheckService>();
		healthCheckService.ShouldNotBeNull();
	}

	[Fact]
	public void Throw_When_Builder_Is_Null()
	{
		// Arrange
		IHealthChecksBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.AddLeaderElectionHealthCheck());
	}

	[Fact]
	public void Return_Builder_For_Chaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		var result = builder.AddLeaderElectionHealthCheck();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public async Task Resolve_And_Execute_HealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var innerFake = A.Fake<ILeaderElection>();
		A.CallTo(() => innerFake.CandidateId).Returns("node-1");
		A.CallTo(() => innerFake.IsLeader).Returns(true);
		A.CallTo(() => innerFake.CurrentLeaderId).Returns("node-1");
		services.AddSingleton(innerFake);
		services.AddHealthChecks().AddLeaderElectionHealthCheck();

		var sp = services.BuildServiceProvider();
		var healthCheckService = sp.GetRequiredService<HealthCheckService>();

		// Act
		var report = await healthCheckService.CheckHealthAsync(CancellationToken.None);

		// Assert
		report.Status.ShouldBe(HealthStatus.Healthy);
		report.Entries.ShouldContainKey("leader-election");
		report.Entries["leader-election"].Status.ShouldBe(HealthStatus.Healthy);
	}
}
