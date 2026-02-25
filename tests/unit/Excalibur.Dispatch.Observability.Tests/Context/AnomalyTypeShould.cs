// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="AnomalyType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class AnomalyTypeShould
{
	[Theory]
	[InlineData(AnomalyType.MissingCorrelation, 0)]
	[InlineData(AnomalyType.InsufficientContext, 1)]
	[InlineData(AnomalyType.ExcessiveContext, 2)]
	[InlineData(AnomalyType.CircularCausation, 3)]
	[InlineData(AnomalyType.PotentialPII, 4)]
	[InlineData(AnomalyType.OversizedItem, 5)]
	public void HaveCorrectIntegerValue(AnomalyType type, int expectedValue)
	{
		// Assert
		((int)type).ShouldBe(expectedValue);
	}

	[Fact]
	public void HaveSixValues()
	{
		// Assert
		Enum.GetValues<AnomalyType>().Length.ShouldBe(6);
	}

	[Theory]
	[InlineData("MissingCorrelation", AnomalyType.MissingCorrelation)]
	[InlineData("InsufficientContext", AnomalyType.InsufficientContext)]
	[InlineData("ExcessiveContext", AnomalyType.ExcessiveContext)]
	[InlineData("CircularCausation", AnomalyType.CircularCausation)]
	[InlineData("PotentialPII", AnomalyType.PotentialPII)]
	[InlineData("OversizedItem", AnomalyType.OversizedItem)]
	public void ParseFromString(string value, AnomalyType expected)
	{
		// Act & Assert
		Enum.Parse<AnomalyType>(value).ShouldBe(expected);
	}

	[Theory]
	[InlineData(AnomalyType.MissingCorrelation, "MissingCorrelation")]
	[InlineData(AnomalyType.InsufficientContext, "InsufficientContext")]
	[InlineData(AnomalyType.ExcessiveContext, "ExcessiveContext")]
	[InlineData(AnomalyType.CircularCausation, "CircularCausation")]
	[InlineData(AnomalyType.PotentialPII, "PotentialPII")]
	[InlineData(AnomalyType.OversizedItem, "OversizedItem")]
	public void ConvertToString(AnomalyType type, string expected)
	{
		// Act & Assert
		type.ToString().ShouldBe(expected);
	}

	[Fact]
	public void DefaultToMissingCorrelation()
	{
		// Arrange
		AnomalyType defaultValue = default;

		// Assert
		defaultValue.ShouldBe(AnomalyType.MissingCorrelation);
	}
}
