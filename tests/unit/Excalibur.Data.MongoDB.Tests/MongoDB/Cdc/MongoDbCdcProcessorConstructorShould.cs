// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Cdc;

using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

/// <summary>
/// Unit tests for the <see cref="MongoDbCdcProcessor"/> class.
/// </summary>
/// <remarks>
/// Sprint 512 (S512.3): MongoDB unit tests.
/// Tests focus on constructor validation and options validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "MongoDB")]
[Trait("Feature", "Cdc")]
public sealed class MongoDbCdcProcessorConstructorShould : UnitTestBase
{
	private readonly IMongoClient _client;
	private readonly ILogger<MongoDbCdcProcessor> _logger;
#pragma warning disable CA2213 // FakeItEasy fake - does not need disposal
	private readonly IMongoDbCdcStateStore _stateStore;
#pragma warning restore CA2213
	private readonly IOptions<MongoDbCdcOptions> _options;

	public MongoDbCdcProcessorConstructorShould()
	{
		_client = A.Fake<IMongoClient>();
		_logger = A.Fake<ILogger<MongoDbCdcProcessor>>();
		_stateStore = A.Fake<IMongoDbCdcStateStore>();
		_options = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017/?replicaSet=rs0",
			ProcessorId = "test-processor",
			BatchSize = 100,
			MaxAwaitTime = TimeSpan.FromSeconds(5)
		});
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullClient_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbCdcProcessor(client: null!, _options, _stateStore, _logger));
		exception.ParamName.ShouldBe("client");
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbCdcProcessor(_client, options: null!, _stateStore, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void Constructor_WithNullStateStore_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbCdcProcessor(_client, _options, stateStore: null!, _logger));
		exception.ParamName.ShouldBe("stateStore");
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbCdcProcessor(_client, _options, _stateStore, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Constructor Tests

	#region Options Validation Tests

	[Fact]
	public void Constructor_WithEmptyConnectionString_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = string.Empty,
			ProcessorId = "test-processor"
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbCdcProcessor(_client, invalidOptions, _stateStore, _logger));
	}

	[Fact]
	public void Constructor_WithEmptyProcessorId_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbCdcProcessor(_client, invalidOptions, _stateStore, _logger));
	}

	[Fact]
	public void Constructor_WithZeroBatchSize_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			BatchSize = 0
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbCdcProcessor(_client, invalidOptions, _stateStore, _logger));
	}

	[Fact]
	public void Constructor_WithNegativeBatchSize_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			BatchSize = -1
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbCdcProcessor(_client, invalidOptions, _stateStore, _logger));
	}

	[Fact]
	public void Constructor_WithZeroMaxAwaitTime_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			MaxAwaitTime = TimeSpan.Zero
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbCdcProcessor(_client, invalidOptions, _stateStore, _logger));
	}

	[Fact]
	public void Constructor_WithNegativeMaxAwaitTime_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			MaxAwaitTime = TimeSpan.FromSeconds(-5)
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbCdcProcessor(_client, invalidOptions, _stateStore, _logger));
	}

	#endregion Options Validation Tests

	#region Interface Implementation Tests

	[Fact]
	public void Processor_ImplementsIMongoDbCdcProcessor()
	{
		// Act
		var processor = new MongoDbCdcProcessor(_client, _options, _stateStore, _logger);

		// Assert
		_ = processor.ShouldBeAssignableTo<IMongoDbCdcProcessor>();
	}

	#endregion Interface Implementation Tests

	#region Configuration Tests

	[Fact]
	public void Constructor_WithDefaultOptions_CreatesInstance()
	{
		// Arrange
		var defaultOptions = Options.Create(new MongoDbCdcOptions());

		// Act
		var processor = new MongoDbCdcProcessor(_client, defaultOptions, _stateStore, _logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithDatabaseFilter_CreatesInstance()
	{
		// Arrange
		var dbOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			DatabaseName = "test_db"
		});

		// Act
		var processor = new MongoDbCdcProcessor(_client, dbOptions, _stateStore, _logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCollectionFilters_CreatesInstance()
	{
		// Arrange
		var collectionOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			DatabaseName = "test_db",
			CollectionNames = ["events", "projections"]
		});

		// Act
		var processor = new MongoDbCdcProcessor(_client, collectionOptions, _stateStore, _logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithFullDocumentEnabled_CreatesInstance()
	{
		// Arrange
		var fullDocOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			FullDocument = true
		});

		// Act
		var processor = new MongoDbCdcProcessor(_client, fullDocOptions, _stateStore, _logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithFullDocumentBeforeChange_CreatesInstance()
	{
		// Arrange
		var preImageOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			FullDocumentBeforeChange = true
		});

		// Act
		var processor = new MongoDbCdcProcessor(_client, preImageOptions, _stateStore, _logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithOperationTypeFilters_CreatesInstance()
	{
		// Arrange
		var opTypeOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			OperationTypes = ["insert", "update", "delete"]
		});

		// Act
		var processor = new MongoDbCdcProcessor(_client, opTypeOptions, _stateStore, _logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithSslEnabled_CreatesInstance()
	{
		// Arrange
		var sslOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			UseSsl = true
		});

		// Act
		var processor = new MongoDbCdcProcessor(_client, sslOptions, _stateStore, _logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomReconnectInterval_CreatesInstance()
	{
		// Arrange
		var reconnectOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			ReconnectInterval = TimeSpan.FromSeconds(10)
		});

		// Act
		var processor = new MongoDbCdcProcessor(_client, reconnectOptions, _stateStore, _logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomPoolSize_CreatesInstance()
	{
		// Arrange
		var poolOptions = Options.Create(new MongoDbCdcOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			ProcessorId = "test-processor",
			MaxPoolSize = 50
		});

		// Act
		var processor = new MongoDbCdcProcessor(_client, poolOptions, _stateStore, _logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	#endregion Configuration Tests
}
