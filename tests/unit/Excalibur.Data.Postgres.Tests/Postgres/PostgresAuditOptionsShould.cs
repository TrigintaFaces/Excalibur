// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Audit;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresAuditOptionsShould
{
	[Fact]
	public void HaveDefaultConnectionString()
	{
		var options = new PostgresAuditOptions();
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultSchemaName()
	{
		var options = new PostgresAuditOptions();
		options.SchemaName.ShouldBe("audit");
	}

	[Fact]
	public void HaveDefaultTableName()
	{
		var options = new PostgresAuditOptions();
		options.TableName.ShouldBe("audit_events");
	}

	[Fact]
	public void HaveDefaultAutoCreateTable()
	{
		var options = new PostgresAuditOptions();
		options.AutoCreateTable.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultCommandTimeoutSeconds()
	{
		var options = new PostgresAuditOptions();
		options.CommandTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void AllowCustomConnectionString()
	{
		var options = new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=mydb;"
		};
		options.ConnectionString.ShouldBe("Host=localhost;Database=mydb;");
	}

	[Fact]
	public void AllowCustomSchemaName()
	{
		var options = new PostgresAuditOptions { SchemaName = "custom_audit" };
		options.SchemaName.ShouldBe("custom_audit");
	}

	[Fact]
	public void AllowCustomTableName()
	{
		var options = new PostgresAuditOptions { TableName = "custom_events" };
		options.TableName.ShouldBe("custom_events");
	}

	[Fact]
	public void AllowCustomCommandTimeout()
	{
		var options = new PostgresAuditOptions { CommandTimeoutSeconds = 60 };
		options.CommandTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowCustomAutoCreateTable()
	{
		var options = new PostgresAuditOptions { AutoCreateTable = false };
		options.AutoCreateTable.ShouldBeFalse();
	}

	[Fact]
	public void Validate_ThrowsWhenConnectionStringIsEmpty()
	{
		var options = new PostgresAuditOptions();

		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_ThrowsWhenConnectionStringIsWhitespace()
	{
		var options = new PostgresAuditOptions { ConnectionString = "   " };

		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_ThrowsWhenSchemaNameIsEmpty()
	{
		var options = new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;",
			SchemaName = string.Empty
		};

		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("SchemaName");
	}

	[Fact]
	public void Validate_ThrowsWhenTableNameIsEmpty()
	{
		var options = new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;",
			TableName = string.Empty
		};

		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("TableName");
	}

	[Fact]
	public void Validate_SucceedsWithValidOptions()
	{
		var options = new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=mydb;"
		};

		Should.NotThrow(() => options.Validate());
	}
}
