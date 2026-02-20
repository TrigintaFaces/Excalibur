// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;
using Excalibur.Dispatch.Options.Validation;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ValidationOptionsShould
{
	// --- ContextValidationOptions ---

	[Fact]
	public void ContextValidationOptions_DefaultValues_AreCorrect()
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
		options.RequiredFields.ShouldNotBeNull();
		options.RequiredFields.Count.ShouldBe(2);
		options.RequiredFields.ShouldContain("MessageId");
		options.RequiredFields.ShouldContain("MessageType");
		options.FieldValidationRules.ShouldNotBeNull();
		options.FieldValidationRules.ShouldBeEmpty();
		options.EnableDetailedDiagnostics.ShouldBeTrue();
		options.MaxMessageAge.ShouldBe(TimeSpan.FromDays(1));
		options.ValidateCorrelationChain.ShouldBeTrue();
		options.CustomValidatorTypes.ShouldNotBeNull();
		options.CustomValidatorTypes.ShouldBeEmpty();
	}

	[Fact]
	public void ContextValidationOptions_AllProperties_AreSettable()
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
	public void ContextValidationOptions_CustomValidatorTypes_CanAddEntries()
	{
		// Arrange
		var options = new ContextValidationOptions();

		// Act
		options.CustomValidatorTypes.Add(typeof(string));

		// Assert
		options.CustomValidatorTypes.Count.ShouldBe(1);
	}

	// --- VersioningOptions ---

	[Fact]
	public void VersioningOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new VersioningOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireContractVersion.ShouldBeTrue();
	}

	[Fact]
	public void VersioningOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new VersioningOptions
		{
			Enabled = false,
			RequireContractVersion = false,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.RequireContractVersion.ShouldBeFalse();
	}
}
