// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="SqlServerCdcIdempotencyFilterOptionsValidator"/>.
/// Verifies IValidateOptions behavior for all validation paths.
/// </summary>
/// <remarks>
/// Sprint 826 — bd-cgqeih: SqlServer CDC idempotency filter options validator.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class SqlServerCdcIdempotencyFilterOptionsValidatorShould
{
	private readonly SqlServerCdcIdempotencyFilterOptionsValidator _validator = new();

	#region Happy Path

	[Fact]
	public void ReturnSuccess_ForDefaultOptions()
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccess_ForCustomValidOptions()
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions
		{
			SchemaName = "dbo",
			TableName = "MyProcessedEvents",
			RetentionPeriod = TimeSpan.FromHours(72),
			CleanupBatchSize = 2000
		};

		// Act
		var result = _validator.Validate("Named", options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Null Options

	[Fact]
	public void ReturnFailure_WhenOptionsIsNull()
	{
		// Act
		var result = _validator.Validate(null, null!);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("null");
	}

	#endregion

	#region SchemaName Validation

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ReturnFailure_WhenSchemaNameIsNullOrWhitespace(string? schemaName)
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions { SchemaName = schemaName! };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	#endregion

	#region TableName Validation

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ReturnFailure_WhenTableNameIsNullOrWhitespace(string? tableName)
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions { TableName = tableName! };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	#endregion

	#region RetentionPeriod Validation

	[Fact]
	public void ReturnFailure_WhenRetentionPeriodIsZero()
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions { RetentionPeriod = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RetentionPeriod");
	}

	[Fact]
	public void ReturnFailure_WhenRetentionPeriodIsNegative()
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions { RetentionPeriod = TimeSpan.FromMinutes(-30) };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RetentionPeriod");
	}

	#endregion

	#region CleanupBatchSize Validation

	[Fact]
	public void ReturnFailure_WhenCleanupBatchSizeIsZero()
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions { CleanupBatchSize = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CleanupBatchSize");
	}

	[Fact]
	public void ReturnFailure_WhenCleanupBatchSizeIsNegative()
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions { CleanupBatchSize = -500 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CleanupBatchSize");
	}

	#endregion
}
