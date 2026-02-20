// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Coordination;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Unit tests for <see cref="CoordinationServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class CoordinationServiceCollectionExtensionsShould
{
	// --- AddJobCoordinationRedis (connection string) ---

	[Fact]
	public void AddJobCoordinationRedisThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddJobCoordinationRedis("localhost:6379"));
	}

	[Fact]
	public void AddJobCoordinationRedisThrowWhenConnectionStringIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis((string)null!));
	}

	[Fact]
	public void AddJobCoordinationRedisThrowWhenConnectionStringIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis(""));
	}

	[Fact]
	public void AddJobCoordinationRedisThrowWhenConnectionStringIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddJobCoordinationRedis("   "));
	}

	// --- AddJobCoordinationRedis (connection multiplexer) ---

	[Fact]
	public void AddJobCoordinationRedisWithMultiplexerThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;
		var multiplexer = FakeItEasy.A.Fake<StackExchange.Redis.IConnectionMultiplexer>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddJobCoordinationRedis(multiplexer));
	}

	[Fact]
	public void AddJobCoordinationRedisWithMultiplexerThrowWhenMultiplexerIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddJobCoordinationRedis((StackExchange.Redis.IConnectionMultiplexer)null!));
	}

	// --- AddJobCoordination<T> ---

	[Fact]
	public void AddJobCoordinationThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddJobCoordination<FakeJobCoordinator>());
	}

	[Fact]
	public void AddJobCoordinationRegisterCoordinatorAndSubInterfaces()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddJobCoordination<FakeJobCoordinator>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(FakeJobCoordinator) &&
			sd.Lifetime == ServiceLifetime.Singleton);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IJobCoordinator) &&
			sd.Lifetime == ServiceLifetime.Singleton);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IJobLockProvider) &&
			sd.Lifetime == ServiceLifetime.Singleton);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IJobRegistry) &&
			sd.Lifetime == ServiceLifetime.Singleton);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IJobDistributor) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddJobCoordinationReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddJobCoordination<FakeJobCoordinator>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	// --- Test helpers ---

	private sealed class FakeJobCoordinator : IJobCoordinator
	{
		public Task<IDistributedJobLock?> TryAcquireLockAsync(string jobKey, TimeSpan lockDuration, CancellationToken cancellationToken)
			=> Task.FromResult<IDistributedJobLock?>(null);

		public Task RegisterInstanceAsync(string instanceId, JobInstanceInfo instanceInfo, CancellationToken cancellationToken)
			=> Task.CompletedTask;

		public Task UnregisterInstanceAsync(string instanceId, CancellationToken cancellationToken)
			=> Task.CompletedTask;

		public Task<IEnumerable<JobInstanceInfo>> GetActiveInstancesAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IEnumerable<JobInstanceInfo>>([]);

		public Task<string?> DistributeJobAsync(string jobKey, object jobData, CancellationToken cancellationToken)
			=> Task.FromResult<string?>(null);

		public Task ReportJobCompletionAsync(string jobKey, string instanceId, bool success, object? result, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}
}
