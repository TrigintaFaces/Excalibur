// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="AdaptationImpact"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class AdaptationImpactShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<AdaptationImpact>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(AdaptationImpact.Minor);
		values.ShouldContain(AdaptationImpact.Moderate);
		values.ShouldContain(AdaptationImpact.Major);
	}

	[Fact]
	public void Minor_HasExpectedValue()
	{
		// Assert
		((int)AdaptationImpact.Minor).ShouldBe(0);
	}

	[Fact]
	public void Moderate_HasExpectedValue()
	{
		// Assert
		((int)AdaptationImpact.Moderate).ShouldBe(1);
	}

	[Fact]
	public void Major_HasExpectedValue()
	{
		// Assert
		((int)AdaptationImpact.Major).ShouldBe(2);
	}

	[Fact]
	public void Minor_IsDefaultValue()
	{
		// Arrange
		AdaptationImpact defaultImpact = default;

		// Assert
		defaultImpact.ShouldBe(AdaptationImpact.Minor);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public void BeDefinedForAllValues(int impactValue)
	{
		// Assert
		var impact = (AdaptationImpact)impactValue;
		Enum.IsDefined(impact).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 1)]
	[InlineData(2, 2)]
	public void CastFromInt_ReturnsCorrectValue(int value, int expectedValue)
	{
		// Act
		var impact = (AdaptationImpact)value;
		var expected = (AdaptationImpact)expectedValue;

		// Assert
		impact.ShouldBe(expected);
	}

	[Fact]
	public void HaveImpactsOrderedBySeverity()
	{
		// Assert - Values should be ordered from least to most severe impact
		(AdaptationImpact.Minor < AdaptationImpact.Moderate).ShouldBeTrue();
		(AdaptationImpact.Moderate < AdaptationImpact.Major).ShouldBeTrue();
	}
}
