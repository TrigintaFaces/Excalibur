// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Functional tests for <see cref="BulkheadManager"/> verifying
/// bulkhead creation, removal, and metrics aggregation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class BulkheadManagerFunctionalShould
{
	private BulkheadManager CreateManager()
	{
		var logger = A.Fake<ILogger<BulkheadManager>>();
		return new BulkheadManager(logger);
	}

	[Fact]
	public void Create_bulkhead_with_default_options()
	{
		var manager = CreateManager();

		var bulkhead = manager.GetOrCreateBulkhead("resource-1");

		bulkhead.ShouldNotBeNull();
	}

	[Fact]
	public void Create_bulkhead_with_custom_options()
	{
		var manager = CreateManager();
		var options = new BulkheadOptions { MaxConcurrency = 3, MaxQueueLength = 10 };

		var bulkhead = manager.GetOrCreateBulkhead("resource-1", options);

		var metrics = bulkhead.GetMetrics();
		metrics.MaxConcurrency.ShouldBe(3);
		metrics.MaxQueueLength.ShouldBe(10);
	}

	[Fact]
	public void Return_same_bulkhead_for_same_resource()
	{
		var manager = CreateManager();

		var bulkhead1 = manager.GetOrCreateBulkhead("resource-1");
		var bulkhead2 = manager.GetOrCreateBulkhead("resource-1");

		bulkhead1.ShouldBeSameAs(bulkhead2);
	}

	[Fact]
	public void Return_different_bulkheads_for_different_resources()
	{
		var manager = CreateManager();

		var bulkhead1 = manager.GetOrCreateBulkhead("resource-1");
		var bulkhead2 = manager.GetOrCreateBulkhead("resource-2");

		bulkhead1.ShouldNotBeSameAs(bulkhead2);
	}

	[Fact]
	public void Get_all_metrics()
	{
		var manager = CreateManager();
		manager.GetOrCreateBulkhead("db");
		manager.GetOrCreateBulkhead("api");
		manager.GetOrCreateBulkhead("cache");

		var allMetrics = manager.GetAllMetrics();

		allMetrics.Count.ShouldBe(3);
		allMetrics.ShouldContainKey("db");
		allMetrics.ShouldContainKey("api");
		allMetrics.ShouldContainKey("cache");
	}

	[Fact]
	public void Get_all_metrics_returns_empty_when_none()
	{
		var manager = CreateManager();

		var allMetrics = manager.GetAllMetrics();

		allMetrics.ShouldBeEmpty();
	}

	[Fact]
	public void Remove_existing_bulkhead()
	{
		var manager = CreateManager();
		manager.GetOrCreateBulkhead("resource-1");

		var removed = manager.RemoveBulkhead("resource-1");

		removed.ShouldBeTrue();
		var metrics = manager.GetAllMetrics();
		metrics.ShouldNotContainKey("resource-1");
	}

	[Fact]
	public void Remove_nonexistent_bulkhead_returns_false()
	{
		var manager = CreateManager();

		var removed = manager.RemoveBulkhead("nonexistent");

		removed.ShouldBeFalse();
	}

	[Fact]
	public void Create_new_bulkhead_after_removal()
	{
		var manager = CreateManager();
		var options1 = new BulkheadOptions { MaxConcurrency = 5 };
		var options2 = new BulkheadOptions { MaxConcurrency = 10 };

		manager.GetOrCreateBulkhead("resource-1", options1);
		manager.RemoveBulkhead("resource-1");
		var newBulkhead = manager.GetOrCreateBulkhead("resource-1", options2);

		newBulkhead.GetMetrics().MaxConcurrency.ShouldBe(10);
	}

	[Fact]
	public void Throw_for_null_resource_name()
	{
		var manager = CreateManager();

		Should.Throw<ArgumentException>(() => manager.GetOrCreateBulkhead(null!));
	}

	[Fact]
	public void Throw_for_empty_resource_name()
	{
		var manager = CreateManager();

		Should.Throw<ArgumentException>(() => manager.GetOrCreateBulkhead(""));
	}

	[Fact]
	public void Throw_for_whitespace_resource_name()
	{
		var manager = CreateManager();

		Should.Throw<ArgumentException>(() => manager.GetOrCreateBulkhead("   "));
	}

	[Fact]
	public async Task Execute_through_managed_bulkhead()
	{
		var manager = CreateManager();
		var bulkhead = manager.GetOrCreateBulkhead("test-resource", new BulkheadOptions { MaxConcurrency = 5 });

		var result = await bulkhead.ExecuteAsync(
			() => Task.FromResult(42),
			CancellationToken.None);

		result.ShouldBe(42);

		var allMetrics = manager.GetAllMetrics();
		allMetrics["test-resource"].TotalExecutions.ShouldBe(1);
	}

	[Fact]
	public void Metrics_include_name_from_resource()
	{
		var manager = CreateManager();
		manager.GetOrCreateBulkhead("my-db-pool");

		var allMetrics = manager.GetAllMetrics();

		allMetrics["my-db-pool"].Name.ShouldBe("my-db-pool");
	}
}
