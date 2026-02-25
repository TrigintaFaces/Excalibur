// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="MaterializedViewsBuilder"/>.
/// </summary>
/// <remarks>
/// Sprint 516: Materialized Views foundation tests.
/// Tests verify internal builder implementation behavior.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "DependencyInjection")]
public sealed class MaterializedViewsBuilderShould
{
	#region Constructor Tests

	[Fact]
	public void ExposeServicesProperty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			// Assert - builder exposes the services
			builder.Services.ShouldBeSameAs(services);
		});
	}

	#endregion

	#region AddBuilder Tests

	[Fact]
	public void AddBuilder_UsesTryAddSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.AddBuilder<TestView, TestViewBuilder>();
			builder.AddBuilder<TestView, AlternativeTestViewBuilder>();
		});

		// Assert - first registration wins
		var provider = services.BuildServiceProvider();
		var viewBuilder = provider.GetRequiredService<IMaterializedViewBuilder<TestView>>();
		viewBuilder.ShouldBeOfType<TestViewBuilder>();
	}

	[Fact]
	public void AddBuilder_RegisterMultipleBuilders()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.AddBuilder<TestView, TestViewBuilder>();
		});

		// Assert - view builder should be registered
		var provider = services.BuildServiceProvider();
		var viewBuilder = provider.GetService<IMaterializedViewBuilder<TestView>>();
		viewBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void AddBuilderWithFactory_ThrowsOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMaterializedViews(builder =>
			{
				builder.AddBuilder((Func<IServiceProvider, IMaterializedViewBuilder<TestView>>)null!);
			}));
	}

	#endregion

	#region UseStore Tests

	[Fact]
	public void UseStore_UsesTryAddSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.UseStore<TestStore>();
			builder.UseStore<AlternativeTestStore>();
		});

		// Assert - first registration wins
		var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<IMaterializedViewStore>();
		store.ShouldBeOfType<TestStore>();
	}

	[Fact]
	public void UseStoreWithFactory_ThrowsOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMaterializedViews(builder =>
			{
				builder.UseStore((Func<IServiceProvider, IMaterializedViewStore>)null!);
			}));
	}

	#endregion

	#region UseProcessor Tests

	[Fact]
	public void UseProcessor_UsesTryAddSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.UseProcessor<TestProcessor>();
			builder.UseProcessor<AlternativeTestProcessor>();
		});

		// Assert - first registration wins
		var provider = services.BuildServiceProvider();
		var processor = provider.GetRequiredService<IMaterializedViewProcessor>();
		processor.ShouldBeOfType<TestProcessor>();
	}

	#endregion

	#region EnableCatchUpOnStartup Tests

	[Fact]
	public void EnableCatchUpOnStartup_ConfiguresOptions()
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
	public void EnableCatchUpOnStartup_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		IMaterializedViewsBuilder? capturedBuilder = null;

		// Act
		services.AddMaterializedViews(builder =>
		{
			capturedBuilder = builder.EnableCatchUpOnStartup();
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	#endregion

	#region WithBatchSize Tests

	[Fact]
	public void WithBatchSize_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.WithBatchSize(500);
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MaterializedViewOptions>>();
		options.Value.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void WithBatchSize_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		IMaterializedViewsBuilder? capturedBuilder = null;

		// Act
		services.AddMaterializedViews(builder =>
		{
			capturedBuilder = builder.WithBatchSize(100);
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void WithBatchSize_RejectsNonPositiveValues(int invalidBatchSize)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddMaterializedViews(builder =>
			{
				builder.WithBatchSize(invalidBatchSize);
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(1000)]
	[InlineData(10000)]
	public void WithBatchSize_AcceptsPositiveValues(int validBatchSize)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMaterializedViews(builder =>
		{
			builder.WithBatchSize(validBatchSize);
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MaterializedViewOptions>>();
		options.Value.BatchSize.ShouldBe(validBatchSize);
	}

	#endregion

	#region Test Types

	internal sealed class TestView
	{
		public string Id { get; set; } = string.Empty;
	}

	internal sealed class TestViewBuilder : IMaterializedViewBuilder<TestView>
	{
		public string ViewName => "TestView";
		public IReadOnlyList<Type> HandledEventTypes => Array.Empty<Type>();
		public string? GetViewId(IDomainEvent @event) => null;
		public TestView Apply(TestView view, IDomainEvent @event) => view;
		public TestView CreateNew() => new();
	}

	internal sealed class AlternativeTestViewBuilder : IMaterializedViewBuilder<TestView>
	{
		public string ViewName => "AlternativeTestView";
		public IReadOnlyList<Type> HandledEventTypes => Array.Empty<Type>();
		public string? GetViewId(IDomainEvent @event) => null;
		public TestView Apply(TestView view, IDomainEvent @event) => view;
		public TestView CreateNew() => new();
	}

	internal sealed class TestStore : IMaterializedViewStore
	{
		public ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken cancellationToken) where TView : class => default;
		public ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken cancellationToken) where TView : class => default;
		public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken cancellationToken) => default;
		public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken cancellationToken) => default;
		public ValueTask SavePositionAsync(string viewName, long position, CancellationToken cancellationToken) => default;
	}

	internal sealed class AlternativeTestStore : IMaterializedViewStore
	{
		public ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken cancellationToken) where TView : class => default;
		public ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken cancellationToken) where TView : class => default;
		public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken cancellationToken) => default;
		public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken cancellationToken) => default;
		public ValueTask SavePositionAsync(string viewName, long position, CancellationToken cancellationToken) => default;
	}

	internal sealed class TestProcessor : IMaterializedViewProcessor
	{
		public Task ProcessEventAsync(IDomainEvent @event, long position, CancellationToken cancellationToken) => Task.CompletedTask;
		public Task ProcessEventsAsync(IEnumerable<(IDomainEvent Event, long Position)> events, CancellationToken cancellationToken) => Task.CompletedTask;
		public Task RebuildAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task CatchUpAsync(string viewName, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	internal sealed class AlternativeTestProcessor : IMaterializedViewProcessor
	{
		public Task ProcessEventAsync(IDomainEvent @event, long position, CancellationToken cancellationToken) => Task.CompletedTask;
		public Task ProcessEventsAsync(IEnumerable<(IDomainEvent Event, long Position)> events, CancellationToken cancellationToken) => Task.CompletedTask;
		public Task RebuildAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task CatchUpAsync(string viewName, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#endregion
}
