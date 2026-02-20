// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions.Persistence;

/// <summary>
/// Unit tests for <see cref="ValidationSeverity"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "Abstractions")]
public sealed class ValidationSeverityShould : UnitTestBase
{
	[Fact]
	public void HaveFourSeverityLevels()
	{
		// Act
		var values = Enum.GetValues<ValidationSeverity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void HaveInfoAsDefault()
	{
		// Assert
		ValidationSeverity defaultValue = default;
		defaultValue.ShouldBe(ValidationSeverity.Info);
	}

	[Theory]
	[InlineData(ValidationSeverity.Info, 0)]
	[InlineData(ValidationSeverity.Warning, 1)]
	[InlineData(ValidationSeverity.Error, 2)]
	[InlineData(ValidationSeverity.Critical, 3)]
	public void HaveCorrectUnderlyingValues(ValidationSeverity severity, int expectedValue)
	{
		// Assert
		((int)severity).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("Info", ValidationSeverity.Info)]
	[InlineData("Warning", ValidationSeverity.Warning)]
	[InlineData("Error", ValidationSeverity.Error)]
	[InlineData("Critical", ValidationSeverity.Critical)]
	public void ParseFromString(string input, ValidationSeverity expected)
	{
		// Act
		var result = Enum.Parse<ValidationSeverity>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(ValidationSeverity.Info, ValidationSeverity.Warning)]
	[InlineData(ValidationSeverity.Warning, ValidationSeverity.Error)]
	[InlineData(ValidationSeverity.Error, ValidationSeverity.Critical)]
	public void SupportComparisonForSeverityOrdering(ValidationSeverity lower, ValidationSeverity higher)
	{
		// Assert - more severe levels have higher ordinal values
		((int)lower).ShouldBeLessThan((int)higher);
	}
}
