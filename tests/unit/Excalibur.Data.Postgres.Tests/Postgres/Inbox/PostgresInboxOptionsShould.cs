// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Inbox;

namespace Excalibur.Data.Tests.Postgres.Inbox;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresInboxOptionsShould
{
	[Fact]
	public void HaveDefaultConnectionString()
	{
		var options = new PostgresInboxOptions();

		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultSchemaName()
	{
		var options = new PostgresInboxOptions();

		options.SchemaName.ShouldBe("public");
	}

	[Fact]
	public void HaveDefaultTableName()
	{
		var options = new PostgresInboxOptions();

		options.TableName.ShouldBe("inbox_messages");
	}

	[Fact]
	public void HaveDefaultCommandTimeout()
	{
		var options = new PostgresInboxOptions();

		options.CommandTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultMaxRetryCount()
	{
		var options = new PostgresInboxOptions();

		options.MaxRetryCount.ShouldBe(3);
	}

	[Fact]
	public void ComputeQualifiedTableName()
	{
		var options = new PostgresInboxOptions();

		options.QualifiedTableName.ShouldBe("\"public\".\"inbox_messages\"");
	}

	[Fact]
	public void ComputeCustomQualifiedTableName()
	{
		var options = new PostgresInboxOptions
		{
			SchemaName = "myschema",
			TableName = "my_inbox"
		};

		options.QualifiedTableName.ShouldBe("\"myschema\".\"my_inbox\"");
	}
}
