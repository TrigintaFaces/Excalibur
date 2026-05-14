// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="SqlServerCdcIdempotencyFilterOptions"/>.
/// Covers defaults, QualifiedTableName, and all Validate() error paths.
/// </summary>
/// <remarks>
/// Sprint 826 — bd-cgqeih: SqlServer CDC idempotency filter options.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class SqlServerCdcIdempotencyFilterOptionsShould
{
	#region Defaults

	[Fact]
	public void HaveCorrectDefaultSchemaName()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions();
		options.SchemaName.ShouldBe("Cdc");
	}

	[Fact]
	public void HaveCorrectDefaultTableName()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions();
		options.TableName.ShouldBe("CdcProcessedEvents");
	}

	[Fact]
	public void HaveCorrectDefaultRetentionPeriod()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions();
		options.RetentionPeriod.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void HaveCorrectDefaultCleanupBatchSize()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions();
		options.CleanupBatchSize.ShouldBe(1000);
	}

	#endregion

	#region QualifiedTableName

	[Fact]
	public void ProduceCorrectQualifiedTableName_WithDefaults()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions();
		options.QualifiedTableName.ShouldBe("[Cdc].[CdcProcessedEvents]");
	}

	[Fact]
	public void ProduceCorrectQualifiedTableName_WithCustomValues()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions
		{
			SchemaName = "MySchema",
			TableName = "MyTable"
		};
		options.QualifiedTableName.ShouldBe("[MySchema].[MyTable]");
	}

	#endregion

	#region Validate — Happy Path

	[Fact]
	public void PassValidation_WithDefaultOptions()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions();

		// Act — should not throw
		options.Validate();
	}

	[Fact]
	public void PassValidation_WithCustomValidOptions()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions
		{
			SchemaName = "dbo",
			TableName = "ProcessedCdcEvents",
			RetentionPeriod = TimeSpan.FromHours(48),
			CleanupBatchSize = 5000
		};

		// Act — should not throw
		options.Validate();
	}

	#endregion

	#region Validate — Error Paths

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowInvalidOperationException_WhenSchemaNameIsNullOrWhitespace(string? schemaName)
	{
		var options = new SqlServerCdcIdempotencyFilterOptions { SchemaName = schemaName! };

		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("SchemaName");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowInvalidOperationException_WhenTableNameIsNullOrWhitespace(string? tableName)
	{
		var options = new SqlServerCdcIdempotencyFilterOptions { TableName = tableName! };

		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("TableName");
	}

	[Theory]
	[InlineData("invalid;schema")]
	[InlineData("DROP TABLE")]
	[InlineData("schema name")]
	public void ThrowInvalidOperationException_WhenSchemaNameContainsInvalidCharacters(string schemaName)
	{
		var options = new SqlServerCdcIdempotencyFilterOptions { SchemaName = schemaName };

		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("invalid characters");
	}

	[Theory]
	[InlineData("invalid;table")]
	[InlineData("DROP TABLE")]
	[InlineData("table name")]
	public void ThrowInvalidOperationException_WhenTableNameContainsInvalidCharacters(string tableName)
	{
		var options = new SqlServerCdcIdempotencyFilterOptions { TableName = tableName };

		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("invalid characters");
	}

	[Fact]
	public void ThrowInvalidOperationException_WhenRetentionPeriodIsZero()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions { RetentionPeriod = TimeSpan.Zero };

		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("RetentionPeriod");
	}

	[Fact]
	public void ThrowInvalidOperationException_WhenRetentionPeriodIsNegative()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions { RetentionPeriod = TimeSpan.FromHours(-1) };

		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("RetentionPeriod");
	}

	[Fact]
	public void ThrowInvalidOperationException_WhenCleanupBatchSizeIsZero()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions { CleanupBatchSize = 0 };

		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("CleanupBatchSize");
	}

	[Fact]
	public void ThrowInvalidOperationException_WhenCleanupBatchSizeIsNegative()
	{
		var options = new SqlServerCdcIdempotencyFilterOptions { CleanupBatchSize = -1 };

		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("CleanupBatchSize");
	}

	#endregion
}
