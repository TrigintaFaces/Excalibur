// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

/// <summary>
/// Unit tests for <see cref="ErrorSeverity"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class ErrorSeverityShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<ErrorSeverity>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(ErrorSeverity.Info);
		values.ShouldContain(ErrorSeverity.Warning);
		values.ShouldContain(ErrorSeverity.Error);
		values.ShouldContain(ErrorSeverity.Critical);
	}

	[Fact]
	public void Info_HasExpectedValue()
	{
		// Assert
		((int)ErrorSeverity.Info).ShouldBe(0);
	}

	[Fact]
	public void Warning_HasExpectedValue()
	{
		// Assert
		((int)ErrorSeverity.Warning).ShouldBe(1);
	}

	[Fact]
	public void Error_HasExpectedValue()
	{
		// Assert
		((int)ErrorSeverity.Error).ShouldBe(2);
	}

	[Fact]
	public void Critical_HasExpectedValue()
	{
		// Assert
		((int)ErrorSeverity.Critical).ShouldBe(3);
	}

	[Fact]
	public void Info_IsDefaultValue()
	{
		// Arrange
		ErrorSeverity defaultSeverity = default;

		// Assert
		defaultSeverity.ShouldBe(ErrorSeverity.Info);
	}

	[Theory]
	[InlineData(ErrorSeverity.Info)]
	[InlineData(ErrorSeverity.Warning)]
	[InlineData(ErrorSeverity.Error)]
	[InlineData(ErrorSeverity.Critical)]
	public void BeDefinedForAllValues(ErrorSeverity severity)
	{
		// Assert
		Enum.IsDefined(severity).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, ErrorSeverity.Info)]
	[InlineData(1, ErrorSeverity.Warning)]
	[InlineData(2, ErrorSeverity.Error)]
	[InlineData(3, ErrorSeverity.Critical)]
	public void CastFromInt_ReturnsCorrectValue(int value, ErrorSeverity expected)
	{
		// Act
		var severity = (ErrorSeverity)value;

		// Assert
		severity.ShouldBe(expected);
	}

	[Fact]
	public void HaveCorrectSeverityOrder()
	{
		// Assert - values should increase with severity
		((int)ErrorSeverity.Info).ShouldBeLessThan((int)ErrorSeverity.Warning);
		((int)ErrorSeverity.Warning).ShouldBeLessThan((int)ErrorSeverity.Error);
		((int)ErrorSeverity.Error).ShouldBeLessThan((int)ErrorSeverity.Critical);
	}

	[Fact]
	public void Critical_IsHighestSeverity()
	{
		// Assert
		var maxValue = Enum.GetValues<ErrorSeverity>().Max();
		maxValue.ShouldBe(ErrorSeverity.Critical);
	}
}
