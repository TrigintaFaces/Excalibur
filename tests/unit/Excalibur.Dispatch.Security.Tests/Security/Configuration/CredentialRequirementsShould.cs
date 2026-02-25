// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Configuration;

/// <summary>
/// Unit tests for <see cref="CredentialRequirements"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class CredentialRequirementsShould
{
	[Fact]
	public void Have12MinimumLength_ByDefault()
	{
		// Arrange & Act
		var requirements = new CredentialRequirements();

		// Assert
		requirements.MinimumLength.ShouldBe(12);
	}

	[Fact]
	public void HaveTrueRequireUppercase_ByDefault()
	{
		// Arrange & Act
		var requirements = new CredentialRequirements();

		// Assert
		requirements.RequireUppercase.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueRequireLowercase_ByDefault()
	{
		// Arrange & Act
		var requirements = new CredentialRequirements();

		// Assert
		requirements.RequireLowercase.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueRequireDigit_ByDefault()
	{
		// Arrange & Act
		var requirements = new CredentialRequirements();

		// Assert
		requirements.RequireDigit.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueRequireSpecialCharacter_ByDefault()
	{
		// Arrange & Act
		var requirements = new CredentialRequirements();

		// Assert
		requirements.RequireSpecialCharacter.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullProhibitedValues_ByDefault()
	{
		// Arrange & Act
		var requirements = new CredentialRequirements();

		// Assert
		requirements.ProhibitedValues.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMinimumLength()
	{
		// Arrange
		var requirements = new CredentialRequirements();

		// Act
		requirements.MinimumLength = 16;

		// Assert
		requirements.MinimumLength.ShouldBe(16);
	}

	[Fact]
	public void AllowSettingRequireUppercase()
	{
		// Arrange
		var requirements = new CredentialRequirements();

		// Act
		requirements.RequireUppercase = false;

		// Assert
		requirements.RequireUppercase.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRequireLowercase()
	{
		// Arrange
		var requirements = new CredentialRequirements();

		// Act
		requirements.RequireLowercase = false;

		// Assert
		requirements.RequireLowercase.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRequireDigit()
	{
		// Arrange
		var requirements = new CredentialRequirements();

		// Act
		requirements.RequireDigit = false;

		// Assert
		requirements.RequireDigit.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRequireSpecialCharacter()
	{
		// Arrange
		var requirements = new CredentialRequirements();

		// Act
		requirements.RequireSpecialCharacter = false;

		// Assert
		requirements.RequireSpecialCharacter.ShouldBeFalse();
	}

	[Fact]
	public void AllowInitializingProhibitedValues()
	{
		// Arrange & Act
		var requirements = new CredentialRequirements
		{
			ProhibitedValues = new HashSet<string> { "password", "123456", "admin" },
		};

		// Assert
		requirements.ProhibitedValues.ShouldNotBeNull();
		requirements.ProhibitedValues.Count.ShouldBe(3);
		requirements.ProhibitedValues.ShouldContain("password");
		requirements.ProhibitedValues.ShouldContain("123456");
		requirements.ProhibitedValues.ShouldContain("admin");
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var requirements = new CredentialRequirements
		{
			MinimumLength = 20,
			RequireUppercase = true,
			RequireLowercase = true,
			RequireDigit = true,
			RequireSpecialCharacter = true,
			ProhibitedValues = new HashSet<string> { "qwerty", "letmein" },
		};

		// Assert
		requirements.MinimumLength.ShouldBe(20);
		requirements.RequireUppercase.ShouldBeTrue();
		requirements.RequireLowercase.ShouldBeTrue();
		requirements.RequireDigit.ShouldBeTrue();
		requirements.RequireSpecialCharacter.ShouldBeTrue();
		requirements.ProhibitedValues.ShouldNotBeNull();
		requirements.ProhibitedValues.Count.ShouldBe(2);
	}

	[Fact]
	public void AllowRelaxedRequirements()
	{
		// Arrange & Act
		var requirements = new CredentialRequirements
		{
			MinimumLength = 8,
			RequireUppercase = false,
			RequireLowercase = false,
			RequireDigit = false,
			RequireSpecialCharacter = false,
		};

		// Assert
		requirements.MinimumLength.ShouldBe(8);
		requirements.RequireUppercase.ShouldBeFalse();
		requirements.RequireLowercase.ShouldBeFalse();
		requirements.RequireDigit.ShouldBeFalse();
		requirements.RequireSpecialCharacter.ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(CredentialRequirements).IsSealed.ShouldBeTrue();
	}
}
