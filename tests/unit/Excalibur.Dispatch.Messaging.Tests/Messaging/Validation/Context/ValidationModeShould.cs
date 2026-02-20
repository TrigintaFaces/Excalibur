// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;

namespace Excalibur.Dispatch.Tests.Messaging.Validation.Context;

/// <summary>
/// Unit tests for <see cref="ValidationMode"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class ValidationModeShould
{
	[Fact]
	public void HaveTwoDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<ValidationMode>();

		// Assert
		values.Length.ShouldBe(2);
		values.ShouldContain(ValidationMode.Strict);
		values.ShouldContain(ValidationMode.Lenient);
	}

	[Fact]
	public void Strict_HasExpectedValue()
	{
		// Assert
		((int)ValidationMode.Strict).ShouldBe(0);
	}

	[Fact]
	public void Lenient_HasExpectedValue()
	{
		// Assert
		((int)ValidationMode.Lenient).ShouldBe(1);
	}

	[Fact]
	public void Strict_IsDefaultValue()
	{
		// Arrange
		ValidationMode defaultMode = default;

		// Assert
		defaultMode.ShouldBe(ValidationMode.Strict);
	}

	[Theory]
	[InlineData(ValidationMode.Strict)]
	[InlineData(ValidationMode.Lenient)]
	public void BeDefinedForAllValues(ValidationMode mode)
	{
		// Assert
		Enum.IsDefined(mode).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, ValidationMode.Strict)]
	[InlineData(1, ValidationMode.Lenient)]
	public void CastFromInt_ReturnsCorrectValue(int value, ValidationMode expected)
	{
		// Act
		var mode = (ValidationMode)value;

		// Assert
		mode.ShouldBe(expected);
	}
}
