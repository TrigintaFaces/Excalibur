// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Coordination;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using StackExchange.Redis;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Depth tests for <see cref="CoordinationServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class CoordinationServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddJobCoordinationRedis_WithConnectionString_ThrowsOnNullServices()
	{
		IServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() =>
			services!.AddJobCoordinationRedis("localhost"));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithConnectionString_ThrowsOnNullConnectionString()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis((string)null!));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithConnectionString_ThrowsOnEmptyConnectionString()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis(""));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithMultiplexer_ThrowsOnNullServices()
	{
		IServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() =>
			services!.AddJobCoordinationRedis(A.Fake<IConnectionMultiplexer>()));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithMultiplexer_ThrowsOnNullMultiplexer()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddJobCoordinationRedis((IConnectionMultiplexer)null!));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithMultiplexer_RegistersCoordinatorInterfaces()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._))
			.Returns(A.Fake<IDatabase>());

		// Act
		services.AddJobCoordinationRedis(multiplexer);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobCoordinator));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobLockProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobRegistry));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobDistributor));
	}

	[Fact]
	public void AddJobCoordinationRedis_WithMultiplexer_ReturnsSameServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var multiplexer = A.Fake<IConnectionMultiplexer>();

		// Act
		var result = services.AddJobCoordinationRedis(multiplexer);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddJobCoordination_Generic_ThrowsOnNullServices()
	{
		IServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() =>
			services!.AddJobCoordination<CoordTestCustomCoordinator>());
	}

	[Fact]
	public void AddJobCoordination_Generic_RegistersCustomCoordinator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddJobCoordination<CoordTestCustomCoordinator>();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(CoordTestCustomCoordinator));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobCoordinator));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobLockProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobRegistry));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobDistributor));
	}

	[Fact]
	public void AddJobCoordination_Generic_ReturnsSameServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddJobCoordination<CoordTestCustomCoordinator>();

		// Assert
		result.ShouldBeSameAs(services);
	}
}

internal sealed class CoordTestCustomCoordinator : IJobCoordinator
{
	public Task<IDistributedJobLock?> TryAcquireLockAsync(string jobKey, TimeSpan lockDuration, CancellationToken cancellationToken) =>
		Task.FromResult<IDistributedJobLock?>(null);

	public Task RegisterInstanceAsync(string instanceId, JobInstanceInfo instanceInfo, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	public Task UnregisterInstanceAsync(string instanceId, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	public Task<IEnumerable<JobInstanceInfo>> GetActiveInstancesAsync(CancellationToken cancellationToken) =>
		Task.FromResult<IEnumerable<JobInstanceInfo>>(Array.Empty<JobInstanceInfo>());

	public Task<string?> DistributeJobAsync(string jobKey, object jobData, CancellationToken cancellationToken) =>
		Task.FromResult<string?>(null);

	public Task ReportJobCompletionAsync(string jobKey, string instanceId, bool success, object? result, CancellationToken cancellationToken) =>
		Task.CompletedTask;
}
