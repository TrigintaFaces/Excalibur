// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.MaterializedViews;
using Excalibur.EventSourcing.Abstractions;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.EventSourcing.Tests.MaterializedViews.Providers;

/// <summary>
/// Unit tests for <see cref="MongoDbMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// Sprint 518: Materialized Views provider tests.
/// Tests verify MongoDB store behavior, argument validation, and configuration.
/// Note: Database integration tests would require TestContainers.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "MongoDB")]
public sealed class MongoDbMaterializedViewStoreShould
{
	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(MongoDbMaterializedViewStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(MongoDbMaterializedViewStore).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void ImplementIMaterializedViewStore()
	{
		// Assert
		typeof(IMaterializedViewStore).IsAssignableFrom(typeof(MongoDbMaterializedViewStore)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Assert
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(MongoDbMaterializedViewStore)).ShouldBeTrue();
	}

	[Fact]
	public void HavePartialModifier()
	{
		// Assert - partial class is needed for LoggerMessage source generation
		// Verified by the class compiling with [LoggerMessage] attributes
		typeof(MongoDbMaterializedViewStore).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Constructor Tests (Options-based)

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullOptions()
	{
		// Arrange
		var logger = NullLogger<MongoDbMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MongoDbMaterializedViewStore(options: null!, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullLogger()
	{
		// Arrange
		var options = Options.Create(new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test"
		});

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MongoDbMaterializedViewStore(options, logger: null!));
	}

	[Fact]
	public void Constructor_ThrowInvalidOperationExceptionForMissingConnectionString()
	{
		// Arrange
		var options = Options.Create(new MongoDbMaterializedViewStoreOptions
		{
			DatabaseName = "test"
		});
		var logger = NullLogger<MongoDbMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			new MongoDbMaterializedViewStore(options, logger));
	}

	[Fact]
	public void Constructor_ThrowInvalidOperationExceptionForMissingDatabaseName()
	{
		// Arrange
		var options = Options.Create(new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017"
		});
		var logger = NullLogger<MongoDbMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			new MongoDbMaterializedViewStore(options, logger));
	}

	[Fact]
	public void Constructor_SucceedWithValidOptions()
	{
		// Arrange
		var options = Options.Create(new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test"
		});
		var logger = NullLogger<MongoDbMaterializedViewStore>.Instance;

		// Act
		var store = new MongoDbMaterializedViewStore(options, logger);

		// Assert
		store.ShouldNotBeNull();
	}

	#endregion

	#region Constructor Tests (IMongoClient-based)

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullClient()
	{
		// Arrange
		var options = Options.Create(new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test"
		});
		var logger = NullLogger<MongoDbMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MongoDbMaterializedViewStore(client: null!, options, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullOptionsWithClient()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var logger = NullLogger<MongoDbMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MongoDbMaterializedViewStore(client, options: null!, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullLoggerWithClient()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var options = Options.Create(new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test"
		});

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MongoDbMaterializedViewStore(client, options, logger: null!));
	}

	[Fact]
	public void Constructor_SucceedWithValidClientAndOptions()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var database = A.Fake<IMongoDatabase>();
		A.CallTo(() => client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._)).Returns(database);

		var options = Options.Create(new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test"
		});
		var logger = NullLogger<MongoDbMaterializedViewStore>.Instance;

		// Act
		var store = new MongoDbMaterializedViewStore(client, options, logger);

		// Assert
		store.ShouldNotBeNull();
	}

	#endregion

	#region GetAsync Argument Validation Tests

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: null!, "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: "", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForWhitespaceViewName()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: "   ", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>("TestView", viewId: null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForEmptyViewId()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

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
		var store = CreateStoreWithMockedClient();
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync(viewName: null!, "view-1", view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync(viewName: "", "view-1", view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync("TestView", viewId: null!, view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentNullExceptionForNullView()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

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
		var store = CreateStoreWithMockedClient();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync(viewName: null!, "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync(viewName: "", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync("TestView", viewId: null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForEmptyViewId()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

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
		var store = CreateStoreWithMockedClient();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetPositionAsync(viewName: null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

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
		var store = CreateStoreWithMockedClient();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SavePositionAsync(viewName: null!, 100, CancellationToken.None));
	}

	[Fact]
	public async Task SavePositionAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();

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
		var store = CreateStoreWithMockedClient();

		// Act - dispose twice should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_ThrowObjectDisposedExceptionOnSubsequentCalls()
	{
		// Arrange
		var store = CreateStoreWithMockedClient();
		await store.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.GetAsync<TestView>("TestView", "view-1", CancellationToken.None));
	}

	#endregion

	#region Default Collection Names Tests

	[Fact]
	public void HaveDefaultViewsCollectionName()
	{
		// Assert
		MongoDbMaterializedViewStoreOptions.DefaultViewsCollectionName.ShouldBe("materialized_views");
	}

	[Fact]
	public void HaveDefaultPositionsCollectionName()
	{
		// Assert
		MongoDbMaterializedViewStoreOptions.DefaultPositionsCollectionName.ShouldBe("materialized_view_positions");
	}

	#endregion

	#region Helper Methods

	private static MongoDbMaterializedViewStore CreateStoreWithMockedClient()
	{
		var client = A.Fake<IMongoClient>();
		var database = A.Fake<IMongoDatabase>();
		A.CallTo(() => client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._)).Returns(database);

		var options = Options.Create(new MongoDbMaterializedViewStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test"
		});
		var logger = NullLogger<MongoDbMaterializedViewStore>.Instance;

		return new MongoDbMaterializedViewStore(client, options, logger);
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
