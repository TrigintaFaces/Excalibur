// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.InMemory;

/// <summary>
/// Integration tests for <see cref="InMemoryPersistenceProvider"/> multi-tenancy patterns.
/// Tests data isolation, cross-tenant prevention, and per-tenant operations.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 180 - InMemory Provider Testing Epic.
/// bd-leevi: Multi-Tenancy Tests (5 tests).
/// </para>
/// <para>
/// These tests verify multi-tenancy isolation using collection name prefixes.
/// Pattern: "tenant-{tenantId}:{collectionName}" for tenant-specific data.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Component", "MultiTenancy")]
[Trait("Provider", "InMemory")]
public sealed class InMemoryMultiTenancyIntegrationShould : IntegrationTestBase
{
	/// <summary>
	/// Tests that tenant data is isolated using collection name prefixes.
	/// </summary>
	[Fact]
	public void IsolateTenantData()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var tenantA = "tenant-A";
		var tenantB = "tenant-B";
		var baseName = "orders";

		// Create tenant-prefixed collection names
		var tenantACollection = $"{tenantA}:{baseName}";
		var tenantBCollection = $"{tenantB}:{baseName}";

		// Act - Store different data for each tenant
		provider.Store(tenantACollection, "order-1", new OrderEntity { Id = "order-1", TenantId = tenantA, Amount = 100.00m });
		provider.Store(tenantACollection, "order-2", new OrderEntity { Id = "order-2", TenantId = tenantA, Amount = 200.00m });

		provider.Store(tenantBCollection, "order-1", new OrderEntity { Id = "order-1", TenantId = tenantB, Amount = 500.00m });

		// Assert - Each tenant has isolated data
		var tenantAOrder1 = provider.Retrieve<OrderEntity>(tenantACollection, "order-1");
		var tenantBOrder1 = provider.Retrieve<OrderEntity>(tenantBCollection, "order-1");

		_ = tenantAOrder1.ShouldNotBeNull();
		tenantAOrder1.TenantId.ShouldBe(tenantA);
		tenantAOrder1.Amount.ShouldBe(100.00m);

		_ = tenantBOrder1.ShouldNotBeNull();
		tenantBOrder1.TenantId.ShouldBe(tenantB);
		tenantBOrder1.Amount.ShouldBe(500.00m);

		// Different amounts for same order ID proves isolation
		tenantAOrder1.Amount.ShouldNotBe(tenantBOrder1.Amount);
	}

	/// <summary>
	/// Tests that accessing another tenant's collection returns null (no data leakage).
	/// </summary>
	[Fact]
	public void PreventCrossTenantAccess()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var tenantA = "tenant-secure-A";
		var tenantB = "tenant-secure-B";
		var baseName = "secrets";

		var tenantACollection = $"{tenantA}:{baseName}";
		var tenantBCollection = $"{tenantB}:{baseName}";

		// Store data only for tenant A
		provider.Store(tenantACollection, "secret-key", new SecretEntity { Id = "secret-key", Value = "TenantA-Secret" });

		// Act - Attempt to access tenant A's data from tenant B's collection
		var crossTenantAttempt = provider.Retrieve<SecretEntity>(tenantBCollection, "secret-key");

		// Assert - No data leakage
		crossTenantAttempt.ShouldBeNull();

		// Verify tenant A's data is still accessible
		var tenantAData = provider.Retrieve<SecretEntity>(tenantACollection, "secret-key");
		_ = tenantAData.ShouldNotBeNull();
		tenantAData.Value.ShouldBe("TenantA-Secret");
	}

	/// <summary>
	/// Tests tenant-specific CRUD operations work independently.
	/// </summary>
	[Fact]
	public void PerformTenantSpecificOperations()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var tenant = "operations-tenant";
		var collection = $"{tenant}:products";

		// Act - Create
		provider.Store(collection, "prod-1", new ProductEntity { Id = "prod-1", Name = "Widget", Price = 9.99m });
		provider.Store(collection, "prod-2", new ProductEntity { Id = "prod-2", Name = "Gadget", Price = 19.99m });

		// Act - Read
		var product1 = provider.Retrieve<ProductEntity>(collection, "prod-1");
		_ = product1.ShouldNotBeNull();
		product1.Name.ShouldBe("Widget");

		// Act - Update
		provider.Store(collection, "prod-1", new ProductEntity { Id = "prod-1", Name = "Super Widget", Price = 14.99m });
		var updatedProduct = provider.Retrieve<ProductEntity>(collection, "prod-1");
		_ = updatedProduct.ShouldNotBeNull();
		updatedProduct.Name.ShouldBe("Super Widget");
		updatedProduct.Price.ShouldBe(14.99m);

		// Act - Delete
		var removed = provider.Remove(collection, "prod-2");
		removed.ShouldBeTrue();

		var deletedProduct = provider.Retrieve<ProductEntity>(collection, "prod-2");
		deletedProduct.ShouldBeNull();

		// Assert - Collection still contains remaining item
		var tenantCollection = provider.GetCollection(collection);
		tenantCollection.Count.ShouldBe(1);
	}

	/// <summary>
	/// Tests that clearing a tenant's collection removes only that tenant's data.
	/// </summary>
	[Fact]
	public void CleanupTenantDataWithoutAffectingOthers()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var tenantToCleanup = "cleanup-tenant";
		var tenantToKeep = "keep-tenant";
		var baseName = "audit-logs";

		var cleanupCollection = $"{tenantToCleanup}:{baseName}";
		var keepCollection = $"{tenantToKeep}:{baseName}";

		// Store data for both tenants
		provider.Store(cleanupCollection, "log-1", new AuditLog { Id = "log-1", Action = "Created" });
		provider.Store(cleanupCollection, "log-2", new AuditLog { Id = "log-2", Action = "Updated" });

		provider.Store(keepCollection, "log-1", new AuditLog { Id = "log-1", Action = "Logged In" });

		// Act - Clear only the cleanup tenant's collection
		var collectionToClear = provider.GetCollection(cleanupCollection);
		collectionToClear.Clear();

		// Assert - Cleanup tenant's data is gone
		collectionToClear.Count.ShouldBe(0);
		var cleanupLog = provider.Retrieve<AuditLog>(cleanupCollection, "log-1");
		cleanupLog.ShouldBeNull();

		// Assert - Keep tenant's data is intact
		var keepLog = provider.Retrieve<AuditLog>(keepCollection, "log-1");
		_ = keepLog.ShouldNotBeNull();
		keepLog.Action.ShouldBe("Logged In");
	}

	/// <summary>
	/// Tests per-tenant metrics using collection counts.
	/// </summary>
	[Fact]
	public async Task CollectTenantMetrics()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var tenant1 = "metrics-tenant-1";
		var tenant2 = "metrics-tenant-2";

		var tenant1Collection = $"{tenant1}:data";
		var tenant2Collection = $"{tenant2}:data";

		// Store different amounts of data per tenant
		for (var i = 0; i < 5; i++)
		{
			provider.Store(tenant1Collection, $"item-{i}", new TestEntity { Id = $"item-{i}" });
		}

		for (var i = 0; i < 3; i++)
		{
			provider.Store(tenant2Collection, $"item-{i}", new TestEntity { Id = $"item-{i}" });
		}

		// Act - Get per-tenant collection metrics
		var tenant1Data = provider.GetCollection(tenant1Collection);
		var tenant2Data = provider.GetCollection(tenant2Collection);

		// Assert - Per-tenant counts are correct
		tenant1Data.Count.ShouldBe(5);
		tenant2Data.Count.ShouldBe(3);

		// Act - Get overall metrics
		var globalMetrics = await provider.GetMetricsAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert - Global metrics reflect all tenants
		globalMetrics.ShouldContainKey("Collections");
		((int)globalMetrics["Collections"]).ShouldBeGreaterThanOrEqualTo(2);
		globalMetrics.ShouldContainKey("TotalItems");
		((int)globalMetrics["TotalItems"]).ShouldBeGreaterThanOrEqualTo(8);
	}

	/// <summary>
	/// Tests that multiple tenants can work concurrently without interference.
	/// </summary>
	[Fact]
	public async Task HandleConcurrentTenantOperations()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var tenantCount = 5;
		var itemsPerTenant = 10;

		// Act - Multiple tenants performing operations concurrently
		var tasks = Enumerable.Range(0, tenantCount).Select(async tenantNum =>
		{
			var tenantId = $"concurrent-tenant-{tenantNum}";
			var collection = $"{tenantId}:events";

			for (var i = 0; i < itemsPerTenant; i++)
			{
				await Task.Yield(); // Allow interleaving
				provider.Store(collection, $"event-{i}", new EventEntity
				{
					Id = $"event-{i}",
					TenantId = tenantId,
					Sequence = i
				});
			}
		}).ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert - Each tenant has correct data
		for (var tenantNum = 0; tenantNum < tenantCount; tenantNum++)
		{
			var tenantId = $"concurrent-tenant-{tenantNum}";
			var collection = $"{tenantId}:events";

			var tenantData = provider.GetCollection(collection);
			tenantData.Count.ShouldBe(itemsPerTenant);

			// Verify all items belong to the correct tenant
			for (var i = 0; i < itemsPerTenant; i++)
			{
				var eventEntity = provider.Retrieve<EventEntity>(collection, $"event-{i}");
				_ = eventEntity.ShouldNotBeNull();
				eventEntity.TenantId.ShouldBe(tenantId);
				eventEntity.Sequence.ShouldBe(i);
			}
		}
	}

	private static InMemoryPersistenceProvider CreatePersistenceProvider()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions
		{
			Name = $"multitenancy-test-{Guid.NewGuid():N}"
		});
		var logger = NullLogger<InMemoryPersistenceProvider>.Instance;
		return new InMemoryPersistenceProvider(options, logger);
	}

	private sealed class TestEntity
	{
		public string Id { get; set; } = string.Empty;
	}

	private sealed class OrderEntity
	{
		public string Id { get; set; } = string.Empty;
		public string TenantId { get; set; } = string.Empty;
		public decimal Amount { get; set; }
	}

	private sealed class SecretEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Value { get; set; } = string.Empty;
	}

	private sealed class ProductEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public decimal Price { get; set; }
	}

	private sealed class AuditLog
	{
		public string Id { get; set; } = string.Empty;
		public string Action { get; set; } = string.Empty;
	}

	private sealed class EventEntity
	{
		public string Id { get; set; } = string.Empty;
		public string TenantId { get; set; } = string.Empty;
		public int Sequence { get; set; }
	}
}
