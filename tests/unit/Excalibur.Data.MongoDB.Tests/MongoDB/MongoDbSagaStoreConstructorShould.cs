// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Excalibur.Data.MongoDB.Saga;

using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Saga;

/// <summary>
/// Unit tests for the <see cref="MongoDbSagaStore"/> dual-constructor pattern.
/// Verifies both simple (options-based) and advanced (IMongoClient) constructors.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MongoDbSagaStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<MongoDbSagaStore> _logger;
	private readonly IOptions<MongoDbSagaOptions> _options;
	private readonly IJsonSerializer _serializer;

	public MongoDbSagaStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<MongoDbSagaStore>>();
		_serializer = A.Fake<IJsonSerializer>();
		_options = Options.Create(new MongoDbSagaOptions());
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidOptions_CreatesInstance()
	{
		// Arrange & Act
		var store = new MongoDbSagaStore(_options, _logger, _serializer);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSagaStore(options: null!, _logger, _serializer));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSagaStore(_options, logger: null!, _serializer));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void SimpleConstructor_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSagaStore(_options, _logger, serializer: null!));
		exception.ParamName.ShouldBe("serializer");
	}

	[Fact]
	public void SimpleConstructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbSagaOptions
		{
			ConnectionString = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbSagaStore(invalidOptions, _logger, _serializer));
	}

	[Fact]
	public void SimpleConstructor_WithNullDatabaseName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbSagaOptions
		{
			DatabaseName = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbSagaStore(invalidOptions, _logger, _serializer));
	}

	[Fact]
	public void SimpleConstructor_WithNullCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbSagaOptions
		{
			CollectionName = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbSagaStore(invalidOptions, _logger, _serializer));
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
		var store = new MongoDbSagaStore(client, _options, _logger, _serializer);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void ClientConstructor_WithNullClient_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSagaStore(client: null!, _options, _logger, _serializer));
		exception.ParamName.ShouldBe("client");
	}

	[Fact]
	public void ClientConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSagaStore(client, options: null!, _logger, _serializer));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void ClientConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSagaStore(client, _options, logger: null!, _serializer));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void ClientConstructor_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSagaStore(client, _options, _logger, serializer: null!));
		exception.ParamName.ShouldBe("serializer");
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
		var simpleStore = new MongoDbSagaStore(_options, _logger, _serializer);
		var clientStore = new MongoDbSagaStore(client, _options, _logger, _serializer);

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
		var store = new MongoDbSagaStore(_options, _logger, _serializer);

		// Act & Assert - Should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_SetsDisposedState()
	{
		// Arrange
		var store = new MongoDbSagaStore(_options, _logger, _serializer);

		// Act
		await store.DisposeAsync();

		// Assert - LoadAsync should throw ObjectDisposedException
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None));
	}

	#endregion Dispose Tests

	/// <summary>
	/// Test saga state for verifying store operations.
	/// </summary>
	private sealed class TestSagaState : Dispatch.Abstractions.Messaging.SagaState
	{
		public string OrderId { get; set; } = string.Empty;
	}
}
