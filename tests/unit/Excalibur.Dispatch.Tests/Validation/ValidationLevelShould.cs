// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation;

namespace Excalibur.Dispatch.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="ValidationLevel"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class ValidationLevelShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<ValidationLevel>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(ValidationLevel.None);
		values.ShouldContain(ValidationLevel.Basic);
		values.ShouldContain(ValidationLevel.Standard);
		values.ShouldContain(ValidationLevel.Strict);
	}

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)ValidationLevel.None).ShouldBe(0);
	}

	[Fact]
	public void Basic_HasExpectedValue()
	{
		// Assert
		((int)ValidationLevel.Basic).ShouldBe(1);
	}

	[Fact]
	public void Standard_HasExpectedValue()
	{
		// Assert
		((int)ValidationLevel.Standard).ShouldBe(2);
	}

	[Fact]
	public void Strict_HasExpectedValue()
	{
		// Assert
		((int)ValidationLevel.Strict).ShouldBe(3);
	}

	[Fact]
	public void None_IsDefaultValue()
	{
		// Arrange
		ValidationLevel defaultLevel = default;

		// Assert
		defaultLevel.ShouldBe(ValidationLevel.None);
	}

	[Theory]
	[InlineData(ValidationLevel.None)]
	[InlineData(ValidationLevel.Basic)]
	[InlineData(ValidationLevel.Standard)]
	[InlineData(ValidationLevel.Strict)]
	public void BeDefinedForAllValues(ValidationLevel level)
	{
		// Assert
		Enum.IsDefined(level).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, ValidationLevel.None)]
	[InlineData(1, ValidationLevel.Basic)]
	[InlineData(2, ValidationLevel.Standard)]
	[InlineData(3, ValidationLevel.Strict)]
	public void CastFromInt_ReturnsCorrectValue(int value, ValidationLevel expected)
	{
		// Act
		var level = (ValidationLevel)value;

		// Assert
		level.ShouldBe(expected);
	}

	[Fact]
	public void HaveLevelsOrderedByStrictness()
	{
		// Assert - Values should be ordered from least to most strict
		(ValidationLevel.None < ValidationLevel.Basic).ShouldBeTrue();
		(ValidationLevel.Basic < ValidationLevel.Standard).ShouldBeTrue();
		(ValidationLevel.Standard < ValidationLevel.Strict).ShouldBeTrue();
	}
}
