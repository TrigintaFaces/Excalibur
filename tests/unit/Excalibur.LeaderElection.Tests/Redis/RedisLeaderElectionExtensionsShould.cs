// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.Redis;

/// <summary>
/// Unit tests for <see cref="RedisLeaderElectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RedisLeaderElectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddRedisLeaderElection_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddRedisLeaderElection("test:leader");

		// Assert - Check service is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(RedisLeaderElection) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElection) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddRedisLeaderElection_WithConfigure_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddRedisLeaderElection("test:leader", options =>
		{
			options.LeaseDuration = TimeSpan.FromSeconds(30);
		});

		// Assert - Check service is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(RedisLeaderElection) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddRedisLeaderElection_WithConfigure_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddRedisLeaderElection("test:leader", options =>
		{
			options.LeaseDuration = TimeSpan.FromSeconds(30);
		});

		// Assert - Check options configuration is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<LeaderElectionOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddRedisLeaderElection_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddRedisLeaderElection("test:leader", _ => { }));
	}

	[Fact]
	public void AddRedisLeaderElection_ThrowsOnNullLockKey()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddRedisLeaderElection(null!, _ => { }));
	}

	[Fact]
	public void AddRedisLeaderElection_ThrowsOnEmptyLockKey()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddRedisLeaderElection("", _ => { }));
	}

	[Fact]
	public void AddRedisLeaderElection_ThrowsOnWhitespaceLockKey()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddRedisLeaderElection("   ", _ => { }));
	}

	[Fact]
	public void AddRedisLeaderElection_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddRedisLeaderElection("test:leader", null!));
	}

	[Fact]
	public async Task AddRedisLeaderElection_ResolvesAsTelemetryWrapper()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IConnectionMultiplexer>());
		_ = services.AddRedisLeaderElection("test:leader");

		// Act
		await using var sp = services.BuildServiceProvider();
		var le = sp.GetRequiredService<ILeaderElection>();

		// Assert — DI should auto-wrap with TelemetryLeaderElection (AD-536.1)
		le.ShouldBeOfType<LeaderElection.Diagnostics.TelemetryLeaderElection>();
	}

	[Fact]
	public async Task AddRedisLeaderElection_InnerIsRedisImplementation()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IConnectionMultiplexer>());
		_ = services.AddRedisLeaderElection("test:leader");

		// Act
		await using var sp = services.BuildServiceProvider();
		var concrete = sp.GetRequiredService<RedisLeaderElection>();

		// Assert — concrete RedisLeaderElection should also be resolvable
		concrete.ShouldNotBeNull();
		concrete.ShouldBeOfType<RedisLeaderElection>();
	}

	[Fact]
	public void AddRedisLeaderElection_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddRedisLeaderElection("test:leader");

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddRedisLeaderElectionFactory_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddRedisLeaderElectionFactory();

		// Assert - Check factory is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddRedisLeaderElectionFactory_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddRedisLeaderElectionFactory());
	}

	[Fact]
	public void AddRedisLeaderElectionFactory_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddRedisLeaderElectionFactory();

		// Assert
		result.ShouldBeSameAs(services);
	}
}
