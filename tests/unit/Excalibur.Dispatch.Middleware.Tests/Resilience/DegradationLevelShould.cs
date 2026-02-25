// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DegradationLevel"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class DegradationLevelShould
{
	[Fact]
	public void HaveCorrectValueForNormal()
	{
		// Assert
		((int)DegradationLevel.Normal).ShouldBe(0);
	}

	[Fact]
	public void HaveCorrectValueForMinor()
	{
		// Assert
		((int)DegradationLevel.Minor).ShouldBe(1);
	}

	[Fact]
	public void HaveCorrectValueForModerate()
	{
		// Assert
		((int)DegradationLevel.Moderate).ShouldBe(2);
	}

	[Fact]
	public void HaveCorrectValueForMajor()
	{
		// Assert
		((int)DegradationLevel.Major).ShouldBe(3);
	}

	[Fact]
	public void HaveCorrectValueForSevere()
	{
		// Assert
		((int)DegradationLevel.Severe).ShouldBe(4);
	}

	[Fact]
	public void HaveCorrectValueForEmergency()
	{
		// Assert
		((int)DegradationLevel.Emergency).ShouldBe(5);
	}

	[Fact]
	public void HaveExactlySixValues()
	{
		// Assert
		Enum.GetValues<DegradationLevel>().Length.ShouldBe(6);
	}

	[Fact]
	public void DefaultValueIsNormal()
	{
		// Arrange
		DegradationLevel defaultLevel = default;

		// Assert
		defaultLevel.ShouldBe(DegradationLevel.Normal);
	}

	[Theory]
	[InlineData(DegradationLevel.Normal, "Normal")]
	[InlineData(DegradationLevel.Minor, "Minor")]
	[InlineData(DegradationLevel.Moderate, "Moderate")]
	[InlineData(DegradationLevel.Major, "Major")]
	[InlineData(DegradationLevel.Severe, "Severe")]
	[InlineData(DegradationLevel.Emergency, "Emergency")]
	public void ConvertToCorrectStringRepresentation(DegradationLevel level, string expected)
	{
		// Assert
		level.ToString().ShouldBe(expected);
	}

	[Theory]
	[InlineData("Normal", DegradationLevel.Normal)]
	[InlineData("Minor", DegradationLevel.Minor)]
	[InlineData("Moderate", DegradationLevel.Moderate)]
	[InlineData("Major", DegradationLevel.Major)]
	[InlineData("Severe", DegradationLevel.Severe)]
	[InlineData("Emergency", DegradationLevel.Emergency)]
	public void ParseFromStringCorrectly(string value, DegradationLevel expected)
	{
		// Act
		var result = Enum.Parse<DegradationLevel>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeComparableBySeverity()
	{
		// Assert - higher values are more severe
		((int)DegradationLevel.Emergency).ShouldBeGreaterThan((int)DegradationLevel.Severe);
		((int)DegradationLevel.Severe).ShouldBeGreaterThan((int)DegradationLevel.Major);
		((int)DegradationLevel.Major).ShouldBeGreaterThan((int)DegradationLevel.Moderate);
		((int)DegradationLevel.Moderate).ShouldBeGreaterThan((int)DegradationLevel.Minor);
		((int)DegradationLevel.Minor).ShouldBeGreaterThan((int)DegradationLevel.Normal);
	}
}
