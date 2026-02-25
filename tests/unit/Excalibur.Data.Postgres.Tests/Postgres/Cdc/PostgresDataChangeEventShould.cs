// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Cdc;

namespace Excalibur.Data.Tests.Postgres.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresDataChangeEventShould
{
	private static readonly PostgresCdcPosition TestPosition = new(12345UL);
	private static readonly DateTimeOffset TestCommitTime = new(2026, 2, 14, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void CreateInsertEvent()
	{
		var changes = new List<PostgresDataChange>
		{
			new() { ColumnName = "id", DataType = "int4", NewValue = 1, IsPrimaryKey = true },
			new() { ColumnName = "name", DataType = "text", NewValue = "test" }
		};

		var evt = PostgresDataChangeEvent.CreateInsert(
			TestPosition, "public", "orders", 42, TestCommitTime, changes);

		evt.ChangeType.ShouldBe(PostgresDataChangeType.Insert);
		evt.SchemaName.ShouldBe("public");
		evt.TableName.ShouldBe("orders");
		evt.FullTableName.ShouldBe("public.orders");
		evt.TransactionId.ShouldBe(42U);
		evt.CommitTime.ShouldBe(TestCommitTime);
		evt.Changes.Count.ShouldBe(2);
		evt.KeyColumns.Count.ShouldBe(1);
	}

	[Fact]
	public void CreateUpdateEvent()
	{
		var changes = new List<PostgresDataChange>
		{
			new() { ColumnName = "name", DataType = "text", OldValue = "old", NewValue = "new" }
		};
		var keys = new List<PostgresDataChange>
		{
			new() { ColumnName = "id", DataType = "int4", NewValue = 1, IsPrimaryKey = true }
		};

		var evt = PostgresDataChangeEvent.CreateUpdate(
			TestPosition, "public", "orders", 43, TestCommitTime, changes, keys);

		evt.ChangeType.ShouldBe(PostgresDataChangeType.Update);
		evt.Changes.Count.ShouldBe(1);
		evt.KeyColumns.Count.ShouldBe(1);
	}

	[Fact]
	public void CreateDeleteEvent()
	{
		var keys = new List<PostgresDataChange>
		{
			new() { ColumnName = "id", DataType = "int4", OldValue = 1, IsPrimaryKey = true }
		};

		var evt = PostgresDataChangeEvent.CreateDelete(
			TestPosition, "public", "orders", 44, TestCommitTime, keys);

		evt.ChangeType.ShouldBe(PostgresDataChangeType.Delete);
		evt.KeyColumns.Count.ShouldBe(1);
		evt.Changes.Count.ShouldBe(1); // For deletes, changes = keyColumns
	}

	[Fact]
	public void CreateTruncateEvent()
	{
		var evt = PostgresDataChangeEvent.CreateTruncate(
			TestPosition, "public", "orders", 45, TestCommitTime);

		evt.ChangeType.ShouldBe(PostgresDataChangeType.Truncate);
		evt.Changes.ShouldBeEmpty();
		evt.KeyColumns.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultValues()
	{
		var evt = new PostgresDataChangeEvent();

		evt.SchemaName.ShouldBe("public");
		evt.TableName.ShouldBe(string.Empty);
		evt.Changes.ShouldBeEmpty();
		evt.KeyColumns.ShouldBeEmpty();
	}

	[Fact]
	public void ComputeFullTableName()
	{
		var evt = new PostgresDataChangeEvent
		{
			SchemaName = "myschema",
			TableName = "mytable"
		};

		evt.FullTableName.ShouldBe("myschema.mytable");
	}
}
