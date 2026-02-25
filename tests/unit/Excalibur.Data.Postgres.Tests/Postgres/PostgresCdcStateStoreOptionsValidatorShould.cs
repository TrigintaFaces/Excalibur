// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Data.Postgres.Cdc;

using Microsoft.Extensions.Options;

using Excalibur.Data.Postgres;

namespace Excalibur.Data.Tests.Postgres.Cdc;

/// <summary>
/// Unit tests for <see cref="PostgresCdcStateStoreOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "PostgresCdcStateStoreOptionsValidator")]
public sealed class PostgresCdcStateStoreOptionsValidatorShould : UnitTestBase
{
	private readonly PostgresCdcStateStoreOptionsValidator _validator = new();

	[Fact]
	public void ReturnSuccessForValidOptions()
	{
		// Arrange
		var options = new PostgresCdcStateStoreOptions
		{
			SchemaName = "excalibur",
			TableName = "cdc_state",
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void FailForNullOptions()
	{
		// Act
		var result = _validator.Validate(null, null!);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("cannot be null");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FailForMissingSchemaName(string? schemaName)
	{
		// Arrange
		var options = new PostgresCdcStateStoreOptions
		{
			SchemaName = schemaName!,
			TableName = "cdc_state",
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FailForMissingTableName(string? tableName)
	{
		// Arrange
		var options = new PostgresCdcStateStoreOptions
		{
			SchemaName = "excalibur",
			TableName = tableName!,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}
}
