// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Cdc;

using Microsoft.Extensions.Options;

using Excalibur.Data.Firestore;

namespace Excalibur.Data.Tests.Firestore.Cdc;

/// <summary>
/// Unit tests for <see cref="FirestoreCdcStateStoreOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "FirestoreCdcStateStoreOptionsValidator")]
public sealed class FirestoreCdcStateStoreOptionsValidatorShould : UnitTestBase
{
	private readonly FirestoreCdcStateStoreOptionsValidator _validator = new();

	[Fact]
	public void ReturnSuccessForValidOptions()
	{
		// Arrange
		var options = new FirestoreCdcStateStoreOptions
		{
			CollectionName = "cdc_positions",
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
	public void FailForMissingCollectionName(string? collectionName)
	{
		// Arrange
		var options = new FirestoreCdcStateStoreOptions
		{
			CollectionName = collectionName!,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CollectionName");
	}
}
