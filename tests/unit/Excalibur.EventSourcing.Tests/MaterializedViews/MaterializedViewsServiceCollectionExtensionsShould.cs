// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Health;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="MaterializedViewsServiceCollectionExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 516: Materialized Views foundation tests.
/// Tests verify DI registration behavior and fluent builder configuration.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "DependencyInjection")]
public sealed class MaterializedViewsServiceCollectionExtensionsShould
{
	#region AddMaterializedViews Basic Tests

	[Fact]
	public void AddMaterializedViews_RegisterOptionsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews();

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<MaterializedViewOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void AddMaterializedViews_ReturnServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMaterializedViews();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddMaterializedViews_ThrowOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => services.AddMaterializedViews());
	}

	[Fact]
	public void AddMaterializedViews_BeIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews();
		services.AddMaterializedViews();

		// Assert - should not throw and should have expected registrations
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<MaterializedViewOptions>>();
		options.ShouldNotBeNull();
	}

	#endregion

	#region AddMaterializedViews with Configuration Tests

	[Fact]
	public void AddMaterializedViews_AcceptConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var configureWasCalled = false;

		// Act
		services.AddMaterializedViews(builder =>
		{
			configureWasCalled = true;
			builder.ShouldNotBeNull();
		});

		// Assert
		configureWasCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddMaterializedViews_ThrowOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMaterializedViews((Action<IMaterializedViewsBuilder>)null!));
	}

	[Fact]
	public void AddMaterializedViews_ConfigureEnableCatchUpOnStartup()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.EnableCatchUpOnStartup();
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MaterializedViewOptions>>();
		options.Value.CatchUpOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void AddMaterializedViews_ConfigureWithBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.WithBatchSize(250);
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MaterializedViewOptions>>();
		options.Value.BatchSize.ShouldBe(250);
	}

	[Fact]
	public void AddMaterializedViews_RejectInvalidBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddMaterializedViews(builder =>
			{
				builder.WithBatchSize(0);
			}));
	}

	[Fact]
	public void AddMaterializedViews_RejectNegativeBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddMaterializedViews(builder =>
			{
				builder.WithBatchSize(-1);
			}));
	}

	#endregion

	#region Builder Registration Tests

	[Fact]
	public void AddBuilder_RegisterViewBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.AddBuilder<TestView, TestViewBuilder>();
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var viewBuilder = provider.GetService<IMaterializedViewBuilder<TestView>>();
		viewBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void AddBuilder_RegisterWithFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		var factoryWasCalled = false;

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.AddBuilder(sp =>
			{
				factoryWasCalled = true;
				return new TestViewBuilder();
			});
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var viewBuilder = provider.GetService<IMaterializedViewBuilder<TestView>>();
		viewBuilder.ShouldNotBeNull();
		factoryWasCalled.ShouldBeTrue();
	}

	#endregion

	#region Store Registration Tests

	[Fact]
	public void UseStore_RegisterStoreImplementation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.UseStore<TestMaterializedViewStore>();
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IMaterializedViewStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<TestMaterializedViewStore>();
	}

	[Fact]
	public void UseStore_RegisterWithFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		var factoryWasCalled = false;

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.UseStore(sp =>
			{
				factoryWasCalled = true;
				return new TestMaterializedViewStore();
			});
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IMaterializedViewStore>();
		store.ShouldNotBeNull();
		factoryWasCalled.ShouldBeTrue();
	}

	#endregion

	#region Processor Registration Tests

	[Fact]
	public void UseProcessor_RegisterProcessorImplementation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.UseProcessor<TestMaterializedViewProcessor>();
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var processor = provider.GetService<IMaterializedViewProcessor>();
		processor.ShouldNotBeNull();
		processor.ShouldBeOfType<TestMaterializedViewProcessor>();
	}

	#endregion

	#region HasMaterializedViews Tests

	[Fact]
	public void HasMaterializedViews_ReturnFalseWhenNotRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		services.HasMaterializedViews().ShouldBeFalse();
	}

	[Fact]
	public void HasMaterializedViews_ReturnTrueWhenStoreRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMaterializedViews(builder =>
		{
			builder.UseStore<TestMaterializedViewStore>();
		});

		// Act & Assert
		services.HasMaterializedViews().ShouldBeTrue();
	}

	[Fact]
	public void HasMaterializedViews_ThrowOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => services.HasMaterializedViews());
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			var result = builder
				.AddBuilder<TestView, TestViewBuilder>()
				.UseStore<TestMaterializedViewStore>()
				.UseProcessor<TestMaterializedViewProcessor>()
				.EnableCatchUpOnStartup()
				.WithBatchSize(500);

			// Assert - all methods should return the builder
			result.ShouldNotBeNull();
			result.ShouldBeAssignableTo<IMaterializedViewsBuilder>();
		});
	}

	#endregion

	#region Health Checks Registration Tests

	[Fact]
	public void WithHealthChecks_RegisterHealthCheckWithHealthCheckService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.WithHealthChecks();
		});

		// Assert - health check is registered via AddHealthChecks, not directly
		var descriptors = services.Where(sd => sd.ServiceType == typeof(HealthCheckService)).ToList();
		descriptors.ShouldNotBeEmpty();
	}

	[Fact]
	public void WithHealthChecks_RegisterMetricsAsDependency()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.WithHealthChecks();
		});

		// Assert - metrics should be registered as a dependency of health check
		var provider = services.BuildServiceProvider();
		var metrics = provider.GetService<MaterializedViewMetrics>();
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void WithHealthChecks_AcceptCustomConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.WithHealthChecks(options =>
			{
				options.StalenessThreshold = TimeSpan.FromMinutes(10);
				options.FailureRateThresholdPercent = 25.0;
			});
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MaterializedViewHealthCheckOptions>>();
		options.Value.StalenessThreshold.ShouldBe(TimeSpan.FromMinutes(10));
		options.Value.FailureRateThresholdPercent.ShouldBe(25.0);
	}

	[Fact]
	public void WithHealthChecks_ThrowOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMaterializedViews(builder =>
			{
				builder.WithHealthChecks(null!);
			}));
	}

	#endregion

	#region Metrics Registration Tests

	[Fact]
	public void WithMetrics_RegisterMetrics()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.WithMetrics();
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var metrics = provider.GetService<MaterializedViewMetrics>();
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void WithMetrics_BeIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.WithMetrics();
			builder.WithMetrics();
		});

		// Assert - should only register once
		var provider = services.BuildServiceProvider();
		var metrics = provider.GetServices<MaterializedViewMetrics>().ToList();
		metrics.Count.ShouldBe(1);
	}

	[Fact]
	public void SupportHealthChecksAndMetricsChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			var result = builder
				.AddBuilder<TestView, TestViewBuilder>()
				.UseStore<TestMaterializedViewStore>()
				.WithMetrics()
				.WithHealthChecks();

			// Assert - all methods should return the builder
			result.ShouldNotBeNull();
			result.ShouldBeAssignableTo<IMaterializedViewsBuilder>();
		});

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<MaterializedViewMetrics>().ShouldNotBeNull();
		// Health check is registered via the health check builder, not directly
		var descriptors = services.Where(sd => sd.ServiceType == typeof(HealthCheckService)).ToList();
		descriptors.ShouldNotBeEmpty();
	}

	#endregion

	#region Test Types

	/// <summary>
	/// Test view type for DI registration testing.
	/// </summary>
	internal sealed class TestView
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	/// <summary>
	/// Test view builder for DI registration testing.
	/// </summary>
	internal sealed class TestViewBuilder : IMaterializedViewBuilder<TestView>
	{
		public string ViewName => "TestView";

		public IReadOnlyList<Type> HandledEventTypes => Array.Empty<Type>();

		public string? GetViewId(IDomainEvent @event) => @event.AggregateId;

		public TestView Apply(TestView view, IDomainEvent @event) => view;

		public TestView CreateNew() => new();
	}

	/// <summary>
	/// Test store implementation for DI registration testing.
	/// </summary>
	internal sealed class TestMaterializedViewStore : IMaterializedViewStore
	{
		public ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken cancellationToken)
			where TView : class => default;

		public ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken cancellationToken)
			where TView : class => default;

		public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken cancellationToken) => default;

		public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken cancellationToken) => default;

		public ValueTask SavePositionAsync(string viewName, long position, CancellationToken cancellationToken) => default;
	}

	/// <summary>
	/// Test processor implementation for DI registration testing.
	/// </summary>
	internal sealed class TestMaterializedViewProcessor : IMaterializedViewProcessor
	{
		public Task ProcessEventAsync(IDomainEvent @event, long position, CancellationToken cancellationToken)
			=> Task.CompletedTask;

		public Task ProcessEventsAsync(IEnumerable<(IDomainEvent Event, long Position)> events, CancellationToken cancellationToken)
			=> Task.CompletedTask;

		public Task RebuildAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		public Task CatchUpAsync(string viewName, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#endregion
}
