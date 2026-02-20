// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Cdc;

using Microsoft.Extensions.Options;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

/// <summary>
/// Unit tests for <see cref="MongoDbCdcStateStoreOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "MongoDbCdcStateStoreOptionsValidator")]
public sealed class MongoDbCdcStateStoreOptionsValidatorShould : UnitTestBase
{
	private readonly MongoDbCdcStateStoreOptionsValidator _validator = new();

	[Fact]
	public void ReturnSuccessForValidOptions()
	{
		// Arrange
		var options = new MongoDbCdcStateStoreOptions
		{
			DatabaseName = "excalibur",
			CollectionName = "cdc_state",
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
	public void FailForMissingDatabaseName(string? databaseName)
	{
		// Arrange
		var options = new MongoDbCdcStateStoreOptions
		{
			DatabaseName = databaseName!,
			CollectionName = "cdc_state",
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("DatabaseName");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FailForMissingCollectionName(string? collectionName)
	{
		// Arrange
		var options = new MongoDbCdcStateStoreOptions
		{
			DatabaseName = "excalibur",
			CollectionName = collectionName!,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CollectionName");
	}
}
