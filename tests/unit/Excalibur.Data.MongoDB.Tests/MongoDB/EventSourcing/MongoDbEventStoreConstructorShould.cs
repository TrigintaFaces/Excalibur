// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.EventSourcing;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Data.Tests.MongoDB.EventSourcing;

/// <summary>
/// Unit tests for the <see cref="MongoDbEventStore"/> class.
/// </summary>
/// <remarks>
/// Sprint 512 (S512.3): MongoDB unit tests.
/// Tests focus on constructor validation following Sprint 511 patterns.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "MongoDB")]
[Trait("Feature", "EventSourcing")]
public sealed class MongoDbEventStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<MongoDbEventStore> _logger;
	private readonly IOptions<MongoDbEventStoreOptions> _options;

	public MongoDbEventStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<MongoDbEventStore>>();
		_options = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_events",
			CounterCollectionName = "test_counters"
		});
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidOptions_CreatesInstance()
	{
		// Act
		var store = new MongoDbEventStore(_options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbEventStore(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbEventStore(_options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Simple Constructor Tests

	#region Options Validation Tests

	[Fact]
	public void SimpleConstructor_WithEmptyConnectionString_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = string.Empty,
			DatabaseName = "test_db",
			CollectionName = "test_events",
			CounterCollectionName = "test_counters"
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbEventStore(invalidOptions, _logger));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyDatabaseName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = string.Empty,
			CollectionName = "test_events",
			CounterCollectionName = "test_counters"
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbEventStore(invalidOptions, _logger));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = string.Empty,
			CounterCollectionName = "test_counters"
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbEventStore(invalidOptions, _logger));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyCounterCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_events",
			CounterCollectionName = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbEventStore(invalidOptions, _logger));
	}

	#endregion Options Validation Tests

	#region Interface Implementation Tests

	[Fact]
	public void Store_ImplementsIEventStore()
	{
		// Act
		var store = new MongoDbEventStore(_options, _logger);

		// Assert
		_ = store.ShouldBeAssignableTo<IEventStore>();
	}

	[Fact]
	public void Store_ImplementsIAsyncDisposable()
	{
		// Act
		var store = new MongoDbEventStore(_options, _logger);

		// Assert
		_ = store.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion Interface Implementation Tests

	#region Constructor with Serializers Tests

	[Fact]
	public void AdvancedConstructor_WithNullSerializers_CreatesInstance()
	{
		// Act
		var store = new MongoDbEventStore(
			_options,
			_logger,
			internalSerializer: null,
			payloadSerializer: null);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void AdvancedConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbEventStore(
				options: null!,
				_logger,
				internalSerializer: null,
				payloadSerializer: null));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbEventStore(
				_options,
				logger: null!,
				internalSerializer: null,
				payloadSerializer: null));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Constructor with Serializers Tests

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = new MongoDbEventStore(_options, _logger);

		// Act & Assert - Should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_CompletesSuccessfully()
	{
		// Arrange
		var store = new MongoDbEventStore(_options, _logger);

		// Act
		await store.DisposeAsync();

		// Assert - No exception thrown indicates success
		Assert.True(true);
	}

	#endregion Dispose Tests

	#region Configuration Tests

	[Fact]
	public void Constructor_WithDefaultOptions_CreatesInstance()
	{
		// Arrange - Use default options (which have valid defaults)
		var defaultOptions = Options.Create(new MongoDbEventStoreOptions());

		// Act
		var store = new MongoDbEventStore(defaultOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomPoolSize_CreatesInstance()
	{
		// Arrange
		var customOptions = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_events",
			CounterCollectionName = "test_counters",
			MaxPoolSize = 50
		});

		// Act
		var store = new MongoDbEventStore(customOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithSslEnabled_CreatesInstance()
	{
		// Arrange
		var sslOptions = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_events",
			CounterCollectionName = "test_counters",
			UseSsl = true
		});

		// Act
		var store = new MongoDbEventStore(sslOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomTimeouts_CreatesInstance()
	{
		// Arrange
		var timeoutOptions = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test_db",
			CollectionName = "test_events",
			CounterCollectionName = "test_counters",
			ServerSelectionTimeoutSeconds = 60,
			ConnectTimeoutSeconds = 60
		});

		// Act
		var store = new MongoDbEventStore(timeoutOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Configuration Tests
}
