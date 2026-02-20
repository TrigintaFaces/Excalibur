// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;

namespace Excalibur.Dispatch.Tests.Validation;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FieldValidationRuleShould
{
	[Fact]
	public void DefaultConstructor_SetDefaultValues()
	{
		// Act
		var rule = new FieldValidationRule();

		// Assert
		rule.Required.ShouldBeFalse();
		rule.ExpectedType.ShouldBeNull();
		rule.Pattern.ShouldBeNull();
		rule.MinLength.ShouldBeNull();
		rule.MaxLength.ShouldBeNull();
		rule.CustomValidator.ShouldBeNull();
		rule.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void SetAllProperties()
	{
		// Act
		var rule = new FieldValidationRule
		{
			Required = true,
			ExpectedType = typeof(string),
			Pattern = @"^[a-z]+$",
			MinLength = 1,
			MaxLength = 100,
			CustomValidator = obj => obj != null,
			ErrorMessage = "Invalid field value",
		};

		// Assert
		rule.Required.ShouldBeTrue();
		rule.ExpectedType.ShouldBe(typeof(string));
		rule.Pattern.ShouldBe(@"^[a-z]+$");
		rule.MinLength.ShouldBe(1);
		rule.MaxLength.ShouldBe(100);
		rule.CustomValidator.ShouldNotBeNull();
		rule.ErrorMessage.ShouldBe("Invalid field value");
	}

	[Fact]
	public void CustomValidator_InvokeCorrectly()
	{
		// Arrange
		var rule = new FieldValidationRule
		{
			CustomValidator = obj => obj is string s && s.Length > 0,
		};

		// Act & Assert
		rule.CustomValidator!("hello").ShouldBeTrue();
		rule.CustomValidator!(string.Empty).ShouldBeFalse();
		rule.CustomValidator!(null).ShouldBeFalse();
	}

	[Fact]
	public void Required_DefaultFalse()
	{
		// Arrange
		var rule = new FieldValidationRule();

		// Act
		rule.Required = true;

		// Assert
		rule.Required.ShouldBeTrue();
	}

	[Fact]
	public void ExpectedType_AcceptAnyType()
	{
		// Arrange
		var rule = new FieldValidationRule();

		// Act
		rule.ExpectedType = typeof(int);

		// Assert
		rule.ExpectedType.ShouldBe(typeof(int));
	}
}
