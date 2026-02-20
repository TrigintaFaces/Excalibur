// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;

namespace Excalibur.Dispatch.Tests.Messaging.Validation.Context;

/// <summary>
///     Tests for the <see cref="FieldValidationRule" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FieldValidationRuleShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		var sut = new FieldValidationRule();
		sut.Required.ShouldBeFalse();
		sut.ExpectedType.ShouldBeNull();
		sut.Pattern.ShouldBeNull();
		sut.MinLength.ShouldBeNull();
		sut.MaxLength.ShouldBeNull();
		sut.CustomValidator.ShouldBeNull();
		sut.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void SetRequired()
	{
		var sut = new FieldValidationRule { Required = true };
		sut.Required.ShouldBeTrue();
	}

	[Fact]
	public void SetExpectedType()
	{
		var sut = new FieldValidationRule { ExpectedType = typeof(string) };
		sut.ExpectedType.ShouldBe(typeof(string));
	}

	[Fact]
	public void SetPattern()
	{
		var sut = new FieldValidationRule { Pattern = @"^\d{4}$" };
		sut.Pattern.ShouldBe(@"^\d{4}$");
	}

	[Fact]
	public void SetMinLength()
	{
		var sut = new FieldValidationRule { MinLength = 5 };
		sut.MinLength.ShouldBe(5);
	}

	[Fact]
	public void SetMaxLength()
	{
		var sut = new FieldValidationRule { MaxLength = 100 };
		sut.MaxLength.ShouldBe(100);
	}

	[Fact]
	public void SetCustomValidator()
	{
		Func<object?, bool> validator = obj => obj is not null;
		var sut = new FieldValidationRule { CustomValidator = validator };
		sut.CustomValidator.ShouldBe(validator);
		sut.CustomValidator!("test").ShouldBeTrue();
		sut.CustomValidator(null).ShouldBeFalse();
	}

	[Fact]
	public void SetErrorMessage()
	{
		var sut = new FieldValidationRule { ErrorMessage = "Field is invalid" };
		sut.ErrorMessage.ShouldBe("Field is invalid");
	}

	[Fact]
	public void SupportObjectInitializerSyntax()
	{
		var sut = new FieldValidationRule
		{
			Required = true,
			ExpectedType = typeof(int),
			MinLength = 1,
			MaxLength = 10,
			Pattern = @"^\d+$",
			ErrorMessage = "Must be numeric",
		};

		sut.Required.ShouldBeTrue();
		sut.ExpectedType.ShouldBe(typeof(int));
		sut.MinLength.ShouldBe(1);
		sut.MaxLength.ShouldBe(10);
		sut.Pattern.ShouldBe(@"^\d+$");
		sut.ErrorMessage.ShouldBe("Must be numeric");
	}
}
