// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Validation;

/// <summary>
/// Unit tests for <see cref="InputValidationOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class InputValidationOptionsShould
{
	[Fact]
	public void HaveTrueEnableValidation_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.EnableValidation.ShouldBeTrue();
	}

	[Fact]
	public void HaveFalseAllowNullProperties_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.AllowNullProperties.ShouldBeFalse();
	}

	[Fact]
	public void HaveFalseAllowEmptyStrings_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.AllowEmptyStrings.ShouldBeFalse();
	}

	[Fact]
	public void Have10000MaxStringLength_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.MaxStringLength.ShouldBe(10000);
	}

	[Fact]
	public void Have1MBMaxMessageSizeBytes_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.MaxMessageSizeBytes.ShouldBe(1048576);
	}

	[Fact]
	public void Have10MaxObjectDepth_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.MaxObjectDepth.ShouldBe(10);
	}

	[Fact]
	public void Have7MaxMessageAgeDays_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.MaxMessageAgeDays.ShouldBe(7);
	}

	[Fact]
	public void HaveTrueRequireCorrelationId_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.RequireCorrelationId.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueBlockControlCharacters_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.InjectionPrevention.BlockControlCharacters.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueBlockHtmlContent_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.InjectionPrevention.BlockHtmlContent.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueBlockSqlInjection_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.InjectionPrevention.BlockSqlInjection.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueBlockNoSqlInjection_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.InjectionPrevention.BlockNoSqlInjection.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueBlockCommandInjection_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.InjectionPrevention.BlockCommandInjection.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueBlockPathTraversal_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.InjectionPrevention.BlockPathTraversal.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueBlockLdapInjection_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.InjectionPrevention.BlockLdapInjection.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueFailOnValidatorException_ByDefault()
	{
		// Arrange & Act
		var options = new InputValidationOptions();

		// Assert
		options.FailOnValidatorException.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingEnableValidation()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.EnableValidation = false;

		// Assert
		options.EnableValidation.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllowNullProperties()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.AllowNullProperties = true;

		// Assert
		options.AllowNullProperties.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllowEmptyStrings()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.AllowEmptyStrings = true;

		// Assert
		options.AllowEmptyStrings.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingMaxStringLength()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.MaxStringLength = 5000;

		// Assert
		options.MaxStringLength.ShouldBe(5000);
	}

	[Fact]
	public void AllowSettingMaxMessageSizeBytes()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.MaxMessageSizeBytes = 2097152; // 2MB

		// Assert
		options.MaxMessageSizeBytes.ShouldBe(2097152);
	}

	[Fact]
	public void AllowSettingMaxObjectDepth()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.MaxObjectDepth = 5;

		// Assert
		options.MaxObjectDepth.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingMaxMessageAgeDays()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.MaxMessageAgeDays = 30;

		// Assert
		options.MaxMessageAgeDays.ShouldBe(30);
	}

	[Fact]
	public void AllowSettingRequireCorrelationId()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.RequireCorrelationId = false;

		// Assert
		options.RequireCorrelationId.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingBlockControlCharacters()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.InjectionPrevention.BlockControlCharacters = false;

		// Assert
		options.InjectionPrevention.BlockControlCharacters.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingBlockHtmlContent()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.InjectionPrevention.BlockHtmlContent = false;

		// Assert
		options.InjectionPrevention.BlockHtmlContent.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingBlockSqlInjection()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.InjectionPrevention.BlockSqlInjection = false;

		// Assert
		options.InjectionPrevention.BlockSqlInjection.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingBlockNoSqlInjection()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.InjectionPrevention.BlockNoSqlInjection = false;

		// Assert
		options.InjectionPrevention.BlockNoSqlInjection.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingBlockCommandInjection()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.InjectionPrevention.BlockCommandInjection = false;

		// Assert
		options.InjectionPrevention.BlockCommandInjection.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingBlockPathTraversal()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.InjectionPrevention.BlockPathTraversal = false;

		// Assert
		options.InjectionPrevention.BlockPathTraversal.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingBlockLdapInjection()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.InjectionPrevention.BlockLdapInjection = false;

		// Assert
		options.InjectionPrevention.BlockLdapInjection.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingFailOnValidatorException()
	{
		// Arrange
		var options = new InputValidationOptions();

		// Act
		options.FailOnValidatorException = false;

		// Assert
		options.FailOnValidatorException.ShouldBeFalse();
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var options = new InputValidationOptions
		{
			EnableValidation = true,
			AllowNullProperties = true,
			AllowEmptyStrings = true,
			MaxStringLength = 50000,
			MaxMessageSizeBytes = 5242880, // 5MB
			MaxObjectDepth = 20,
			MaxMessageAgeDays = 14,
			RequireCorrelationId = false,
			InjectionPrevention = new InputInjectionPreventionOptions
			{
				BlockControlCharacters = false,
				BlockHtmlContent = false,
				BlockSqlInjection = true,
				BlockNoSqlInjection = true,
				BlockCommandInjection = true,
				BlockPathTraversal = true,
				BlockLdapInjection = true,
			},
			FailOnValidatorException = false,
		};

		// Assert
		options.EnableValidation.ShouldBeTrue();
		options.AllowNullProperties.ShouldBeTrue();
		options.AllowEmptyStrings.ShouldBeTrue();
		options.MaxStringLength.ShouldBe(50000);
		options.MaxMessageSizeBytes.ShouldBe(5242880);
		options.MaxObjectDepth.ShouldBe(20);
		options.MaxMessageAgeDays.ShouldBe(14);
		options.RequireCorrelationId.ShouldBeFalse();
		options.InjectionPrevention.BlockControlCharacters.ShouldBeFalse();
		options.InjectionPrevention.BlockHtmlContent.ShouldBeFalse();
		options.InjectionPrevention.BlockSqlInjection.ShouldBeTrue();
		options.InjectionPrevention.BlockNoSqlInjection.ShouldBeTrue();
		options.InjectionPrevention.BlockCommandInjection.ShouldBeTrue();
		options.InjectionPrevention.BlockPathTraversal.ShouldBeTrue();
		options.InjectionPrevention.BlockLdapInjection.ShouldBeTrue();
		options.FailOnValidatorException.ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(InputValidationOptions).IsSealed.ShouldBeTrue();
	}
}
