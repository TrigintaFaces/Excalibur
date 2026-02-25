// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Outbox;
using Excalibur.Dispatch.Abstractions;

using MongoDB.Driver;

#pragma warning disable CA1859 // Concrete type not needed - using interface for DI pattern testing

namespace Excalibur.Data.Tests.MongoDB.Outbox;

/// <summary>
/// Unit tests for the <see cref="MongoDbOutboxStore"/> class.
/// </summary>
/// <remarks>
/// Sprint 512 (S512.3): MongoDB unit tests.
/// Tests focus on constructor validation and dual-constructor pattern consistency.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "MongoDB")]
[Trait("Feature", "Outbox")]
public sealed class MongoDbOutboxStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<MongoDbOutboxStore> _logger;
	private readonly IOptions<MongoDbOutboxOptions> _options;

	public MongoDbOutboxStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<MongoDbOutboxStore>>();
		_options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_outbox"
		});
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidOptions_CreatesInstance()
	{
		// Act
		var store = new MongoDbOutboxStore(_options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbOutboxStore(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbOutboxStore(_options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void SimpleConstructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbOutboxStore(invalidOptions, _logger));
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
		var store = new MongoDbOutboxStore(client, _options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void ClientConstructor_WithNullClient_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbOutboxStore(client: null!, _options, _logger));
		exception.ParamName.ShouldBe("client");
	}

	[Fact]
	public void ClientConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbOutboxStore(client, options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void ClientConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbOutboxStore(client, _options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void ClientConstructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var invalidOptions = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbOutboxStore(client, invalidOptions, _logger));
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
		var simpleStore = new MongoDbOutboxStore(_options, _logger);
		var clientStore = new MongoDbOutboxStore(client, _options, _logger);

		// Assert - Both create valid instances
		_ = simpleStore.ShouldNotBeNull();
		_ = clientStore.ShouldNotBeNull();
	}

	#endregion Constructor Equivalence Tests

	#region Interface Implementation Tests

	[Fact]
	public void Store_ImplementsIOutboxStore()
	{
		// Act
		var store = new MongoDbOutboxStore(_options, _logger);

		// Assert
		_ = store.ShouldBeAssignableTo<IOutboxStore>();
	}

	[Fact]
	public void Store_ImplementsIAsyncDisposable()
	{
		// Act
		var store = new MongoDbOutboxStore(_options, _logger);

		// Assert
		_ = store.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion Interface Implementation Tests

	#region Options Validation Tests

	[Fact]
	public void SimpleConstructor_WithEmptyDatabaseName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = string.Empty,
			CollectionName = "test_outbox"
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbOutboxStore(invalidOptions, _logger));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbOutboxStore(invalidOptions, _logger));
	}

	#endregion Options Validation Tests

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = new MongoDbOutboxStore(_options, _logger);

		// Act & Assert - Should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	#endregion Dispose Tests

	#region Configuration Tests

	[Fact]
	public void Constructor_WithDefaultOptions_CreatesInstance()
	{
		// Arrange - Default options have valid defaults
		var defaultOptions = Options.Create(new MongoDbOutboxOptions());

		// Act
		var store = new MongoDbOutboxStore(defaultOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomTtl_CreatesInstance()
	{
		// Arrange
		var customOptions = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_outbox",
			SentMessageTtlSeconds = 86400 // 1 day
		});

		// Act
		var store = new MongoDbOutboxStore(customOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithZeroTtl_CreatesInstance()
	{
		// Arrange - Zero TTL means no expiration
		var customOptions = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_outbox",
			SentMessageTtlSeconds = 0
		});

		// Act
		var store = new MongoDbOutboxStore(customOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithSslEnabled_CreatesInstance()
	{
		// Arrange
		var sslOptions = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_outbox",
			UseSsl = true
		});

		// Act
		var store = new MongoDbOutboxStore(sslOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Configuration Tests
}
