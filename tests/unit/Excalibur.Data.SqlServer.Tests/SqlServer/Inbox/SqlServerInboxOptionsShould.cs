// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Inbox;

namespace Excalibur.Data.Tests.SqlServer.Inbox;

/// <summary>
/// Unit tests for <see cref="SqlServerInboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "Inbox")]
public sealed class SqlServerInboxOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveEmptyConnectionString_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerInboxOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDboSchemaName_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerInboxOptions();

		// Assert
		options.SchemaName.ShouldBe("dbo");
	}

	[Fact]
	public void HaveInboxMessagesTableName_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerInboxOptions();

		// Assert
		options.TableName.ShouldBe("inbox_messages");
	}

	[Fact]
	public void HaveThirtySecondCommandTimeout_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerInboxOptions();

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveThreeMaxRetryCount_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerInboxOptions();

		// Assert
		options.MaxRetryCount.ShouldBe(3);
	}

	[Fact]
	public void ReturnQualifiedTableName_WithDefaultValues()
	{
		// Arrange
		var options = new SqlServerInboxOptions();

		// Act
		var qualifiedName = options.QualifiedTableName;

		// Assert
		qualifiedName.ShouldBe("[dbo].[inbox_messages]");
	}

	[Fact]
	public void ReturnQualifiedTableName_WithCustomValues()
	{
		// Arrange
		var options = new SqlServerInboxOptions
		{
			SchemaName = "messaging",
			TableName = "custom_inbox"
		};

		// Act
		var qualifiedName = options.QualifiedTableName;

		// Assert
		qualifiedName.ShouldBe("[messaging].[custom_inbox]");
	}

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=TestDb;";

		// Act
		var options = new SqlServerInboxOptions
		{
			ConnectionString = connectionString
		};

		// Assert
		options.ConnectionString.ShouldBe(connectionString);
	}

	[Fact]
	public void AllowSettingSchemaName()
	{
		// Arrange & Act
		var options = new SqlServerInboxOptions
		{
			SchemaName = "custom_schema"
		};

		// Assert
		options.SchemaName.ShouldBe("custom_schema");
	}

	[Fact]
	public void AllowSettingTableName()
	{
		// Arrange & Act
		var options = new SqlServerInboxOptions
		{
			TableName = "custom_table"
		};

		// Assert
		options.TableName.ShouldBe("custom_table");
	}

	[Fact]
	public void AllowSettingCommandTimeoutSeconds()
	{
		// Arrange & Act
		var options = new SqlServerInboxOptions
		{
			CommandTimeoutSeconds = 60
		};

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingMaxRetryCount()
	{
		// Arrange & Act
		var options = new SqlServerInboxOptions
		{
			MaxRetryCount = 5
		};

		// Assert
		options.MaxRetryCount.ShouldBe(5);
	}
}
