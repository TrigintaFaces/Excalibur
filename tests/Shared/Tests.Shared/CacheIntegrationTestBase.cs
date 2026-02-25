// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Tests.Shared;

/// <summary>
/// Base class for cache integration tests using TestContainers.
/// </summary>
public abstract class CacheIntegrationTestBase : IntegrationTestBase, IClassFixture<RedisContainerFixture>
{
	protected CacheIntegrationTestBase(RedisContainerFixture fixture)
	{
		Fixture = fixture;
	}

	protected RedisContainerFixture Fixture { get; }

	/// <summary>
	/// Gets the Redis connection string.
	/// </summary>
	protected string ConnectionString => Fixture.ConnectionString;

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await FlushCacheAsync();
	}

	/// <summary>
	/// Initialize services and build provider. Call in derived class constructor after setting up services.
	/// </summary>
	protected void InitializeServices()
	{
		ConfigureServices(Services);
		BuildServiceProvider();
	}

	/// <summary>
	/// Configure cache-specific services.
	/// </summary>
	protected virtual void ConfigureServices(IServiceCollection services)
	{
		// Override to register IDistributedCache, Redis clients, etc.
	}

	/// <summary>
	/// Flush cache before tests.
	/// </summary>
	protected virtual Task FlushCacheAsync() => Task.CompletedTask;
}

/// <summary>
/// Search/Index integration test base class for Elasticsearch.
/// </summary>
public abstract class SearchIntegrationTestBase : IntegrationTestBase, IClassFixture<ElasticsearchContainerFixture>
{
	protected SearchIntegrationTestBase(ElasticsearchContainerFixture fixture)
	{
		Fixture = fixture;
	}

	protected ElasticsearchContainerFixture Fixture { get; }

	/// <summary>
	/// Gets the Elasticsearch connection string.
	/// </summary>
	protected string ConnectionString => Fixture.ConnectionString;

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await SetupIndicesAsync();
	}

	public override async Task DisposeAsync()
	{
		await CleanupIndicesAsync();
		await base.DisposeAsync();
	}

	/// <summary>
	/// Initialize services and build provider. Call in derived class constructor after setting up services.
	/// </summary>
	protected void InitializeSearchServices()
	{
		ConfigureServices(Services);
		BuildServiceProvider();
	}

	/// <summary>
	/// Configure search-specific services.
	/// </summary>
	protected virtual void ConfigureServices(IServiceCollection services)
	{
		// Override to register Elasticsearch clients
	}

	/// <summary>
	/// Create test indices before tests.
	/// </summary>
	protected virtual Task SetupIndicesAsync() => Task.CompletedTask;

	/// <summary>
	/// Delete test indices after tests.
	/// </summary>
	protected virtual Task CleanupIndicesAsync() => Task.CompletedTask;
}
