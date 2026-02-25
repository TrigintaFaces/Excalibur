// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;

namespace Excalibur.Dispatch.Tests.Messaging.Validation.Context;

/// <summary>
/// Unit tests for <see cref="ValidationSeverity"/> enum in Context namespace.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class ContextValidationSeverityShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<ValidationSeverity>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(ValidationSeverity.Info);
		values.ShouldContain(ValidationSeverity.Warning);
		values.ShouldContain(ValidationSeverity.Error);
		values.ShouldContain(ValidationSeverity.Critical);
	}

	[Fact]
	public void Info_HasExpectedValue()
	{
		// Assert
		((int)ValidationSeverity.Info).ShouldBe(0);
	}

	[Fact]
	public void Warning_HasExpectedValue()
	{
		// Assert
		((int)ValidationSeverity.Warning).ShouldBe(1);
	}

	[Fact]
	public void Error_HasExpectedValue()
	{
		// Assert
		((int)ValidationSeverity.Error).ShouldBe(2);
	}

	[Fact]
	public void Critical_HasExpectedValue()
	{
		// Assert
		((int)ValidationSeverity.Critical).ShouldBe(3);
	}

	[Fact]
	public void Info_IsDefaultValue()
	{
		// Arrange
		ValidationSeverity defaultSeverity = default;

		// Assert
		defaultSeverity.ShouldBe(ValidationSeverity.Info);
	}

	[Theory]
	[InlineData(ValidationSeverity.Info)]
	[InlineData(ValidationSeverity.Warning)]
	[InlineData(ValidationSeverity.Error)]
	[InlineData(ValidationSeverity.Critical)]
	public void BeDefinedForAllValues(ValidationSeverity severity)
	{
		// Assert
		Enum.IsDefined(severity).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, ValidationSeverity.Info)]
	[InlineData(1, ValidationSeverity.Warning)]
	[InlineData(2, ValidationSeverity.Error)]
	[InlineData(3, ValidationSeverity.Critical)]
	public void CastFromInt_ReturnsCorrectValue(int value, ValidationSeverity expected)
	{
		// Act
		var severity = (ValidationSeverity)value;

		// Assert
		severity.ShouldBe(expected);
	}

	[Fact]
	public void HaveSeveritiesOrderedByImpact()
	{
		// Assert - Values should be ordered from least to most severe
		(ValidationSeverity.Info < ValidationSeverity.Warning).ShouldBeTrue();
		(ValidationSeverity.Warning < ValidationSeverity.Error).ShouldBeTrue();
		(ValidationSeverity.Error < ValidationSeverity.Critical).ShouldBeTrue();
	}
}
