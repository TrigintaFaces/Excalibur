// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Health;
using Excalibur.LeaderElection.InMemory;

namespace Excalibur.LeaderElection.Tests.Aot;

/// <summary>
/// Unit tests for <see cref="LeaderElectionAotHelpers"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class LeaderElectionAotHelpersShould
{
	// --- AddLeaderElection<T> ---

	[Fact]
	public void AddLeaderElectionThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddLeaderElection<InMemoryLeaderElection>());
	}

	[Fact]
	public void AddLeaderElectionRegisterSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElection<InMemoryLeaderElection>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ILeaderElection) &&
			sd.ImplementationType == typeof(InMemoryLeaderElection) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddLeaderElectionReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddLeaderElection<InMemoryLeaderElection>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	// --- AddLeaderElectionFactory<T> ---

	[Fact]
	public void AddLeaderElectionFactoryThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddLeaderElectionFactory<InMemoryLeaderElectionFactory>());
	}

	[Fact]
	public void AddLeaderElectionFactoryRegisterSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElectionFactory<InMemoryLeaderElectionFactory>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.ImplementationType == typeof(InMemoryLeaderElectionFactory) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddLeaderElectionFactoryReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddLeaderElectionFactory<InMemoryLeaderElectionFactory>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	// --- AddLeaderElectionTelemetry ---

	[Fact]
	public void AddLeaderElectionTelemetryThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddLeaderElectionTelemetry());
	}

	[Fact]
	public void AddLeaderElectionTelemetryRegisterTelemetryFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElectionTelemetry();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TelemetryLeaderElectionFactory) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddLeaderElectionTelemetryReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddLeaderElectionTelemetry();

		// Assert
		result.ShouldBeSameAs(services);
	}

	// --- AddLeaderElectionHealthCheck ---

	[Fact]
	public void AddLeaderElectionHealthCheckThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddLeaderElectionHealthCheck());
	}

	[Fact]
	public void AddLeaderElectionHealthCheckRegisterHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElectionHealthCheck();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(LeaderElectionHealthCheck) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddLeaderElectionHealthCheckReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddLeaderElectionHealthCheck();

		// Assert
		result.ShouldBeSameAs(services);
	}
}
