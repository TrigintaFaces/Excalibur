// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Cdc;

namespace Excalibur.Data.Tests.Postgres.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresCdcStateStoreOptionsShould
{
	[Fact]
	public void HaveDefaultSchemaName()
	{
		var options = new PostgresCdcStateStoreOptions();

		options.SchemaName.ShouldBe("excalibur");
	}

	[Fact]
	public void HaveDefaultTableName()
	{
		var options = new PostgresCdcStateStoreOptions();

		options.TableName.ShouldBe("cdc_state");
	}

	[Fact]
	public void ComputeQualifiedTableName()
	{
		var options = new PostgresCdcStateStoreOptions();

		options.QualifiedTableName.ShouldBe("\"excalibur\".\"cdc_state\"");
	}

	[Fact]
	public void ComputeCustomQualifiedTableName()
	{
		var options = new PostgresCdcStateStoreOptions
		{
			SchemaName = "myschema",
			TableName = "mytable"
		};

		options.QualifiedTableName.ShouldBe("\"myschema\".\"mytable\"");
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		var options = new PostgresCdcStateStoreOptions();

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenSchemaNameIsEmpty()
	{
		var options = new PostgresCdcStateStoreOptions { SchemaName = "" };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenTableNameIsEmpty()
	{
		var options = new PostgresCdcStateStoreOptions { TableName = "" };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}
}
