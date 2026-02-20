// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;
using Excalibur.Dispatch.Options.Validation;

namespace Excalibur.Dispatch.Tests.Options.Validation;

/// <summary>
/// Unit tests for <see cref="ContextValidationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
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
		options.ValidateRequiredFields.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateMultiTenancy_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.ValidateMultiTenancy.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateAuthentication_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.ValidateAuthentication.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateTracing_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.ValidateTracing.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateVersioning_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.ValidateVersioning.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateCollections_IsTrue()
	{
		// Arrange & Act
		var options = new ContextValidationOptions();

		// Assert
		options.ValidateCollections.ShouldBeTrue();
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
		options.ValidateCorrelationChain.ShouldBeTrue();
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
		options.ValidateRequiredFields = false;

		// Assert
		options.ValidateRequiredFields.ShouldBeFalse();
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
			ValidateRequiredFields = false,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
			ValidateCollections = false,
			EnableDetailedDiagnostics = false,
			MaxMessageAge = TimeSpan.FromHours(6),
			ValidateCorrelationChain = false,
		};

		// Assert
		options.Mode.ShouldBe(ValidationMode.Strict);
		options.ValidateRequiredFields.ShouldBeFalse();
		options.ValidateMultiTenancy.ShouldBeFalse();
		options.ValidateAuthentication.ShouldBeFalse();
		options.ValidateTracing.ShouldBeFalse();
		options.ValidateVersioning.ShouldBeFalse();
		options.ValidateCollections.ShouldBeFalse();
		options.EnableDetailedDiagnostics.ShouldBeFalse();
		options.MaxMessageAge.ShouldBe(TimeSpan.FromHours(6));
		options.ValidateCorrelationChain.ShouldBeFalse();
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
			ValidateRequiredFields = true,
			ValidateMultiTenancy = true,
			ValidateAuthentication = true,
			ValidateTracing = true,
		};

		// Assert
		options.Mode.ShouldBe(ValidationMode.Strict);
		options.ValidateRequiredFields.ShouldBeTrue();
		options.ValidateMultiTenancy.ShouldBeTrue();
		options.ValidateAuthentication.ShouldBeTrue();
		options.ValidateTracing.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForMinimalValidation_DisablesMostChecks()
	{
		// Act
		var options = new ContextValidationOptions
		{
			Mode = ValidationMode.Lenient,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
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
}
