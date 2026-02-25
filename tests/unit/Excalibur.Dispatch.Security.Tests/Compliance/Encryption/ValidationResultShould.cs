// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ValidationResult"/> and related types.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ValidationResultShould
{
	#region ValidationResult Factory Methods

	[Fact]
	public void Success_CreateValidResult()
	{
		// Act
		var result = ValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeNull();
		result.Warnings.ShouldBeNull();
		result.Code.ShouldBeNull();
		result.Details.ShouldBeNull();
	}

	[Fact]
	public void SuccessWithWarnings_CreateValidResultWithWarnings()
	{
		// Arrange
		var warnings = new[] { "Warning 1", "Warning 2" };

		// Act
		var result = ValidationResult.SuccessWithWarnings(warnings);

		// Assert
		result.IsValid.ShouldBeTrue();
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.Count.ShouldBe(2);
		result.Warnings[0].ShouldBe("Warning 1");
		result.Warnings[1].ShouldBe("Warning 2");
		result.Errors.ShouldBeNull();
	}

	[Fact]
	public void Failure_CreateInvalidResultWithErrors()
	{
		// Arrange
		var errors = new[] { "Error 1", "Error 2" };

		// Act
		var result = ValidationResult.Failure(errors);

		// Assert
		result.IsValid.ShouldBeFalse();
		_ = result.Errors.ShouldNotBeNull();
		result.Errors.Count.ShouldBe(2);
		result.Errors[0].ShouldBe("Error 1");
		result.Errors[1].ShouldBe("Error 2");
		result.Code.ShouldBeNull();
	}

	[Fact]
	public void FailureWithCode_CreateInvalidResultWithCodeAndError()
	{
		// Arrange
		var code = "ERR001";
		var error = "Validation failed";

		// Act
		var result = ValidationResult.Failure(code, error);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Code.ShouldBe(code);
		_ = result.Errors.ShouldNotBeNull();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldBe(error);
	}

	[Fact]
	public void ValidationResult_SupportDetails()
	{
		// Arrange
		var details = new Dictionary<string, object>
		{
			["field"] = "KeyId",
			["value"] = "test-key"
		};

		// Act
		var result = new ValidationResult
		{
			IsValid = false,
			Errors = ["Field validation failed"],
			Details = details
		};

		// Assert
		_ = result.Details.ShouldNotBeNull();
		result.Details["field"].ShouldBe("KeyId");
		result.Details["value"].ShouldBe("test-key");
	}

	#endregion ValidationResult Factory Methods

	#region ComprehensiveValidationResult Tests

	[Fact]
	public void ComprehensiveValidationResult_CombineValidationResults()
	{
		// Arrange
		var structureValidation = ValidationResult.Success();
		var keyValidation = ValidationResult.Success();
		var decryptabilityValidation = ValidationResult.Success();

		// Act
		var result = new ComprehensiveValidationResult
		{
			IsValid = true,
			StructureValidation = structureValidation,
			KeyValidation = keyValidation,
			DecryptabilityValidation = decryptabilityValidation,
			Duration = TimeSpan.FromMilliseconds(150)
		};

		// Assert
		result.IsValid.ShouldBeTrue();
		result.StructureValidation.IsValid.ShouldBeTrue();
		result.KeyValidation.IsValid.ShouldBeTrue();
		result.DecryptabilityValidation.IsValid.ShouldBeTrue();
		result.Duration.TotalMilliseconds.ShouldBe(150);
	}

	[Fact]
	public void ComprehensiveValidationResult_IncludeComplianceValidation()
	{
		// Arrange
		var complianceValidation = ValidationResult.Failure("COMP001", "Algorithm not FIPS compliant");

		// Act
		var result = new ComprehensiveValidationResult
		{
			IsValid = false,
			StructureValidation = ValidationResult.Success(),
			KeyValidation = ValidationResult.Success(),
			ComplianceValidation = complianceValidation
		};

		// Assert
		result.IsValid.ShouldBeFalse();
		_ = result.ComplianceValidation.ShouldNotBeNull();
		result.ComplianceValidation.IsValid.ShouldBeFalse();
		result.ComplianceValidation.Code.ShouldBe("COMP001");
	}

	[Fact]
	public void ComprehensiveValidationResult_AllowNullOptionalValidations()
	{
		// Act
		var result = new ComprehensiveValidationResult
		{
			IsValid = true,
			StructureValidation = ValidationResult.Success(),
			KeyValidation = ValidationResult.Success(),
			DecryptabilityValidation = null,
			ComplianceValidation = null
		};

		// Assert
		result.DecryptabilityValidation.ShouldBeNull();
		result.ComplianceValidation.ShouldBeNull();
	}

	#endregion ComprehensiveValidationResult Tests

	#region ComplianceRequirements Tests

	[Fact]
	public void ComplianceRequirements_HaveCorrectDefaults()
	{
		// Act
		var requirements = new ComplianceRequirements();

		// Assert
		requirements.RequireFips.ShouldBeFalse();
		requirements.MinKeySize.ShouldBeNull();
		requirements.AllowedAlgorithms.ShouldBeNull();
		requirements.MaxDataAge.ShouldBeNull();
		requirements.RequireAuthTag.ShouldBeTrue();
	}

	[Fact]
	public void ComplianceRequirements_FipsPreset_HaveStrictSettings()
	{
		// Act
		var fipsRequirements = ComplianceRequirements.Fips;

		// Assert
		fipsRequirements.RequireFips.ShouldBeTrue();
		fipsRequirements.MinKeySize.ShouldBe(256);
		fipsRequirements.RequireAuthTag.ShouldBeTrue();
		_ = fipsRequirements.AllowedAlgorithms.ShouldNotBeNull();
		fipsRequirements.AllowedAlgorithms.Count.ShouldBe(2);
		fipsRequirements.AllowedAlgorithms.ShouldContain(EncryptionAlgorithm.Aes256Gcm);
		fipsRequirements.AllowedAlgorithms.ShouldContain(EncryptionAlgorithm.Aes256CbcHmac);
	}

	[Fact]
	public void ComplianceRequirements_SupportCustomConfiguration()
	{
		// Arrange
		var customAlgorithms = new HashSet<EncryptionAlgorithm>
		{
			EncryptionAlgorithm.Aes256Gcm
		};

		// Act
		var requirements = new ComplianceRequirements
		{
			RequireFips = true,
			MinKeySize = 256,
			AllowedAlgorithms = customAlgorithms,
			MaxDataAge = TimeSpan.FromDays(365),
			RequireAuthTag = true
		};

		// Assert
		requirements.RequireFips.ShouldBeTrue();
		requirements.MinKeySize.ShouldBe(256);
		requirements.AllowedAlgorithms.Count.ShouldBe(1);
		requirements.MaxDataAge.Value.TotalDays.ShouldBe(365);
	}

	#endregion ComplianceRequirements Tests

	#region ValidationOptions Tests

	[Fact]
	public void ValidationOptions_HaveCorrectDefaults()
	{
		// Act
		var options = ValidationOptions.Default;

		// Assert
		options.ValidateDecryptability.ShouldBeTrue();
		options.ValidateCompliance.ShouldBeTrue();
		options.ComplianceRequirements.ShouldBeNull();
		options.FailFast.ShouldBeFalse();
	}

	[Fact]
	public void ValidationOptions_SupportCustomConfiguration()
	{
		// Arrange
		var complianceRequirements = ComplianceRequirements.Fips;

		// Act
		var options = new ValidationOptions
		{
			ValidateDecryptability = false,
			ValidateCompliance = true,
			ComplianceRequirements = complianceRequirements,
			FailFast = true
		};

		// Assert
		options.ValidateDecryptability.ShouldBeFalse();
		options.ValidateCompliance.ShouldBeTrue();
		options.ComplianceRequirements.ShouldBe(complianceRequirements);
		options.FailFast.ShouldBeTrue();
	}

	#endregion ValidationOptions Tests
}
