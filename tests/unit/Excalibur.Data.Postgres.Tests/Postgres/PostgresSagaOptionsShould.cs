// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Saga;

using Excalibur.Data.Postgres;

namespace Excalibur.Data.Tests.Postgres.Saga;

/// <summary>
/// Unit tests for <see cref="PostgresSagaOptions"/> configuration and validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostgresSagaOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultConnectionStringEmpty()
	{
		// Arrange & Act
		var options = new PostgresSagaOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultSchema()
	{
		// Arrange & Act
		var options = new PostgresSagaOptions();

		// Assert
		options.Schema.ShouldBe("dispatch");
	}

	[Fact]
	public void HaveDefaultTableName()
	{
		// Arrange & Act
		var options = new PostgresSagaOptions();

		// Assert
		options.TableName.ShouldBe("sagas");
	}

	[Fact]
	public void HaveDefaultCommandTimeout()
	{
		// Arrange & Act
		var options = new PostgresSagaOptions();

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void GenerateQualifiedTableName()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			Schema = "myschema",
			TableName = "mytable"
		};

		// Act
		var qualifiedName = options.QualifiedTableName;

		// Assert
		qualifiedName.ShouldBe("\"myschema\".\"mytable\"");
	}

	[Fact]
	public void GenerateDefaultQualifiedTableName()
	{
		// Arrange & Act
		var options = new PostgresSagaOptions();

		// Assert
		options.QualifiedTableName.ShouldBe("\"dispatch\".\"sagas\"");
	}

	#endregion Default Values Tests

	#region Property Setters Tests

	[Fact]
	public void AllowCustomConnectionString()
	{
		// Arrange & Act
		var options = new PostgresSagaOptions
		{
			ConnectionString = "Host=custom;Database=db;"
		};

		// Assert
		options.ConnectionString.ShouldBe("Host=custom;Database=db;");
	}

	[Fact]
	public void AllowCustomSchema()
	{
		// Arrange & Act
		var options = new PostgresSagaOptions
		{
			Schema = "custom_schema"
		};

		// Assert
		options.Schema.ShouldBe("custom_schema");
	}

	[Fact]
	public void AllowCustomTableName()
	{
		// Arrange & Act
		var options = new PostgresSagaOptions
		{
			TableName = "custom_sagas"
		};

		// Assert
		options.TableName.ShouldBe("custom_sagas");
	}

	[Fact]
	public void AllowCustomTimeout()
	{
		// Arrange & Act
		var options = new PostgresSagaOptions
		{
			CommandTimeoutSeconds = 60
		};

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(60);
	}

	#endregion Property Setters Tests

	#region Validation Tests

	[Fact]
	public void Validate_WithValidOptions_DoesNotThrow()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;Database=test;",
			Schema = "dispatch",
			TableName = "sagas",
			CommandTimeoutSeconds = 30
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_WithNullConnectionString_ThrowsArgumentException()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = null!
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithEmptyConnectionString_ThrowsArgumentException()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = string.Empty
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithWhitespaceConnectionString_ThrowsArgumentException()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = "   "
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNullSchema_ThrowsArgumentException()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			Schema = null!
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithEmptySchema_ThrowsArgumentException()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			Schema = string.Empty
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNullTableName_ThrowsArgumentException()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			TableName = null!
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithEmptyTableName_ThrowsArgumentException()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			TableName = string.Empty
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroTimeout_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			CommandTimeoutSeconds = 0
		};

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNegativeTimeout_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var options = new PostgresSagaOptions
		{
			ConnectionString = "Host=localhost;",
			CommandTimeoutSeconds = -5
		};

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => options.Validate());
	}

	#endregion Validation Tests
}
