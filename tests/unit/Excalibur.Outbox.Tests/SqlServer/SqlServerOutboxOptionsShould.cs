// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerOutboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerOutboxOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveEmptyConnectionStringByDefault()
	{
		// Arrange & Act
		var options = new SqlServerOutboxOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultSchemaName()
	{
		// Arrange & Act
		var options = new SqlServerOutboxOptions();

		// Assert
		options.SchemaName.ShouldBe("dbo");
	}

	[Fact]
	public void HaveDefaultTableName()
	{
		// Arrange & Act
		var options = new SqlServerOutboxOptions();

		// Assert
		options.OutboxTableName.ShouldBe("OutboxMessages");
	}

	[Fact]
	public void HaveDefaultTransportsTableName()
	{
		// Arrange & Act
		var options = new SqlServerOutboxOptions();

		// Assert
		options.TransportsTableName.ShouldBe("OutboxMessageTransports");
	}

	[Fact]
	public void HaveDefaultCommandTimeoutSeconds()
	{
		// Arrange & Act
		var options = new SqlServerOutboxOptions();

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultMaxRetryCount()
	{
		// Arrange & Act
		var options = new SqlServerOutboxOptions();

		// Assert
		options.MaxRetryCount.ShouldBe(3);
	}

	[Fact]
	public void AllowCustomConnectionString()
	{
		// Arrange
		var options = new SqlServerOutboxOptions();

		// Act
		options.ConnectionString = "Server=localhost;Database=TestDb";

		// Assert
		options.ConnectionString.ShouldBe("Server=localhost;Database=TestDb");
	}

	[Fact]
	public void AllowCustomSchemaName()
	{
		// Arrange
		var options = new SqlServerOutboxOptions();

		// Act
		options.SchemaName = "outbox";

		// Assert
		options.SchemaName.ShouldBe("outbox");
	}

	[Fact]
	public void AllowCustomTableName()
	{
		// Arrange
		var options = new SqlServerOutboxOptions();

		// Act
		options.OutboxTableName = "CustomOutbox";

		// Assert
		options.OutboxTableName.ShouldBe("CustomOutbox");
	}

	[Fact]
	public void AllowCustomTransportsTableName()
	{
		// Arrange
		var options = new SqlServerOutboxOptions();

		// Act
		options.TransportsTableName = "CustomTransports";

		// Assert
		options.TransportsTableName.ShouldBe("CustomTransports");
	}

	[Fact]
	public void AllowCustomCommandTimeout()
	{
		// Arrange
		var options = new SqlServerOutboxOptions();

		// Act
		options.CommandTimeoutSeconds = 60;

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowCustomMaxRetryCount()
	{
		// Arrange
		var options = new SqlServerOutboxOptions();

		// Act
		options.MaxRetryCount = 5;

		// Assert
		options.MaxRetryCount.ShouldBe(5);
	}

	[Fact]
	public void GenerateCorrectQualifiedOutboxTableName()
	{
		// Arrange
		var options = new SqlServerOutboxOptions
		{
			SchemaName = "outbox",
			OutboxTableName = "Messages"
		};

		// Act & Assert
		options.QualifiedOutboxTableName.ShouldBe("[outbox].[Messages]");
	}

	[Fact]
	public void GenerateCorrectQualifiedTransportsTableName()
	{
		// Arrange
		var options = new SqlServerOutboxOptions
		{
			SchemaName = "outbox",
			TransportsTableName = "Transports"
		};

		// Act & Assert
		options.QualifiedTransportsTableName.ShouldBe("[outbox].[Transports]");
	}

	[Fact]
	public void GenerateDefaultQualifiedTableNames()
	{
		// Arrange
		var options = new SqlServerOutboxOptions();

		// Act & Assert
		options.QualifiedOutboxTableName.ShouldBe("[dbo].[OutboxMessages]");
		options.QualifiedTransportsTableName.ShouldBe("[dbo].[OutboxMessageTransports]");
	}
}
