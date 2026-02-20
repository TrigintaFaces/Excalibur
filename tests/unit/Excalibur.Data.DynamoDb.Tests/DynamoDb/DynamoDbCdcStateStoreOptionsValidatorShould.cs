// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Cdc;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbCdcStateStoreOptionsValidator"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify options validation logic.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class DynamoDbCdcStateStoreOptionsValidatorShould
{
	private readonly DynamoDbCdcStateStoreOptionsValidator _validator = new();

	#region Validation Tests

	[Fact]
	public void Validate_ReturnsSuccess_ForValidOptions()
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = "cdc-state"
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Validate_ReturnsFail_WhenOptionsIsNull()
	{
		// Act
		var result = _validator.Validate(null, null!);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("cannot be null");
	}

	[Fact]
	public void Validate_ReturnsFail_WhenTableNameIsNull()
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = null!
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Fact]
	public void Validate_ReturnsFail_WhenTableNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = string.Empty
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Fact]
	public void Validate_ReturnsFail_WhenTableNameIsWhitespace()
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = "   "
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Theory]
	[InlineData("test-name")]
	[InlineData("another-name")]
	[InlineData(null)]
	public void Validate_IgnoresName_Parameter(string? name)
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = "cdc-state"
		};

		// Act
		var result = _validator.Validate(name, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIValidateOptions()
	{
		// Assert
		typeof(IValidateOptions<DynamoDbCdcStateStoreOptions>)
			.IsAssignableFrom(typeof(DynamoDbCdcStateStoreOptionsValidator))
			.ShouldBeTrue();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(DynamoDbCdcStateStoreOptionsValidator).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbCdcStateStoreOptionsValidator).IsPublic.ShouldBeTrue();
	}

	#endregion
}
