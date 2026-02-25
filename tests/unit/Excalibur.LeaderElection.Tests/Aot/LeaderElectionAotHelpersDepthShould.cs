// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Health;

namespace Excalibur.LeaderElection.Tests.Aot;

/// <summary>
/// Depth coverage tests for <see cref="LeaderElectionAotHelpers"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LeaderElectionAotHelpersDepthShould
{
	[Fact]
	public void ThrowWhenServicesIsNullForAddLeaderElection()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			LeaderElectionAotHelpers.AddLeaderElection<InMemoryLeaderElection>(null!));
	}

	[Fact]
	public void RegisterLeaderElectionImplementation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElection<InMemoryLeaderElection>();

		// Assert
		services.ShouldContain(d =>
			d.ServiceType == typeof(ILeaderElection) &&
			d.ImplementationType == typeof(InMemoryLeaderElection));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForAddLeaderElectionFactory()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			LeaderElectionAotHelpers.AddLeaderElectionFactory<InMemoryLeaderElectionFactory>(null!));
	}

	[Fact]
	public void RegisterLeaderElectionFactoryImplementation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElectionFactory<InMemoryLeaderElectionFactory>();

		// Assert
		services.ShouldContain(d =>
			d.ServiceType == typeof(ILeaderElectionFactory) &&
			d.ImplementationType == typeof(InMemoryLeaderElectionFactory));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForAddLeaderElectionTelemetry()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			LeaderElectionAotHelpers.AddLeaderElectionTelemetry(null!));
	}

	[Fact]
	public void RegisterTelemetryLeaderElectionFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElectionTelemetry();

		// Assert
		services.ShouldContain(d =>
			d.ServiceType == typeof(TelemetryLeaderElectionFactory));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForAddLeaderElectionHealthCheck()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			LeaderElectionAotHelpers.AddLeaderElectionHealthCheck(null!));
	}

	[Fact]
	public void RegisterLeaderElectionHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElectionHealthCheck();

		// Assert
		services.ShouldContain(d =>
			d.ServiceType == typeof(LeaderElectionHealthCheck));
	}
}
