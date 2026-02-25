// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Tests.Data.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbCdcStateStoreOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DynamoDbCdcStateStoreOptionsValidatorShould
{
	private readonly DynamoDbCdcStateStoreOptionsValidator _validator = new();

	[Fact]
	public void ReturnSuccessForValidOptions()
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
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
	public void FailForMissingTableName(string? tableName)
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = tableName!,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}
}
