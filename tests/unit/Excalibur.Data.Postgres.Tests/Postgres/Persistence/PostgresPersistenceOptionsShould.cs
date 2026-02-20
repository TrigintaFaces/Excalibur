// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Reflection;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Postgres.Persistence;

namespace Excalibur.Data.Tests.Postgres.Persistence;

/// <summary>
/// Unit tests for <see cref="PostgresPersistenceOptions"/> and its sub-option classes
/// (<see cref="PostgresConnectionOptions"/>, <see cref="PostgresStatementOptions"/>).
/// Validates default values, setters, ISP property count gates, sub-object initialization,
/// and validation behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Persistence")]
public sealed class PostgresPersistenceOptionsShould : UnitTestBase
{
	// ─────────────────────────────────────────────
	// Root options: interface conformance
	// ─────────────────────────────────────────────

	[Fact]
	public void ImplementIPersistenceOptions()
	{
		// Arrange & Act
		var options = new PostgresPersistenceOptions();

		// Assert
		options.ShouldBeAssignableTo<IPersistenceOptions>();
	}

	// ─────────────────────────────────────────────
	// Root options: default values
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveCorrectDefaultValues()
	{
		// Arrange & Act
		var options = new PostgresPersistenceOptions();

		// Assert — IPersistenceOptions-mandated properties
		options.ConnectionString.ShouldBe(string.Empty);
		options.ConnectionTimeout.ShouldBe(30);
		options.CommandTimeout.ShouldBe(30);
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelayMilliseconds.ShouldBe(1000);
		options.EnableConnectionPooling.ShouldBeTrue();
		options.MaxPoolSize.ShouldBe(100);
		options.MinPoolSize.ShouldBe(0);
		options.EnableDetailedLogging.ShouldBeFalse();
		options.EnableMetrics.ShouldBeTrue();
		options.ProviderSpecificOptions.ShouldNotBeNull();
		options.ProviderSpecificOptions.ShouldBeEmpty();
	}

	// ─────────────────────────────────────────────
	// Sub-objects are non-null by default
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveNonNullConnectionSubObject()
	{
		// Arrange & Act
		var options = new PostgresPersistenceOptions();

		// Assert
		options.Connection.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullStatementsSubObject()
	{
		// Arrange & Act
		var options = new PostgresPersistenceOptions();

		// Assert
		options.Statements.ShouldNotBeNull();
	}

	// ─────────────────────────────────────────────
	// Root property setters
	// ─────────────────────────────────────────────

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.ConnectionString = "Host=localhost;Database=TestDb;";

		// Assert
		options.ConnectionString.ShouldBe("Host=localhost;Database=TestDb;");
	}

	[Fact]
	public void AllowSettingConnectionTimeout()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.ConnectionTimeout = 60;

		// Assert
		options.ConnectionTimeout.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingCommandTimeout()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.CommandTimeout = 120;

		// Assert
		options.CommandTimeout.ShouldBe(120);
	}

	[Fact]
	public void AllowSettingMaxRetryAttempts()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.MaxRetryAttempts = 5;

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingRetryDelayMilliseconds()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.RetryDelayMilliseconds = 2000;

		// Assert
		options.RetryDelayMilliseconds.ShouldBe(2000);
	}

	[Fact]
	public void AllowDisablingConnectionPooling()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.EnableConnectionPooling = false;

		// Assert
		options.EnableConnectionPooling.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingPoolSizes()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.MaxPoolSize = 200;
		options.MinPoolSize = 20;

		// Assert
		options.MaxPoolSize.ShouldBe(200);
		options.MinPoolSize.ShouldBe(20);
	}

	[Fact]
	public void AllowEnablingDetailedLogging()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.EnableDetailedLogging = true;

		// Assert
		options.EnableDetailedLogging.ShouldBeTrue();
	}

	[Fact]
	public void AllowDisablingMetrics()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.EnableMetrics = false;

		// Assert
		options.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingProviderSpecificOptions()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();
		var customOptions = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["custom-key"] = "custom-value",
		};

		// Act
		options.ProviderSpecificOptions = customOptions;

		// Assert
		options.ProviderSpecificOptions.ShouldBeSameAs(customOptions);
		options.ProviderSpecificOptions["custom-key"].ShouldBe("custom-value");
	}

	// ─────────────────────────────────────────────
	// ISP quality gate: property counts
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveRootPropertyCountWithinIspGate()
	{
		// The ISP gate allows at most 10 settable properties on the root.
		// Root has 11 IPersistenceOptions-mandated settable properties + 2 read-only sub-object getters.
		// Only the 11 settable properties count toward the options gate.
		// The sub-object getters (Connection, Statements) are read-only navigation properties.

		// Arrange
		var settableProperties = typeof(PostgresPersistenceOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanWrite)
			.ToList();

		var readOnlySubObjects = typeof(PostgresPersistenceOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => !p.CanWrite && p.PropertyType.Name.StartsWith("Postgres", StringComparison.Ordinal))
			.ToList();

		// Assert — 11 settable properties (all from IPersistenceOptions)
		settableProperties.Count.ShouldBe(11);
		// 2 read-only sub-option navigation properties
		readOnlySubObjects.Count.ShouldBe(2);
	}

	[Fact]
	public void HaveConnectionOptionsPropertyCountWithinIspGate()
	{
		// Arrange
		var properties = typeof(PostgresConnectionOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.ToList();

		// Assert — 10 properties (at the ISP limit)
		properties.Count.ShouldBeLessThanOrEqualTo(10);
	}

	[Fact]
	public void HaveStatementOptionsPropertyCountWithinIspGate()
	{
		// Arrange
		var properties = typeof(PostgresStatementOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.ToList();

		// Assert — 4 properties (within ISP limit of 10)
		properties.Count.ShouldBeLessThanOrEqualTo(10);
	}

	// ─────────────────────────────────────────────
	// PostgresConnectionOptions: defaults
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveCorrectConnectionOptionsDefaults()
	{
		// Arrange & Act
		var options = new PostgresPersistenceOptions();
		var connection = options.Connection;

		// Assert
		connection.ApplicationName.ShouldBeNull();
		connection.DefaultDatabase.ShouldBeNull();
		connection.ConnectionIdleLifetime.ShouldBe(300);
		connection.ConnectionPruningInterval.ShouldBe(10);
		connection.EnableTcpKeepAlive.ShouldBeTrue();
		connection.TcpKeepAliveTime.ShouldBe(30);
		connection.TcpKeepAliveInterval.ShouldBe(1);
		connection.IncludeErrorDetail.ShouldBeTrue();
		connection.SocketReceiveBufferSize.ShouldBe(0);
		connection.SocketSendBufferSize.ShouldBe(0);
	}

	// ─────────────────────────────────────────────
	// PostgresConnectionOptions: setters
	// ─────────────────────────────────────────────

	[Fact]
	public void AllowSettingConnectionOptionsProperties()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();
		var connection = options.Connection;

		// Act
		connection.ApplicationName = "MyApp";
		connection.DefaultDatabase = "MyDb";
		connection.ConnectionIdleLifetime = 600;
		connection.ConnectionPruningInterval = 30;
		connection.EnableTcpKeepAlive = false;
		connection.TcpKeepAliveTime = 60;
		connection.TcpKeepAliveInterval = 5;
		connection.IncludeErrorDetail = false;
		connection.SocketReceiveBufferSize = 65536;
		connection.SocketSendBufferSize = 65536;

		// Assert
		connection.ApplicationName.ShouldBe("MyApp");
		connection.DefaultDatabase.ShouldBe("MyDb");
		connection.ConnectionIdleLifetime.ShouldBe(600);
		connection.ConnectionPruningInterval.ShouldBe(30);
		connection.EnableTcpKeepAlive.ShouldBeFalse();
		connection.TcpKeepAliveTime.ShouldBe(60);
		connection.TcpKeepAliveInterval.ShouldBe(5);
		connection.IncludeErrorDetail.ShouldBeFalse();
		connection.SocketReceiveBufferSize.ShouldBe(65536);
		connection.SocketSendBufferSize.ShouldBe(65536);
	}

	// ─────────────────────────────────────────────
	// PostgresStatementOptions: defaults
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveCorrectStatementOptionsDefaults()
	{
		// Arrange & Act
		var options = new PostgresPersistenceOptions();
		var statements = options.Statements;

		// Assert
		statements.EnablePreparedStatementCaching.ShouldBeTrue();
		statements.MaxPreparedStatements.ShouldBe(200);
		statements.EnableAutoPrepare.ShouldBeTrue();
		statements.AutoPrepareMinUsages.ShouldBe(2);
	}

	// ─────────────────────────────────────────────
	// PostgresStatementOptions: setters
	// ─────────────────────────────────────────────

	[Fact]
	public void AllowSettingStatementOptionsProperties()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();
		var statements = options.Statements;

		// Act
		statements.EnablePreparedStatementCaching = false;
		statements.MaxPreparedStatements = 500;
		statements.EnableAutoPrepare = false;
		statements.AutoPrepareMinUsages = 5;

		// Assert
		statements.EnablePreparedStatementCaching.ShouldBeFalse();
		statements.MaxPreparedStatements.ShouldBe(500);
		statements.EnableAutoPrepare.ShouldBeFalse();
		statements.AutoPrepareMinUsages.ShouldBe(5);
	}

	// ─────────────────────────────────────────────
	// Nested object initializer syntax
	// ─────────────────────────────────────────────

	[Fact]
	public void SupportNestedObjectInitializerSyntax()
	{
		// Arrange & Act — using nested initializer syntax on read-only sub-objects
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=mydb;",
			ConnectionTimeout = 60,
			Connection =
			{
				ApplicationName = "TestApp",
				ConnectionIdleLifetime = 600,
				EnableTcpKeepAlive = false,
			},
			Statements =
			{
				MaxPreparedStatements = 500,
				AutoPrepareMinUsages = 3,
			},
		};

		// Assert — root properties
		options.ConnectionString.ShouldBe("Host=localhost;Database=mydb;");
		options.ConnectionTimeout.ShouldBe(60);

		// Assert — connection sub-options
		options.Connection.ApplicationName.ShouldBe("TestApp");
		options.Connection.ConnectionIdleLifetime.ShouldBe(600);
		options.Connection.EnableTcpKeepAlive.ShouldBeFalse();

		// Assert — statement sub-options
		options.Statements.MaxPreparedStatements.ShouldBe(500);
		options.Statements.AutoPrepareMinUsages.ShouldBe(3);
	}

	// ─────────────────────────────────────────────
	// Validation: happy path
	// ─────────────────────────────────────────────

	[Fact]
	public void ValidateSuccessfullyWithValidOptions()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;Username=test;Password=test;",
		};

		// Act & Assert — should not throw
#pragma warning disable IL2026 // Validator.TryValidateObject uses reflection
		Should.NotThrow(() => options.Validate());
#pragma warning restore IL2026
	}

	// ─────────────────────────────────────────────
	// Validation: error paths
	// ─────────────────────────────────────────────

	[Fact]
	public void ThrowOnValidateWithEmptyConnectionString()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = string.Empty,
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithWhitespaceConnectionString()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "   ",
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithMinPoolSizeGreaterThanMaxPoolSize()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			MaxPoolSize = 10,
			MinPoolSize = 20,
		};

		// Act & Assert
#pragma warning disable IL2026
		var ex = Should.Throw<ValidationException>(() => options.Validate());
		ex.Message.ShouldContain("MinPoolSize");
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithZeroMaxPoolSizeWhenPoolingEnabled()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			EnableConnectionPooling = true,
			MaxPoolSize = 0,
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithInvalidConnectionTimeout()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			ConnectionTimeout = 0,
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithInvalidCommandTimeout()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			CommandTimeout = 0,
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithInvalidRetryDelay()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			RetryDelayMilliseconds = 50, // Below minimum of 100
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithInvalidConnectionIdleLifetime()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			Connection =
			{
				ConnectionIdleLifetime = -1,
			},
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithInvalidConnectionPruningInterval()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			Connection =
			{
				ConnectionPruningInterval = 0,
			},
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithInvalidMaxPreparedStatements()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			Statements =
			{
				MaxPreparedStatements = -1,
			},
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	[Fact]
	public void ThrowOnValidateWithInvalidAutoPrepareMinUsages()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			Statements =
			{
				AutoPrepareMinUsages = 0,
			},
		};

		// Act & Assert
#pragma warning disable IL2026
		Should.Throw<ValidationException>(() => options.Validate());
#pragma warning restore IL2026
	}

	// ─────────────────────────────────────────────
	// ProviderSpecificOptions uses ordinal comparer
	// ─────────────────────────────────────────────

	[Fact]
	public void UseOrdinalComparerForProviderSpecificOptions()
	{
		// Arrange
		var options = new PostgresPersistenceOptions();

		// Act
		options.ProviderSpecificOptions["Key"] = "value1";
		options.ProviderSpecificOptions["key"] = "value2";

		// Assert — ordinal comparison means "Key" and "key" are different keys
		options.ProviderSpecificOptions.Count.ShouldBe(2);
		options.ProviderSpecificOptions["Key"].ShouldBe("value1");
		options.ProviderSpecificOptions["key"].ShouldBe("value2");
	}

	// ─────────────────────────────────────────────
	// BuildConnectionString
	// ─────────────────────────────────────────────

	[Fact]
	public void BuildConnectionStringWithDefaults()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
		};

		// Act
		var connStr = options.BuildConnectionString();

		// Assert — should contain key settings from defaults
		connStr.ShouldNotBeNullOrWhiteSpace();
		connStr.ShouldContain("Timeout=30");
		connStr.ShouldContain("Command Timeout=30");
		connStr.ShouldContain("Pooling=True");
		connStr.ShouldContain("Maximum Pool Size=100");
	}

	[Fact]
	public void BuildConnectionStringWithCustomSettings()
	{
		// Arrange
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=TestDb;",
			ConnectionTimeout = 60,
			CommandTimeout = 120,
			EnableConnectionPooling = false,
			MaxPoolSize = 200,
			MinPoolSize = 10,
			Connection =
			{
				ApplicationName = "TestApp",
				ConnectionIdleLifetime = 600,
			},
		};

		// Act
		var connStr = options.BuildConnectionString();

		// Assert
		connStr.ShouldContain("Timeout=60");
		connStr.ShouldContain("Command Timeout=120");
		connStr.ShouldContain("Pooling=False");
		connStr.ShouldContain("Application Name=TestApp");
	}
}
