// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Excalibur.Data.Postgres.Saga;

using Microsoft.Extensions.Options;

using Npgsql;

using Excalibur.Data.Postgres;

namespace Excalibur.Data.Tests.Postgres.Saga;

/// <summary>
/// Unit tests for the <see cref="PostgresSagaStore"/> dual-constructor pattern.
/// Verifies both simple (options-based) and advanced (connection factory) constructors.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostgresSagaStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<PostgresSagaStore> _logger;
	private readonly IOptions<PostgresSagaOptions> _options;
	private readonly IJsonSerializer _serializer;

	public PostgresSagaStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<PostgresSagaStore>>();
		_serializer = A.Fake<IJsonSerializer>();
		_options = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;Database=test;",
			Schema = "dispatch",
			TableName = "sagas"
		});
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidOptions_CreatesInstance()
	{
		// Arrange & Act
		var store = new PostgresSagaStore(_options, _logger, _serializer);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSagaStore(options: null!, _logger, _serializer));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSagaStore(_options, logger: null!, _serializer));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void SimpleConstructor_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSagaStore(_options, _logger, serializer: null!));
		exception.ParamName.ShouldBe("serializer");
	}

	[Fact]
	public void SimpleConstructor_WithEmptyConnectionString_ThrowsArgumentException()
	{
		// Arrange
		var invalidOptions = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new PostgresSagaStore(invalidOptions, _logger, _serializer));
	}

	[Fact]
	public void SimpleConstructor_WithEmptySchema_ThrowsArgumentException()
	{
		// Arrange
		var invalidOptions = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			Schema = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new PostgresSagaStore(invalidOptions, _logger, _serializer));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyTableName_ThrowsArgumentException()
	{
		// Arrange
		var invalidOptions = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			TableName = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new PostgresSagaStore(invalidOptions, _logger, _serializer));
	}

	[Fact]
	public void SimpleConstructor_WithInvalidTimeout_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var invalidOptions = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			CommandTimeoutSeconds = 0
		});

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new PostgresSagaStore(invalidOptions, _logger, _serializer));
	}

	#endregion Simple Constructor Tests

	#region Factory Constructor Tests

	[Fact]
	public void FactoryConstructor_WithValidFactory_CreatesInstance()
	{
		// Arrange
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection("Host=localhost;");
		var factoryOptions = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			Schema = "dispatch",
			TableName = "sagas"
		};

		// Act
		var store = new PostgresSagaStore(factory, factoryOptions, _logger, _serializer);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void FactoryConstructor_WithNullFactory_ThrowsArgumentNullException()
	{
		// Arrange
		var factoryOptions = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			Schema = "dispatch",
			TableName = "sagas"
		};

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSagaStore(connectionFactory: null!, factoryOptions, _logger, _serializer));
		exception.ParamName.ShouldBe("connectionFactory");
	}

	[Fact]
	public void FactoryConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection("Host=localhost;");

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSagaStore(factory, options: null!, _logger, _serializer));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void FactoryConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection("Host=localhost;");
		var factoryOptions = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			Schema = "dispatch",
			TableName = "sagas"
		};

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSagaStore(factory, factoryOptions, logger: null!, _serializer));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void FactoryConstructor_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Arrange
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection("Host=localhost;");
		var factoryOptions = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			Schema = "dispatch",
			TableName = "sagas"
		};

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSagaStore(factory, factoryOptions, _logger, serializer: null!));
		exception.ParamName.ShouldBe("serializer");
	}

	#endregion Factory Constructor Tests

	#region Constructor Equivalence Tests

	[Fact]
	public void BothConstructors_CreateValidInstances()
	{
		// Arrange
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection("Host=localhost;");
		var factoryOptions = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			Schema = "dispatch",
			TableName = "sagas"
		};

		// Act
		var simpleStore = new PostgresSagaStore(_options, _logger, _serializer);
		var factoryStore = new PostgresSagaStore(factory, factoryOptions, _logger, _serializer);

		// Assert - Both create valid instances
		_ = simpleStore.ShouldNotBeNull();
		_ = factoryStore.ShouldNotBeNull();
	}

	#endregion Constructor Equivalence Tests
}
