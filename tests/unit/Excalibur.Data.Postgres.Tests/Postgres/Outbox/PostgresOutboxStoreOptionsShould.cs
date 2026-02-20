// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Outbox;

namespace Excalibur.Data.Tests.Postgres.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxStoreOptionsShould
{
	[Fact]
	public void HaveDefaultSchemaName()
	{
		var options = new PostgresOutboxStoreOptions();

		options.SchemaName.ShouldBe("public");
	}

	[Fact]
	public void HaveDefaultOutboxTableName()
	{
		var options = new PostgresOutboxStoreOptions();

		options.OutboxTableName.ShouldBe("outbox");
	}

	[Fact]
	public void HaveDefaultDeadLetterTableName()
	{
		var options = new PostgresOutboxStoreOptions();

		options.DeadLetterTableName.ShouldBe("outbox_dead_letters");
	}

	[Fact]
	public void ComputeQualifiedOutboxTableName()
	{
		var options = new PostgresOutboxStoreOptions();

		options.QualifiedOutboxTableName.ShouldBe("\"public\".\"outbox\"");
	}

	[Fact]
	public void ComputeQualifiedDeadLetterTableName()
	{
		var options = new PostgresOutboxStoreOptions();

		options.QualifiedDeadLetterTableName.ShouldBe("\"public\".\"outbox_dead_letters\"");
	}

	[Fact]
	public void ComputeCustomQualifiedTableNames()
	{
		var options = new PostgresOutboxStoreOptions
		{
			SchemaName = "custom",
			OutboxTableName = "my_outbox",
			DeadLetterTableName = "my_dead_letters"
		};

		options.QualifiedOutboxTableName.ShouldBe("\"custom\".\"my_outbox\"");
		options.QualifiedDeadLetterTableName.ShouldBe("\"custom\".\"my_dead_letters\"");
	}

	[Fact]
	public void HaveDefaultReservationTimeout()
	{
		var options = new PostgresOutboxStoreOptions();

		options.ReservationTimeout.ShouldBe(300);
	}
}
