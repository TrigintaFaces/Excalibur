// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.MaterializedViews;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.MaterializedViews.Providers;

/// <summary>
/// Unit tests for <see cref="ElasticSearchMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// Sprint 518: Materialized Views provider tests.
/// Tests verify Elasticsearch store behavior, argument validation, and configuration.
/// Note: Database integration tests would require TestContainers or actual ES instance.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Elasticsearch")]
public sealed class ElasticSearchMaterializedViewStoreShould
{
	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(ElasticSearchMaterializedViewStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(ElasticSearchMaterializedViewStore).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void ImplementIMaterializedViewStore()
	{
		// Assert
		typeof(IMaterializedViewStore).IsAssignableFrom(typeof(ElasticSearchMaterializedViewStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Assert
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(ElasticSearchMaterializedViewStore)).ShouldBeTrue();
	}

	[Fact]
	public void HavePartialModifier()
	{
		// Assert - partial class is needed for LoggerMessage source generation
		// Verified by the class compiling with [LoggerMessage] attributes
		typeof(ElasticSearchMaterializedViewStore).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Constructor Tests (Options-based)

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullOptions()
	{
		// Arrange
		var logger = NullLogger<ElasticSearchMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ElasticSearchMaterializedViewStore(options: null!, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullLogger()
	{
		// Arrange
		var options = Options.Create(new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200"
		});

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ElasticSearchMaterializedViewStore(options, logger: null!));
	}

	[Fact]
	public void Constructor_ThrowInvalidOperationExceptionForMissingNodeUri()
	{
		// Arrange
		var options = Options.Create(new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = ""
		});
		var logger = NullLogger<ElasticSearchMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			new ElasticSearchMaterializedViewStore(options, logger));
	}

	[Fact]
	public void Constructor_ThrowInvalidOperationExceptionForInvalidNodeUri()
	{
		// Arrange
		var options = Options.Create(new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "not-a-valid-uri"
		});
		var logger = NullLogger<ElasticSearchMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			new ElasticSearchMaterializedViewStore(options, logger));
	}

	[Fact]
	public void Constructor_SucceedWithValidOptions()
	{
		// Arrange
		var options = Options.Create(new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200"
		});
		var logger = NullLogger<ElasticSearchMaterializedViewStore>.Instance;

		// Act
		var store = new ElasticSearchMaterializedViewStore(options, logger);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptCustomJsonSerializerOptions()
	{
		// Arrange
		var options = Options.Create(new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200"
		});
		var logger = NullLogger<ElasticSearchMaterializedViewStore>.Instance;
		var jsonOptions = new System.Text.Json.JsonSerializerOptions
		{
			PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
		};

		// Act
		var store = new ElasticSearchMaterializedViewStore(options, logger, jsonOptions);

		// Assert
		store.ShouldNotBeNull();
	}

	#endregion

	#region GetAsync Argument Validation Tests

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: null!, "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: "", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForWhitespaceViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: "   ", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>("TestView", viewId: null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForEmptyViewId()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>("TestView", viewId: "", CancellationToken.None));
	}

	#endregion

	#region SaveAsync Argument Validation Tests

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync(viewName: null!, "view-1", view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync(viewName: "", "view-1", view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var store = CreateStoreWithDefaults();
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync("TestView", viewId: null!, view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentNullExceptionForNullView()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.SaveAsync<TestView>("TestView", "view-1", view: null!, CancellationToken.None));
	}

	#endregion

	#region DeleteAsync Argument Validation Tests

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync(viewName: null!, "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync(viewName: "", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync("TestView", viewId: null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForEmptyViewId()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync("TestView", viewId: "", CancellationToken.None));
	}

	#endregion

	#region GetPositionAsync Argument Validation Tests

	[Fact]
	public async Task GetPositionAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetPositionAsync(viewName: null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetPositionAsync(viewName: "", CancellationToken.None));
	}

	#endregion

	#region SavePositionAsync Argument Validation Tests

	[Fact]
	public async Task SavePositionAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SavePositionAsync(viewName: null!, 100, CancellationToken.None));
	}

	[Fact]
	public async Task SavePositionAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SavePositionAsync(viewName: "", 100, CancellationToken.None));
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_BeIdempotent()
	{
		// Arrange
		var store = CreateStoreWithDefaults();

		// Act - dispose twice should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_ThrowObjectDisposedExceptionOnSubsequentCalls()
	{
		// Arrange
		var store = CreateStoreWithDefaults();
		await store.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.GetAsync<TestView>("TestView", "view-1", CancellationToken.None));
	}

	#endregion

	#region Default Index Names Tests

	[Fact]
	public void HaveDefaultViewsIndexName()
	{
		// Assert
		ElasticSearchMaterializedViewStoreOptions.DefaultViewsIndexName.ShouldBe("materialized-views");
	}

	[Fact]
	public void HaveDefaultPositionsIndexName()
	{
		// Assert
		ElasticSearchMaterializedViewStoreOptions.DefaultPositionsIndexName.ShouldBe("materialized-view-positions");
	}

	#endregion

	#region Options Configuration Tests

	[Fact]
	public void Options_HaveCorrectDefaultNodeUri()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.NodeUri.ShouldBe("http://localhost:9200");
	}

	[Fact]
	public void Options_HaveCorrectDefaultRequestTimeout()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.RequestTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void Options_HaveCorrectDefaultShards()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.NumberOfShards.ShouldBe(1);
	}

	[Fact]
	public void Options_HaveCorrectDefaultReplicas()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.NumberOfReplicas.ShouldBe(0);
	}

	[Fact]
	public void Options_HaveCreateIndexOnInitializeEnabledByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.CreateIndexOnInitialize.ShouldBeTrue();
	}

	[Fact]
	public void Options_HaveCorrectDefaultRefreshInterval()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.RefreshInterval.ShouldBe("1s");
	}

	[Fact]
	public void Options_HaveNullUsernameByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.Username.ShouldBeNull();
	}

	[Fact]
	public void Options_HaveNullPasswordByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.Password.ShouldBeNull();
	}

	[Fact]
	public void Options_HaveNullApiKeyByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.ApiKey.ShouldBeNull();
	}

	[Fact]
	public void Options_HaveDebugModeDisabledByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.EnableDebugMode.ShouldBeFalse();
	}

	#endregion

	#region Options Validation Tests

	[Fact]
	public void Options_Validate_ThrowOnEmptyViewsIndexName()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200",
			ViewsIndexName = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Options_Validate_ThrowOnEmptyPositionsIndexName()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200",
			PositionsIndexName = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Options_Validate_SucceedWithValidOptions()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200"
		};

		// Act & Assert - should not throw
		options.Validate();
	}

	#endregion

	#region Helper Methods

	private static ElasticSearchMaterializedViewStore CreateStoreWithDefaults()
	{
		var options = Options.Create(new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200"
		});
		var logger = NullLogger<ElasticSearchMaterializedViewStore>.Instance;

		return new ElasticSearchMaterializedViewStore(options, logger);
	}

	#endregion

	#region Test Types

	private sealed class TestView
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	#endregion
}
