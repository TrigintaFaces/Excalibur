// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryLeaderElectionServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryLeaderElectionServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddInMemoryLeaderElection_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryLeaderElection();

		// Assert - Check keyed service is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.IsKeyedService &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddInMemoryLeaderElection_WithConfigure_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryLeaderElection(options =>
		{
			options.InstanceId = "test-instance";
			options.LeaseDuration = TimeSpan.FromSeconds(30);
		});

		// Assert - Check keyed service is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.IsKeyedService &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddInMemoryLeaderElection_WithConfigure_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryLeaderElection(options =>
		{
			options.InstanceId = "test-instance";
		});

		// Assert - Check options configuration is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<LeaderElectionOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddInMemoryLeaderElection_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryLeaderElection(_ => { }));
	}

	[Fact]
	public void AddInMemoryLeaderElection_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryLeaderElection(null!));
	}

	[Fact]
	public void AddInMemoryLeaderElection_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddInMemoryLeaderElection();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddInMemoryLeaderElection_WithConfigure_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddInMemoryLeaderElection(options =>
		{
			options.InstanceId = "test";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddInMemoryLeaderElection_DoesNotAddDuplicateFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryLeaderElection();
		_ = services.AddInMemoryLeaderElection();

		// Assert - The "default" key uses TryAddKeyedSingleton so only one "default" registration
		// but the "inmemory" key uses AddKeyedSingleton so it adds each time.
		// Check "default" keyed registrations are deduplicated.
		services.Count(static sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.IsKeyedService &&
			sd.ServiceKey is string key &&
			key == "default").ShouldBe(1);
	}

	[Fact]
	public void AddInMemoryLeaderElection_FactoryCanResolve_WhenAllDependenciesRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryLeaderElection(options =>
		{
			options.InstanceId = "test-instance";
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var factory = provider.GetKeyedService<ILeaderElectionFactory>("default");

		// Assert
		_ = factory.ShouldNotBeNull();
		_ = factory.ShouldBeOfType<InMemoryLeaderElectionFactory>();
	}

	[Fact]
	public void AddInMemoryLeaderElection_FactoryCreatesWorkingElection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryLeaderElection(options =>
		{
			options.InstanceId = "integration-test-instance";
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredKeyedService<ILeaderElectionFactory>("default");
		var election = factory.CreateElection($"test-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();
		election.CandidateId.ShouldBe("integration-test-instance");

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}
}
