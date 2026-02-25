// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// Unit tests for <see cref="SecuritySeverity"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecuritySeverityShould
{
	[Fact]
	public void HaveLowAsZero()
	{
		// Assert
		((int)SecuritySeverity.Low).ShouldBe(0);
	}

	[Fact]
	public void HaveMediumAsOne()
	{
		// Assert
		((int)SecuritySeverity.Medium).ShouldBe(1);
	}

	[Fact]
	public void HaveHighAsTwo()
	{
		// Assert
		((int)SecuritySeverity.High).ShouldBe(2);
	}

	[Fact]
	public void HaveCriticalAsThree()
	{
		// Assert
		((int)SecuritySeverity.Critical).ShouldBe(3);
	}

	[Fact]
	public void HaveFourDefinedValues()
	{
		// Arrange
		var values = Enum.GetValues<SecuritySeverity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void DefaultToLow()
	{
		// Arrange & Act
		var defaultValue = default(SecuritySeverity);

		// Assert
		defaultValue.ShouldBe(SecuritySeverity.Low);
	}

	[Theory]
	[InlineData(SecuritySeverity.Low, "Low")]
	[InlineData(SecuritySeverity.Medium, "Medium")]
	[InlineData(SecuritySeverity.High, "High")]
	[InlineData(SecuritySeverity.Critical, "Critical")]
	public void HaveCorrectStringRepresentation(SecuritySeverity severity, string expected)
	{
		// Act
		var result = severity.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("Low", SecuritySeverity.Low)]
	[InlineData("Medium", SecuritySeverity.Medium)]
	[InlineData("High", SecuritySeverity.High)]
	[InlineData("Critical", SecuritySeverity.Critical)]
	public void ParseFromString(string value, SecuritySeverity expected)
	{
		// Act
		var result = Enum.Parse<SecuritySeverity>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void SupportOrderedComparison()
	{
		// Assert
		((int)SecuritySeverity.Low).ShouldBeLessThan((int)SecuritySeverity.Medium);
		((int)SecuritySeverity.Medium).ShouldBeLessThan((int)SecuritySeverity.High);
		((int)SecuritySeverity.High).ShouldBeLessThan((int)SecuritySeverity.Critical);
	}
}
