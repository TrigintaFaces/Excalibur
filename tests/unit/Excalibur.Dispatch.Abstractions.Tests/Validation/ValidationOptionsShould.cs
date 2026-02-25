// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="ValidationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Validation")]
[Trait("Priority", "0")]
public sealed class ValidationOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_EnabledIsTrue()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_FailFastIsTrue()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.FailFast.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxErrorsIs10()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.MaxErrors.ShouldBe(10);
	}

	[Fact]
	public void Default_IncludeDetailedErrorsIsTrue()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.IncludeDetailedErrors.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateContractsIsTrue()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.ValidateContracts.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateSchemasIsFalse()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.ValidateSchemas.ShouldBeFalse();
	}

	[Fact]
	public void Default_ValidationTimeoutIs5Seconds()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		options.ValidationTimeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void Default_CustomMetadataIsEmpty()
	{
		// Arrange & Act
		var options = new ValidationOptions();

		// Assert
		_ = options.CustomMetadata.ShouldNotBeNull();
		options.CustomMetadata.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSetToFalse()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void FailFast_CanBeSetToFalse()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.FailFast = false;

		// Assert
		options.FailFast.ShouldBeFalse();
	}

	[Fact]
	public void MaxErrors_CanBeSet()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.MaxErrors = 50;

		// Assert
		options.MaxErrors.ShouldBe(50);
	}

	[Fact]
	public void IncludeDetailedErrors_CanBeSetToFalse()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.IncludeDetailedErrors = false;

		// Assert
		options.IncludeDetailedErrors.ShouldBeFalse();
	}

	[Fact]
	public void ValidateContracts_CanBeSetToFalse()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.ValidateContracts = false;

		// Assert
		options.ValidateContracts.ShouldBeFalse();
	}

	[Fact]
	public void ValidateSchemas_CanBeSetToTrue()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.ValidateSchemas = true;

		// Assert
		options.ValidateSchemas.ShouldBeTrue();
	}

	[Fact]
	public void ValidationTimeout_CanBeSet()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.ValidationTimeout = TimeSpan.FromSeconds(30);

		// Assert
		options.ValidationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void CustomMetadata_CanAddEntry()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.CustomMetadata["key"] = "value";

		// Assert
		options.CustomMetadata.ShouldContainKeyAndValue("key", "value");
	}

	[Fact]
	public void CustomMetadata_CanAddMultipleEntries()
	{
		// Arrange
		var options = new ValidationOptions();

		// Act
		options.CustomMetadata["key1"] = "value1";
		options.CustomMetadata["key2"] = 42;
		options.CustomMetadata["key3"] = true;

		// Assert
		options.CustomMetadata.Count.ShouldBe(3);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new ValidationOptions
		{
			Enabled = false,
			FailFast = false,
			MaxErrors = 25,
			IncludeDetailedErrors = false,
			ValidateContracts = false,
			ValidateSchemas = true,
			ValidationTimeout = TimeSpan.FromSeconds(15),
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.FailFast.ShouldBeFalse();
		options.MaxErrors.ShouldBe(25);
		options.IncludeDetailedErrors.ShouldBeFalse();
		options.ValidateContracts.ShouldBeFalse();
		options.ValidateSchemas.ShouldBeTrue();
		options.ValidationTimeout.ShouldBe(TimeSpan.FromSeconds(15));
	}

	#endregion
}
