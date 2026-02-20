// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Inbox;
using Excalibur.Dispatch.Abstractions;

using MongoDB.Driver;

#pragma warning disable CA1859 // Concrete type not needed - using interface for DI pattern testing

namespace Excalibur.Data.Tests.MongoDB.Inbox;

/// <summary>
/// Unit tests for the <see cref="MongoDbInboxStore"/> class.
/// </summary>
/// <remarks>
/// Sprint 512 (S512.3): MongoDB unit tests.
/// Tests focus on constructor validation and dual-constructor pattern consistency.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "MongoDB")]
[Trait("Feature", "Inbox")]
public sealed class MongoDbInboxStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<MongoDbInboxStore> _logger;
	private readonly IOptions<MongoDbInboxOptions> _options;

	public MongoDbInboxStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<MongoDbInboxStore>>();
		_options = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_inbox"
		});
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidOptions_CreatesInstance()
	{
		// Act
		var store = new MongoDbInboxStore(_options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbInboxStore(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbInboxStore(_options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void SimpleConstructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbInboxStore(invalidOptions, _logger));
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
		var store = new MongoDbInboxStore(client, _options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void ClientConstructor_WithNullClient_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbInboxStore(client: null!, _options, _logger));
		exception.ParamName.ShouldBe("client");
	}

	[Fact]
	public void ClientConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbInboxStore(client, options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void ClientConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbInboxStore(client, _options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void ClientConstructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var invalidOptions = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbInboxStore(client, invalidOptions, _logger));
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
		var simpleStore = new MongoDbInboxStore(_options, _logger);
		var clientStore = new MongoDbInboxStore(client, _options, _logger);

		// Assert - Both create valid instances
		_ = simpleStore.ShouldNotBeNull();
		_ = clientStore.ShouldNotBeNull();
	}

	#endregion Constructor Equivalence Tests

	#region Interface Implementation Tests

	[Fact]
	public void Store_ImplementsIInboxStore()
	{
		// Act
		var store = new MongoDbInboxStore(_options, _logger);

		// Assert
		_ = store.ShouldBeAssignableTo<IInboxStore>();
	}

	[Fact]
	public void Store_ImplementsIAsyncDisposable()
	{
		// Act
		var store = new MongoDbInboxStore(_options, _logger);

		// Assert
		_ = store.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion Interface Implementation Tests

	#region Options Validation Tests

	[Fact]
	public void SimpleConstructor_WithEmptyDatabaseName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = string.Empty,
			CollectionName = "test_inbox"
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbInboxStore(invalidOptions, _logger));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbInboxStore(invalidOptions, _logger));
	}

	#endregion Options Validation Tests

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = new MongoDbInboxStore(_options, _logger);

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
		var defaultOptions = Options.Create(new MongoDbInboxOptions());

		// Act
		var store = new MongoDbInboxStore(defaultOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomTtl_CreatesInstance()
	{
		// Arrange
		var customOptions = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_inbox",
			DefaultTtlSeconds = 86400 // 1 day
		});

		// Act
		var store = new MongoDbInboxStore(customOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithZeroTtl_CreatesInstance()
	{
		// Arrange - Zero TTL means no expiration
		var customOptions = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_inbox",
			DefaultTtlSeconds = 0
		});

		// Act
		var store = new MongoDbInboxStore(customOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithSslEnabled_CreatesInstance()
	{
		// Arrange
		var sslOptions = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_inbox",
			UseSsl = true
		});

		// Act
		var store = new MongoDbInboxStore(sslOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomPoolSize_CreatesInstance()
	{
		// Arrange
		var customOptions = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_inbox",
			MaxPoolSize = 50
		});

		// Act
		var store = new MongoDbInboxStore(customOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Configuration Tests
}
