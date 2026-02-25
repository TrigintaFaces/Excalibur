// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Cdc;

namespace Excalibur.Data.Tests.Postgres.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresDataChangeShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		var change = new PostgresDataChange();

		change.ColumnName.ShouldBe(string.Empty);
		change.DataType.ShouldBe(string.Empty);
		change.OldValue.ShouldBeNull();
		change.NewValue.ShouldBeNull();
		change.IsPrimaryKey.ShouldBeFalse();
	}

	[Fact]
	public void DetectChangedValues()
	{
		var change = new PostgresDataChange
		{
			OldValue = "old",
			NewValue = "new"
		};

		change.HasChanged.ShouldBeTrue();
	}

	[Fact]
	public void DetectUnchangedValues()
	{
		var change = new PostgresDataChange
		{
			OldValue = "same",
			NewValue = "same"
		};

		change.HasChanged.ShouldBeFalse();
	}

	[Fact]
	public void DetectChangeFromNull()
	{
		var change = new PostgresDataChange
		{
			OldValue = null,
			NewValue = "new"
		};

		change.HasChanged.ShouldBeTrue();
	}

	[Fact]
	public void DetectChangeToNull()
	{
		var change = new PostgresDataChange
		{
			OldValue = "old",
			NewValue = null
		};

		change.HasChanged.ShouldBeTrue();
	}

	[Fact]
	public void DetectBothNullAsUnchanged()
	{
		var change = new PostgresDataChange
		{
			OldValue = null,
			NewValue = null
		};

		change.HasChanged.ShouldBeFalse();
	}

	[Fact]
	public void StoreCustomValues()
	{
		var change = new PostgresDataChange
		{
			ColumnName = "order_id",
			DataType = "int4",
			OldValue = 1,
			NewValue = 2,
			IsPrimaryKey = true
		};

		change.ColumnName.ShouldBe("order_id");
		change.DataType.ShouldBe("int4");
		change.OldValue.ShouldBe(1);
		change.NewValue.ShouldBe(2);
		change.IsPrimaryKey.ShouldBeTrue();
	}
}
