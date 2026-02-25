// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Redis;

/// <summary>
/// Integration tests for <see cref="RedisPersistenceProvider"/> using TestContainers.
/// Tests real Redis database operations for persistence management.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 177 - Provider Testing Epic Phase 3.
/// bd-mj93p: Redis Persistence Tests (5 tests).
/// </para>
/// <para>
/// These tests verify the RedisPersistenceProvider implementation against a real Redis
/// instance using TestContainers. Tests cover connection, metrics, database operations,
/// and lifecycle management.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Redis)]
[Trait("Component", "Persistence")]
[Trait("Provider", "Redis")]
public sealed class RedisPersistenceProviderIntegrationShould : IntegrationTestBase
{
	private readonly RedisContainerFixture _redisFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisPersistenceProviderIntegrationShould"/> class.
	/// </summary>
	/// <param name="redisFixture">The Redis container fixture.</param>
	public RedisPersistenceProviderIntegrationShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	/// <summary>
	/// Tests that the provider can connect to Redis.
	/// </summary>
	[Fact]
	public async Task ConnectToRedis()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();

		// Act
		var result = await provider.TestConnectionAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		result.ShouldBeTrue();
		provider.IsAvailable.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that the provider reports accurate metrics.
	/// </summary>
	[Fact]
	public async Task RetrieveMetrics()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();

		// Act
		var metrics = await provider.GetMetricsAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = metrics.ShouldNotBeNull();
		metrics.ShouldContainKey("Provider");
		metrics["Provider"].ShouldBe("Redis");
		metrics.ShouldContainKey("IsConnected");
		((bool)metrics["IsConnected"]).ShouldBeTrue();
		metrics.ShouldContainKey("IsAvailable");
		((bool)metrics["IsAvailable"]).ShouldBeTrue();
	}

	/// <summary>
	/// Tests that the provider can get and set values via the database.
	/// </summary>
	[Fact]
	public async Task PerformBasicDatabaseOperations()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var database = provider.GetDatabase();
		var key = $"test:{Guid.NewGuid()}";
		var value = "test-value";

		// Act
		_ = await database.StringSetAsync(key, value).ConfigureAwait(true);
		var retrieved = await database.StringGetAsync(key).ConfigureAwait(true);

		// Assert
		retrieved.HasValue.ShouldBeTrue();
		retrieved.ToString().ShouldBe(value);

		// Cleanup
		_ = await database.KeyDeleteAsync(key).ConfigureAwait(true);
	}

	/// <summary>
	/// Tests that pool stats are available.
	/// </summary>
	[Fact]
	public async Task RetrieveConnectionPoolStats()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();

		// Act
		var stats = await provider.GetConnectionPoolStatsAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = stats.ShouldNotBeNull();
		stats.ShouldContainKey("IsConnected");
		((bool)stats["IsConnected"]).ShouldBeTrue();
		stats.ShouldContainKey("EndpointCount");
		((int)stats["EndpointCount"]).ShouldBeGreaterThan(0);
	}

	/// <summary>
	/// Tests that the provider disposes correctly.
	/// </summary>
	[Fact]
	public void DisposeCorrectly()
	{
		// Arrange
		var provider = CreatePersistenceProvider();

		// Act
		provider.Dispose();

		// Assert
		provider.IsAvailable.ShouldBeFalse();
	}

	private RedisPersistenceProvider CreatePersistenceProvider()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisProviderOptions
		{
			ConnectionString = _redisFixture.ConnectionString,
			Name = "test-redis",
			DatabaseId = 0,
			ConnectTimeout = 5,
			SyncTimeout = 5,
			AsyncTimeout = 5,
			AbortOnConnectFail = false
		});
		var logger = NullLogger<RedisPersistenceProvider>.Instance;
		return new RedisPersistenceProvider(options, logger);
	}
}
