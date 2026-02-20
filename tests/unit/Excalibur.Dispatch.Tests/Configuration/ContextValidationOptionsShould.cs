// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;
using Excalibur.Dispatch.Options.Validation;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ContextValidationOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new ContextValidationOptions();

		// Assert
		options.Mode.ShouldBe(ValidationMode.Lenient);
		options.ValidateRequiredFields.ShouldBeTrue();
		options.ValidateMultiTenancy.ShouldBeTrue();
		options.ValidateAuthentication.ShouldBeTrue();
		options.ValidateTracing.ShouldBeTrue();
		options.ValidateVersioning.ShouldBeTrue();
		options.ValidateCollections.ShouldBeTrue();
		options.EnableDetailedDiagnostics.ShouldBeTrue();
		options.MaxMessageAge.ShouldBe(TimeSpan.FromDays(1));
		options.ValidateCorrelationChain.ShouldBeTrue();
		options.CustomValidatorTypes.ShouldNotBeNull();
		options.CustomValidatorTypes.ShouldBeEmpty();
	}

	[Fact]
	public void RequiredFields_DefaultToMessageIdAndMessageType()
	{
		// Act
		var options = new ContextValidationOptions();

		// Assert
		options.RequiredFields.Count.ShouldBe(2);
		options.RequiredFields.ShouldContain("MessageId");
		options.RequiredFields.ShouldContain("MessageType");
	}

	[Fact]
	public void FieldValidationRules_DefaultToEmpty()
	{
		// Act
		var options = new ContextValidationOptions();

		// Assert
		options.FieldValidationRules.ShouldNotBeNull();
		options.FieldValidationRules.ShouldBeEmpty();
	}

	[Fact]
	public void FieldValidationRules_CanAddRules()
	{
		// Arrange
		var options = new ContextValidationOptions();

		// Act
		options.FieldValidationRules["CorrelationId"] = new FieldValidationRule
		{
			Required = true,
			MinLength = 1,
			MaxLength = 128,
		};

		// Assert
		options.FieldValidationRules.Count.ShouldBe(1);
		options.FieldValidationRules["CorrelationId"].Required.ShouldBeTrue();
	}

	[Fact]
	public void CustomValidatorTypes_CanAddTypes()
	{
		// Arrange
		var options = new ContextValidationOptions();

		// Act
		options.CustomValidatorTypes.Add(typeof(TraceContextValidator));

		// Assert
		options.CustomValidatorTypes.Count.ShouldBe(1);
		options.CustomValidatorTypes.ShouldContain(typeof(TraceContextValidator));
	}

	[Fact]
	public void AllProperties_AreSettable()
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
			MaxMessageAge = TimeSpan.FromHours(12),
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
		options.MaxMessageAge.ShouldBe(TimeSpan.FromHours(12));
		options.ValidateCorrelationChain.ShouldBeFalse();
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
}
