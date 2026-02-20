// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.ErrorHandling;

namespace Excalibur.Data.Tests.SqlServer.ErrorHandling;

/// <summary>
/// Unit tests for <see cref="SqlServerDeadLetterOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "ErrorHandling")]
public sealed class SqlServerDeadLetterOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveEmptyConnectionString_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDboSchemaName_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterOptions();

		// Assert
		options.SchemaName.ShouldBe("dbo");
	}

	[Fact]
	public void HaveDeadLetterMessagesTableName_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterOptions();

		// Assert
		options.TableName.ShouldBe("DeadLetterMessages");
	}

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=TestDb;";

		// Act
		var options = new SqlServerDeadLetterOptions
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
		var options = new SqlServerDeadLetterOptions
		{
			SchemaName = "errors"
		};

		// Assert
		options.SchemaName.ShouldBe("errors");
	}

	[Fact]
	public void AllowSettingTableName()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterOptions
		{
			TableName = "FailedMessages"
		};

		// Assert
		options.TableName.ShouldBe("FailedMessages");
	}
}
