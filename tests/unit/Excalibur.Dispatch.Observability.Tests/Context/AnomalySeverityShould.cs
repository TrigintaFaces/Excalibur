// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="AnomalySeverity"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class AnomalySeverityShould
{
	[Fact]
	public void HaveLowAsZero()
	{
		// Assert
		((int)AnomalySeverity.Low).ShouldBe(0);
	}

	[Fact]
	public void HaveMediumAsOne()
	{
		// Assert
		((int)AnomalySeverity.Medium).ShouldBe(1);
	}

	[Fact]
	public void HaveHighAsTwo()
	{
		// Assert
		((int)AnomalySeverity.High).ShouldBe(2);
	}

	[Fact]
	public void HaveThreeValues()
	{
		// Assert
		Enum.GetValues<AnomalySeverity>().ShouldBe([AnomalySeverity.Low, AnomalySeverity.Medium, AnomalySeverity.High]);
	}

	[Fact]
	public void ParseFromString()
	{
		// Act & Assert
		Enum.Parse<AnomalySeverity>("Low").ShouldBe(AnomalySeverity.Low);
		Enum.Parse<AnomalySeverity>("Medium").ShouldBe(AnomalySeverity.Medium);
		Enum.Parse<AnomalySeverity>("High").ShouldBe(AnomalySeverity.High);
	}

	[Theory]
	[InlineData(AnomalySeverity.Low, "Low")]
	[InlineData(AnomalySeverity.Medium, "Medium")]
	[InlineData(AnomalySeverity.High, "High")]
	public void ConvertToString(AnomalySeverity severity, string expected)
	{
		// Act & Assert
		severity.ToString().ShouldBe(expected);
	}

	[Fact]
	public void DefaultToLow()
	{
		// Arrange
		AnomalySeverity defaultValue = default;

		// Assert
		defaultValue.ShouldBe(AnomalySeverity.Low);
	}
}
