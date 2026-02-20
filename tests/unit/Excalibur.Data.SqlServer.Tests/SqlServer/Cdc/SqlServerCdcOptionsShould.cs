// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerCdcOptionsShould
{
	[Fact]
	public void HaveDefaultSchemaName()
	{
		var options = new SqlServerCdcOptions();

		options.SchemaName.ShouldBe("Cdc");
	}

	[Fact]
	public void HaveDefaultStateTableName()
	{
		var options = new SqlServerCdcOptions();

		options.StateTableName.ShouldBe("CdcProcessingState");
	}

	[Fact]
	public void ComputeQualifiedTableName()
	{
		var options = new SqlServerCdcOptions();

		options.QualifiedTableName.ShouldBe("[Cdc].[CdcProcessingState]");
	}

	[Fact]
	public void ComputeQualifiedTableNameWithCustomValues()
	{
		var options = new SqlServerCdcOptions
		{
			SchemaName = "myschema",
			StateTableName = "mytable"
		};

		options.QualifiedTableName.ShouldBe("[myschema].[mytable]");
	}

	[Fact]
	public void HaveDefaultPollingInterval()
	{
		var options = new SqlServerCdcOptions();

		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveDefaultBatchSize()
	{
		var options = new SqlServerCdcOptions();

		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultCommandTimeout()
	{
		var options = new SqlServerCdcOptions();

		options.CommandTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveNullConnectionStringByDefault()
	{
		var options = new SqlServerCdcOptions();

		options.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		var options = new SqlServerCdcOptions();

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenSchemaNameIsEmpty()
	{
		var options = new SqlServerCdcOptions { SchemaName = "" };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenStateTableNameIsEmpty()
	{
		var options = new SqlServerCdcOptions { StateTableName = "" };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenPollingIntervalIsZero()
	{
		var options = new SqlServerCdcOptions { PollingInterval = TimeSpan.Zero };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenBatchSizeIsZero()
	{
		var options = new SqlServerCdcOptions { BatchSize = 0 };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenCommandTimeoutIsZero()
	{
		var options = new SqlServerCdcOptions { CommandTimeout = TimeSpan.Zero };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void AllowCustomConnectionString()
	{
		var options = new SqlServerCdcOptions { ConnectionString = "Server=localhost" };

		options.ConnectionString.ShouldBe("Server=localhost");
	}
}
