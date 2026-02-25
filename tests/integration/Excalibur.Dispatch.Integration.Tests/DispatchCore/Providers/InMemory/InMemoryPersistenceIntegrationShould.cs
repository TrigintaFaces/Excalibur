// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.InMemory;

/// <summary>
/// Integration tests for <see cref="InMemoryPersistenceProvider"/> persistence operations.
/// Tests CRUD operations, lifecycle management, and health checks.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 180 - InMemory Provider Testing Epic.
/// bd-9q6fv: Persistence Provider Tests (5 tests).
/// </para>
/// <para>
/// These tests verify the InMemoryPersistenceProvider implementation for basic persistence
/// operations. No TestContainers required - tests run entirely in-process.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Component", "Persistence")]
[Trait("Provider", "InMemory")]
public sealed class InMemoryPersistenceIntegrationShould : IntegrationTestBase
{
	/// <summary>
	/// Tests that data can be created and retrieved from the provider.
	/// </summary>
	[Fact]
	public async Task CreateAndRetrieveData()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var collectionName = "test-collection";
		var key = $"item-{Guid.NewGuid()}";
		var testData = new TestEntity { Id = key, Name = "Test Item", Value = 42 };

		// Act
		provider.Store(collectionName, key, testData);
		var retrieved = provider.Retrieve<TestEntity>(collectionName, key);

		// Assert
		_ = retrieved.ShouldNotBeNull();
		retrieved.Id.ShouldBe(key);
		retrieved.Name.ShouldBe("Test Item");
		retrieved.Value.ShouldBe(42);

		// Verify async connection test
		var connectionTest = await provider.TestConnectionAsync(TestCancellationToken).ConfigureAwait(true);
		connectionTest.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that data is isolated between separate provider instances.
	/// </summary>
	[Fact]
	public void IsolateDataBetweenProviders()
	{
		// Arrange
		using var provider1 = CreatePersistenceProvider("provider-1");
		using var provider2 = CreatePersistenceProvider("provider-2");
		var collectionName = "shared-collection";
		var key = "test-key";

		// Act - Store data in provider1
		provider1.Store(collectionName, key, new TestEntity { Id = key, Name = "Provider 1 Data" });

		// Store different data with same key in provider2
		provider2.Store(collectionName, key, new TestEntity { Id = key, Name = "Provider 2 Data" });

		// Assert - Each provider has its own data
		var data1 = provider1.Retrieve<TestEntity>(collectionName, key);
		var data2 = provider2.Retrieve<TestEntity>(collectionName, key);

		_ = data1.ShouldNotBeNull();
		data1.Name.ShouldBe("Provider 1 Data");

		_ = data2.ShouldNotBeNull();
		data2.Name.ShouldBe("Provider 2 Data");
	}

	/// <summary>
	/// Tests the provider lifecycle including initialization, connection testing, and disposal.
	/// </summary>
	[Fact]
	public async Task ManageProviderLifecycle()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions { Name = "lifecycle-test" });
		var logger = NullLogger<InMemoryPersistenceProvider>.Instance;
		var provider = new InMemoryPersistenceProvider(options, logger);

		// Act - Test connection
		var connected = await provider.TestConnectionAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert - Provider is available
		connected.ShouldBeTrue();
		provider.IsAvailable.ShouldBeTrue();
		provider.Name.ShouldBe("lifecycle-test");
		provider.ProviderType.ShouldBe("InMemory");

		// Act - Dispose
		provider.Dispose();

		// Assert - Provider is no longer available
		provider.IsAvailable.ShouldBeFalse();
	}

	/// <summary>
	/// Tests that the provider returns accurate metrics about collections and items.
	/// </summary>
	[Fact]
	public async Task CollectMetrics()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();

		// Store some test data
		var collection1 = "metrics-collection-1";
		var collection2 = "metrics-collection-2";

		provider.Store(collection1, "item-1", new TestEntity { Id = "1" });
		provider.Store(collection1, "item-2", new TestEntity { Id = "2" });
		provider.Store(collection2, "item-1", new TestEntity { Id = "3" });

		// Act
		var metrics = await provider.GetMetricsAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = metrics.ShouldNotBeNull();
		metrics.ShouldContainKey("Provider");
		metrics["Provider"].ShouldBe("InMemory");
		metrics.ShouldContainKey("Collections");
		((int)metrics["Collections"]).ShouldBeGreaterThanOrEqualTo(2);
		metrics.ShouldContainKey("TotalItems");
		((int)metrics["TotalItems"]).ShouldBeGreaterThanOrEqualTo(3);
		metrics.ShouldContainKey("IsAvailable");
		((bool)metrics["IsAvailable"]).ShouldBeTrue();
	}

	/// <summary>
	/// Tests health check integration via the IsAvailable property.
	/// </summary>
	[Fact]
	public async Task IntegrateWithHealthChecks()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();

		// Act - Check initial health
		var initialHealth = provider.IsAvailable;
		var connectionHealth = await provider.TestConnectionAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert - Provider is healthy
		initialHealth.ShouldBeTrue();
		connectionHealth.ShouldBeTrue();

		// Act - Verify metadata contains health information
		var metadata = provider.GetMetadata();

		// Assert - Metadata is available
		_ = metadata.ShouldNotBeNull();
		metadata.ShouldContainKey("Provider");
		metadata["Provider"].ShouldBe("InMemory");

		// Verify connection pool stats returns null (InMemory doesn't have pooling)
		var poolStats = await provider.GetConnectionPoolStatsAsync(TestCancellationToken).ConfigureAwait(true);
		poolStats.ShouldBeNull();
	}

	private static InMemoryPersistenceProvider CreatePersistenceProvider(string? name = null)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions
		{
			Name = name ?? $"test-inmemory-{Guid.NewGuid():N}",
			MaxItemsPerCollection = 1000
		});
		var logger = NullLogger<InMemoryPersistenceProvider>.Instance;
		return new InMemoryPersistenceProvider(options, logger);
	}

	private sealed class TestEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public int Value { get; set; }
	}
}
