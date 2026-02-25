// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Projections;

using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Projections;

/// <summary>
/// Unit tests for the <see cref="MongoDbProjectionStore{TProjection}"/> dual-constructor pattern.
/// Verifies both simple (options-based) and advanced (IMongoClient) constructors.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MongoDbProjectionStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<MongoDbProjectionStore<TestProjection>> _logger;
	private readonly IOptions<MongoDbProjectionStoreOptions> _options;

	public MongoDbProjectionStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<MongoDbProjectionStore<TestProjection>>>();
		_options = Options.Create(new MongoDbProjectionStoreOptions());
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidOptions_CreatesInstance()
	{
		// Arrange & Act
		var store = new MongoDbProjectionStore<TestProjection>(_options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbProjectionStore<TestProjection>(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbProjectionStore<TestProjection>(_options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void SimpleConstructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbProjectionStoreOptions
		{
			ConnectionString = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbProjectionStore<TestProjection>(invalidOptions, _logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullDatabaseName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbProjectionStoreOptions
		{
			DatabaseName = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbProjectionStore<TestProjection>(invalidOptions, _logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbProjectionStoreOptions
		{
			CollectionName = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbProjectionStore<TestProjection>(invalidOptions, _logger));
	}

	#endregion Simple Constructor Tests

	#region Client Constructor Tests

	[Fact]
	public void ClientConstructor_WithValidClient_CreatesInstance()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var database = A.Fake<IMongoDatabase>();

		_ = A.CallTo(() => client.GetDatabase(_options.Value.DatabaseName, null))
			.Returns(database);

		// Act
		var store = new MongoDbProjectionStore<TestProjection>(client, _options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void ClientConstructor_WithNullClient_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbProjectionStore<TestProjection>(client: null!, _options, _logger));
		exception.ParamName.ShouldBe("client");
	}

	[Fact]
	public void ClientConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbProjectionStore<TestProjection>(client, options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void ClientConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbProjectionStore<TestProjection>(client, _options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Client Constructor Tests

	#region Constructor Equivalence Tests

	[Fact]
	public void BothConstructors_CreateValidInstances()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var database = A.Fake<IMongoDatabase>();

		_ = A.CallTo(() => client.GetDatabase(_options.Value.DatabaseName, null))
			.Returns(database);

		// Act
		var simpleStore = new MongoDbProjectionStore<TestProjection>(_options, _logger);
		var clientStore = new MongoDbProjectionStore<TestProjection>(client, _options, _logger);

		// Assert - Both create valid instances
		_ = simpleStore.ShouldNotBeNull();
		_ = clientStore.ShouldNotBeNull();
	}

	#endregion Constructor Equivalence Tests

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = new MongoDbProjectionStore<TestProjection>(_options, _logger);

		// Act & Assert - Should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_SetsDisposedState()
	{
		// Arrange
		var store = new MongoDbProjectionStore<TestProjection>(_options, _logger);

		// Act
		await store.DisposeAsync();

		// Assert - GetByIdAsync should throw ObjectDisposedException
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.GetByIdAsync("test-id", CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var store = new MongoDbProjectionStore<TestProjection>(_options, _logger);
		await store.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.GetByIdAsync("test-id", CancellationToken.None));
	}

	[Fact]
	public async Task UpsertAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var store = new MongoDbProjectionStore<TestProjection>(_options, _logger);
		await store.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.UpsertAsync("test-id", new TestProjection(), CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var store = new MongoDbProjectionStore<TestProjection>(_options, _logger);
		await store.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.DeleteAsync("test-id", CancellationToken.None));
	}

	[Fact]
	public async Task QueryAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var store = new MongoDbProjectionStore<TestProjection>(_options, _logger);
		await store.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.QueryAsync(null, null, CancellationToken.None));
	}

	[Fact]
	public async Task CountAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var store = new MongoDbProjectionStore<TestProjection>(_options, _logger);
		await store.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.CountAsync(null, CancellationToken.None));
	}

	#endregion Dispose Tests
}

/// <summary>
/// Test projection for verifying store operations.
/// </summary>
public sealed class TestProjection
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public decimal Amount { get; set; }
}
