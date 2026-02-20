// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Snapshots;

using Microsoft.Extensions.Options;

using Npgsql;

using Excalibur.Data.Postgres;

namespace Excalibur.Data.Tests.Postgres.Snapshots;

/// <summary>
/// Unit tests for the <see cref="PostgresSnapshotStore"/> dual-constructor pattern.
/// Verifies both simple (connection string) and advanced (connection factory) constructors.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostgresSnapshotStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<PostgresSnapshotStore> _logger;
	private readonly IOptions<PostgresSnapshotStoreOptions> _options;

	public PostgresSnapshotStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<PostgresSnapshotStore>>();
		_options = Options.Create(new PostgresSnapshotStoreOptions());
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidConnectionString_CreatesInstance()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";

		// Act
		var store = new PostgresSnapshotStore(connectionString, _options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PostgresSnapshotStore(connectionString: null!, _options, _logger));
		exception.ParamName.ShouldBe("connectionString");
	}

	[Fact]
	public void SimpleConstructor_WithEmptyConnectionString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PostgresSnapshotStore(connectionString: string.Empty, _options, _logger));
		exception.ParamName.ShouldBe("connectionString");
	}

	[Fact]
	public void SimpleConstructor_WithWhitespaceConnectionString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PostgresSnapshotStore(connectionString: "   ", _options, _logger));
		exception.ParamName.ShouldBe("connectionString");
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSnapshotStore(connectionString, options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSnapshotStore(connectionString, _options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Simple Constructor Tests

	#region Factory Constructor Tests

	[Fact]
	public void FactoryConstructor_WithValidFactory_CreatesInstance()
	{
		// Arrange
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection("Host=localhost;Database=test;");

		// Act
		var store = new PostgresSnapshotStore(factory, _options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void FactoryConstructor_WithNullFactory_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSnapshotStore(connectionFactory: null!, _options, _logger));
		exception.ParamName.ShouldBe("connectionFactory");
	}

	[Fact]
	public void FactoryConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection("Host=localhost;Database=test;");

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSnapshotStore(factory, options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void FactoryConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection("Host=localhost;Database=test;");

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new PostgresSnapshotStore(factory, _options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Factory Constructor Tests

	#region Constructor Equivalence Tests

	[Fact]
	public void BothConstructors_CreateValidInstances()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection(connectionString);

		// Act
		var simpleStore = new PostgresSnapshotStore(connectionString, _options, _logger);
		var factoryStore = new PostgresSnapshotStore(factory, _options, _logger);

		// Assert - Both create valid instances
		_ = simpleStore.ShouldNotBeNull();
		_ = factoryStore.ShouldNotBeNull();
	}

	#endregion Constructor Equivalence Tests
}
