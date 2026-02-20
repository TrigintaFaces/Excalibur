// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerDeadLetterQueueOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerDeadLetterQueueOptionsShould : UnitTestBase
{
	#region Default Value Tests

	[Fact]
	public void HaveEmptyConnectionStringByDefault()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterQueueOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultTableName()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterQueueOptions();

		// Assert
		options.TableName.ShouldBe("DeadLetterQueue");
	}

	[Fact]
	public void HaveDefaultSchemaName()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterQueueOptions();

		// Assert
		options.SchemaName.ShouldBe("dbo");
	}

	[Fact]
	public void HaveDefaultCommandTimeoutSeconds()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterQueueOptions();

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultRetentionPeriodOf30Days()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterQueueOptions();

		// Assert
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	#endregion

	#region Property Setting Tests

	[Fact]
	public void AllowCustomConnectionString()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions();

		// Act
		options.ConnectionString = "Server=localhost;Database=DeadLetters";

		// Assert
		options.ConnectionString.ShouldBe("Server=localhost;Database=DeadLetters");
	}

	[Fact]
	public void AllowCustomTableName()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions();

		// Act
		options.TableName = "FailedMessages";

		// Assert
		options.TableName.ShouldBe("FailedMessages");
	}

	[Fact]
	public void AllowCustomSchemaName()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions();

		// Act
		options.SchemaName = "messaging";

		// Assert
		options.SchemaName.ShouldBe("messaging");
	}

	[Fact]
	public void AllowCustomCommandTimeout()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions();

		// Act
		options.CommandTimeoutSeconds = 120;

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(120);
	}

	[Fact]
	public void AllowCustomRetentionPeriod()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions();

		// Act
		options.DefaultRetentionPeriod = TimeSpan.FromDays(90);

		// Assert
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromDays(90));
	}

	#endregion

	#region QualifiedTableName Tests

	[Fact]
	public void GenerateDefaultQualifiedTableName()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions();

		// Act & Assert
		options.QualifiedTableName.ShouldBe("[dbo].[DeadLetterQueue]");
	}

	[Fact]
	public void GenerateCustomQualifiedTableName()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions
		{
			SchemaName = "messaging",
			TableName = "FailedMessages"
		};

		// Act & Assert
		options.QualifiedTableName.ShouldBe("[messaging].[FailedMessages]");
	}

	[Fact]
	public void UpdateQualifiedTableNameWhenSchemaChanges()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions();

		// Act
		options.SchemaName = "archive";

		// Assert
		options.QualifiedTableName.ShouldBe("[archive].[DeadLetterQueue]");
	}

	[Fact]
	public void UpdateQualifiedTableNameWhenTableNameChanges()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions();

		// Act
		options.TableName = "Errors";

		// Assert
		options.QualifiedTableName.ShouldBe("[dbo].[Errors]");
	}

	#endregion

	#region Full Configuration Tests

	[Fact]
	public void AllowFullCustomConfiguration()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterQueueOptions
		{
			ConnectionString = "Server=prod;Database=DLQ",
			TableName = "PoisonQueue",
			SchemaName = "errors",
			CommandTimeoutSeconds = 60,
			DefaultRetentionPeriod = TimeSpan.FromDays(7)
		};

		// Assert
		options.ConnectionString.ShouldBe("Server=prod;Database=DLQ");
		options.TableName.ShouldBe("PoisonQueue");
		options.SchemaName.ShouldBe("errors");
		options.CommandTimeoutSeconds.ShouldBe(60);
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
		options.QualifiedTableName.ShouldBe("[errors].[PoisonQueue]");
	}

	#endregion
}
