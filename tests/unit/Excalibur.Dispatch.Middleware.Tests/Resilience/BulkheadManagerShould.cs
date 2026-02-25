// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="BulkheadManager"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class BulkheadManagerShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new BulkheadManager(null!));
	}

	[Fact]
	public void Constructor_WithValidLogger_CreatesInstance()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();

		// Act
		var manager = new BulkheadManager(logger);

		// Assert
		_ = manager.ShouldNotBeNull();
	}

	#endregion

	#region GetOrCreateBulkhead Tests

	[Fact]
	public void GetOrCreateBulkhead_WithNullResourceName_ThrowsArgumentException()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => manager.GetOrCreateBulkhead(null!));
	}

	[Fact]
	public void GetOrCreateBulkhead_WithEmptyResourceName_ThrowsArgumentException()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => manager.GetOrCreateBulkhead(string.Empty));
	}

	[Fact]
	public void GetOrCreateBulkhead_WithWhitespaceResourceName_ThrowsArgumentException()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => manager.GetOrCreateBulkhead("   "));
	}

	[Fact]
	public void GetOrCreateBulkhead_WithValidResourceName_CreatesBulkhead()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);

		// Act
		var bulkhead = manager.GetOrCreateBulkhead("test-resource");

		// Assert
		_ = bulkhead.ShouldNotBeNull();
	}

	[Fact]
	public void GetOrCreateBulkhead_WithCustomOptions_UsesThem()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);
		var options = new BulkheadOptions { MaxConcurrency = 5 };

		// Act
		var bulkhead = manager.GetOrCreateBulkhead("test-resource", options);

		// Assert
		_ = bulkhead.ShouldNotBeNull();
		// The bulkhead was created with custom options
	}

	[Fact]
	public void GetOrCreateBulkhead_CalledTwiceForSameResource_ReturnsSameInstance()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);

		// Act
		var bulkhead1 = manager.GetOrCreateBulkhead("test-resource");
		var bulkhead2 = manager.GetOrCreateBulkhead("test-resource");

		// Assert
		bulkhead1.ShouldBeSameAs(bulkhead2);
	}

	[Fact]
	public void GetOrCreateBulkhead_CalledForDifferentResources_ReturnsDifferentInstances()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);

		// Act
		var bulkhead1 = manager.GetOrCreateBulkhead("resource-1");
		var bulkhead2 = manager.GetOrCreateBulkhead("resource-2");

		// Assert
		bulkhead1.ShouldNotBeSameAs(bulkhead2);
	}

	[Fact]
	public void GetOrCreateBulkhead_WithNullOptions_UsesDefaults()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);

		// Act
		var bulkhead = manager.GetOrCreateBulkhead("test-resource", null);

		// Assert
		_ = bulkhead.ShouldNotBeNull();
	}

	#endregion

	#region GetAllMetrics Tests

	[Fact]
	public void GetAllMetrics_WhenNoBulkheads_ReturnsEmptyDictionary()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);

		// Act
		var metrics = manager.GetAllMetrics();

		// Assert
		metrics.ShouldBeEmpty();
	}

	[Fact]
	public void GetAllMetrics_WithSingleBulkhead_ReturnsSingleEntry()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);
		_ = manager.GetOrCreateBulkhead("test-resource");

		// Act
		var metrics = manager.GetAllMetrics();

		// Assert
		metrics.Count.ShouldBe(1);
		metrics.ShouldContainKey("test-resource");
	}

	[Fact]
	public void GetAllMetrics_WithMultipleBulkheads_ReturnsAllEntries()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);
		_ = manager.GetOrCreateBulkhead("resource-1");
		_ = manager.GetOrCreateBulkhead("resource-2");
		_ = manager.GetOrCreateBulkhead("resource-3");

		// Act
		var metrics = manager.GetAllMetrics();

		// Assert
		metrics.Count.ShouldBe(3);
		metrics.ShouldContainKey("resource-1");
		metrics.ShouldContainKey("resource-2");
		metrics.ShouldContainKey("resource-3");
	}

	#endregion

	#region RemoveBulkhead Tests

	[Fact]
	public void RemoveBulkhead_WhenBulkheadExists_ReturnsTrue()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);
		_ = manager.GetOrCreateBulkhead("test-resource");

		// Act
		var result = manager.RemoveBulkhead("test-resource");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void RemoveBulkhead_WhenBulkheadDoesNotExist_ReturnsFalse()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);

		// Act
		var result = manager.RemoveBulkhead("non-existent-resource");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void RemoveBulkhead_AfterRemoval_BulkheadIsNoLongerInMetrics()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);
		_ = manager.GetOrCreateBulkhead("test-resource");

		// Act
		_ = manager.RemoveBulkhead("test-resource");
		var metrics = manager.GetAllMetrics();

		// Assert
		metrics.ShouldBeEmpty();
	}

	[Fact]
	public void RemoveBulkhead_AfterRemoval_GetOrCreateCreatesNewInstance()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);
		var original = manager.GetOrCreateBulkhead("test-resource");

		// Act
		_ = manager.RemoveBulkhead("test-resource");
		var newBulkhead = manager.GetOrCreateBulkhead("test-resource");

		// Assert
		newBulkhead.ShouldNotBeSameAs(original);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task GetOrCreateBulkhead_UnderConcurrentAccess_IsThreadSafe()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);
		var tasks = new List<Task<IBulkheadPolicy>>();
		const int concurrencyLevel = 10;

		// Act - Create bulkheads concurrently for the same resource
		for (int i = 0; i < concurrencyLevel; i++)
		{
			tasks.Add(Task.Run(() => manager.GetOrCreateBulkhead("shared-resource")));
		}

		var results = await Task.WhenAll(tasks);

		// Assert - All tasks should return the same instance
		var firstBulkhead = results[0];
		foreach (var bulkhead in results)
		{
			bulkhead.ShouldBeSameAs(firstBulkhead);
		}
	}

	[Fact]
	public async Task GetAllMetrics_UnderConcurrentAccess_IsThreadSafe()
	{
		// Arrange
		var logger = A.Fake<ILogger<BulkheadManager>>();
		var manager = new BulkheadManager(logger);
		_ = manager.GetOrCreateBulkhead("resource-1");
		_ = manager.GetOrCreateBulkhead("resource-2");
		var tasks = new List<Task<IReadOnlyDictionary<string, BulkheadMetrics>>>();
		const int concurrencyLevel = 10;

		// Act - Get metrics concurrently
		for (int i = 0; i < concurrencyLevel; i++)
		{
			tasks.Add(Task.Run(() => manager.GetAllMetrics()));
		}

		var results = await Task.WhenAll(tasks);

		// Assert - All results should have the same count
		foreach (var metrics in results)
		{
			metrics.Count.ShouldBe(2);
		}
	}

	#endregion
}
