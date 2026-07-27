// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;
using Excalibur.Dispatch.Options.Validation;

namespace Excalibur.Dispatch.Tests.Options.Validation;

/// <summary>
/// Unit tests for <see cref="ContextValidationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Options)]
[Trait("Priority", "0")]
public sealed class ContextValidationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Mode_IsLenient()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.Mode.ShouldBe(ValidationMode.Lenient);
	}

	[Fact]
	public void Default_ValidateRequiredFields_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.Checks.ValidateRequiredFields.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateMultiTenancy_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.Checks.ValidateMultiTenancy.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateAuthentication_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.Checks.ValidateAuthentication.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateTracing_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.Checks.ValidateTracing.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateVersioning_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.Checks.ValidateVersioning.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateCollections_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.Checks.ValidateCollections.ShouldBeTrue();
	}

	[Fact]
	public void Default_RequiredFields_ContainsMessageIdAndMessageType()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		_ = options.RequiredFields.ShouldNotBeNull();
		options.RequiredFields.ShouldContain("MessageId");
		options.RequiredFields.ShouldContain("MessageType");
	}

	[Fact]
	public void Default_FieldValidationRules_IsEmpty()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		_ = options.FieldValidationRules.ShouldNotBeNull();
		options.FieldValidationRules.ShouldBeEmpty();
	}

	[Fact]
	public void Default_EnableDetailedDiagnostics_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.EnableDetailedDiagnostics.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxMessageAge_Is1Day()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.MaxMessageAge.ShouldBe(TimeSpan.FromDays(1));
	}

	[Fact]
	public void Default_ValidateCorrelationChain_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.Checks.ValidateCorrelationChain.ShouldBeTrue();
	}

	[Fact]
	public void Default_CustomValidatorTypes_IsEmpty()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		_ = options.CustomValidatorTypes.ShouldNotBeNull();
		options.CustomValidatorTypes.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Mode_CanBeSet()
	{
		// Arrange
		var options = new ContextValidationOptions();

		// Act
		options.Mode = ValidationMode.Strict;

		// Assert
		options.Mode.ShouldBe(ValidationMode.Strict);
	}

	[Fact]
	public void ValidateRequiredFields_CanBeSet()
	{
		// Arrange
		var options = new ContextValidationOptions();

		// Act
		options.Checks.ValidateRequiredFields = false;

		// Assert
		options.Checks.ValidateRequiredFields.ShouldBeFalse();
	}

	[Fact]
	public void MaxMessageAge_CanBeSetToNull()
	{
		// Arrange
		var options = new ContextValidationOptions();

		// Act
		options.MaxMessageAge = null;

		// Assert
		options.MaxMessageAge.ShouldBeNull();
	}

	[Fact]
	public void MaxMessageAge_CanBeSetToCustomValue()
	{
		// Arrange
		var options = new ContextValidationOptions();

		// Act
		options.MaxMessageAge = TimeSpan.FromHours(12);

		// Assert
		options.MaxMessageAge.ShouldBe(TimeSpan.FromHours(12));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsScalarProperties()
	{
		// Act
		var options = new ContextValidationOptions
		{
			Mode = ValidationMode.Strict,
			Checks =
			{
				ValidateRequiredFields = false,
				ValidateMultiTenancy = false,
				ValidateAuthentication = false,
				ValidateTracing = false,
				ValidateVersioning = false,
				ValidateCollections = false,
				ValidateCorrelationChain = false,
			},
			EnableDetailedDiagnostics = false,
			MaxMessageAge = TimeSpan.FromHours(6),
		};

		// Assert
		options.Mode.ShouldBe(ValidationMode.Strict);
		options.Checks.ValidateRequiredFields.ShouldBeFalse();
		options.Checks.ValidateMultiTenancy.ShouldBeFalse();
		options.Checks.ValidateAuthentication.ShouldBeFalse();
		options.Checks.ValidateTracing.ShouldBeFalse();
		options.Checks.ValidateVersioning.ShouldBeFalse();
		options.Checks.ValidateCollections.ShouldBeFalse();
		options.EnableDetailedDiagnostics.ShouldBeFalse();
		options.MaxMessageAge.ShouldBe(TimeSpan.FromHours(6));
		options.Checks.ValidateCorrelationChain.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForStrictValidation_EnablesAllChecks()
	{
		// Act
		var options = new ContextValidationOptions
		{
			Mode = ValidationMode.Strict,
			Checks =
			{
				ValidateRequiredFields = true,
				ValidateMultiTenancy = true,
				ValidateAuthentication = true,
				ValidateTracing = true,
			},
		};

		// Assert
		options.Mode.ShouldBe(ValidationMode.Strict);
		options.Checks.ValidateRequiredFields.ShouldBeTrue();
		options.Checks.ValidateMultiTenancy.ShouldBeTrue();
		options.Checks.ValidateAuthentication.ShouldBeTrue();
		options.Checks.ValidateTracing.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForMinimalValidation_DisablesMostChecks()
	{
		// Act
		var options = new ContextValidationOptions
		{
			Mode = ValidationMode.Lenient,
			Checks =
			{
				ValidateMultiTenancy = false,
				ValidateAuthentication = false,
				ValidateTracing = false,
				ValidateVersioning = false,
			},
		};

		// Assert
		options.Mode.ShouldBe(ValidationMode.Lenient);
	}

	[Fact]
	public void Options_WithCustomValidators_AddsValidatorTypes()
	{
		// Arrange
		var options = new ContextValidationOptions();

		// Act
		options.CustomValidatorTypes.Add(typeof(string));

		// Assert
		options.CustomValidatorTypes.Count.ShouldBe(1);
		options.CustomValidatorTypes.ShouldContain(typeof(string));
	}

	#endregion

	#region Validator Tests

	[Fact]
	public void Validator_Fails_WhenMaxMessageAgeIsNotPositive()
	{
		// Arrange
		var validator = new ContextValidationOptionsValidator();
		var options = new ContextValidationOptions { MaxMessageAge = TimeSpan.Zero };

		// Act
		var result = validator.Validate(name: null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validator_Fails_WhenRequiredFieldsEmptyAndValidationEnabled()
	{
		// Arrange
		var validator = new ContextValidationOptionsValidator();
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			Checks = { ValidateRequiredFields = true },
		};

		// Act
		var result = validator.Validate(name: null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validator_Succeeds_ForDefaultOptions()
	{
		// Arrange
		var validator = new ContextValidationOptionsValidator();
		var options = new ContextValidationOptions();

		// Act
		var result = validator.Validate(name: null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	#endregion
}
