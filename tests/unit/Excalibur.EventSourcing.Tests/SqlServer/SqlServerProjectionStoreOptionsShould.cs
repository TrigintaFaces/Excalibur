// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.EventSourcing.SqlServer;

namespace Excalibur.EventSourcing.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class SqlServerProjectionStoreOptionsShould
{
	[Fact]
	public void DefaultConnectionStringToNull()
	{
		// Arrange & Act
		var options = new SqlServerProjectionStoreOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void DefaultTableNameToNull()
	{
		// Arrange & Act
		var options = new SqlServerProjectionStoreOptions();

		// Assert
		options.TableName.ShouldBeNull();
	}

	[Fact]
	public void DefaultJsonSerializerOptionsToNull()
	{
		// Arrange & Act
		var options = new SqlServerProjectionStoreOptions();

		// Assert
		options.JsonSerializerOptions.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var options = new SqlServerProjectionStoreOptions();

		// Act
		options.ConnectionString = "Server=localhost;Database=Test";

		// Assert
		options.ConnectionString.ShouldBe("Server=localhost;Database=Test");
	}

	[Fact]
	public void AllowSettingTableName()
	{
		// Arrange
		var options = new SqlServerProjectionStoreOptions();

		// Act
		options.TableName = "MyProjections";

		// Assert
		options.TableName.ShouldBe("MyProjections");
	}

	[Fact]
	public void AllowSettingJsonSerializerOptions()
	{
		// Arrange
		var options = new SqlServerProjectionStoreOptions();
		var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

		// Act
		options.JsonSerializerOptions = jsonOptions;

		// Assert
		options.JsonSerializerOptions.ShouldBeSameAs(jsonOptions);
	}

	[Fact]
	public void ThrowOnValidateWhenConnectionStringIsNull()
	{
		// Arrange
		var options = new SqlServerProjectionStoreOptions();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowOnValidateWhenConnectionStringIsEmpty()
	{
		// Arrange
		var options = new SqlServerProjectionStoreOptions { ConnectionString = "" };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowOnValidateWhenConnectionStringIsWhitespace()
	{
		// Arrange
		var options = new SqlServerProjectionStoreOptions { ConnectionString = "   " };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void NotThrowOnValidateWithValidConnectionString()
	{
		// Arrange
		var options = new SqlServerProjectionStoreOptions
		{
			ConnectionString = "Server=localhost;Database=Test"
		};

		// Act & Assert — should not throw
		options.Validate();
	}
}
