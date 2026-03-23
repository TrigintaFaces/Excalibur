// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

/// <summary>
/// Unit tests for <see cref="BatchErrorSeverity"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class ErrorSeverityShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<BatchErrorSeverity>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(BatchErrorSeverity.Info);
		values.ShouldContain(BatchErrorSeverity.Warning);
		values.ShouldContain(BatchErrorSeverity.Error);
		values.ShouldContain(BatchErrorSeverity.Critical);
	}

	[Fact]
	public void Info_HasExpectedValue()
	{
		// Assert
		((int)BatchErrorSeverity.Info).ShouldBe(0);
	}

	[Fact]
	public void Warning_HasExpectedValue()
	{
		// Assert
		((int)BatchErrorSeverity.Warning).ShouldBe(1);
	}

	[Fact]
	public void Error_HasExpectedValue()
	{
		// Assert
		((int)BatchErrorSeverity.Error).ShouldBe(2);
	}

	[Fact]
	public void Critical_HasExpectedValue()
	{
		// Assert
		((int)BatchErrorSeverity.Critical).ShouldBe(3);
	}

	[Fact]
	public void Info_IsDefaultValue()
	{
		// Arrange
		BatchErrorSeverity defaultSeverity = default;

		// Assert
		defaultSeverity.ShouldBe(BatchErrorSeverity.Info);
	}

	[Theory]
	[InlineData(BatchErrorSeverity.Info)]
	[InlineData(BatchErrorSeverity.Warning)]
	[InlineData(BatchErrorSeverity.Error)]
	[InlineData(BatchErrorSeverity.Critical)]
	public void BeDefinedForAllValues(BatchErrorSeverity severity)
	{
		// Assert
		Enum.IsDefined(severity).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, BatchErrorSeverity.Info)]
	[InlineData(1, BatchErrorSeverity.Warning)]
	[InlineData(2, BatchErrorSeverity.Error)]
	[InlineData(3, BatchErrorSeverity.Critical)]
	public void CastFromInt_ReturnsCorrectValue(int value, BatchErrorSeverity expected)
	{
		// Act
		var severity = (BatchErrorSeverity)value;

		// Assert
		severity.ShouldBe(expected);
	}

	[Fact]
	public void HaveCorrectSeverityOrder()
	{
		// Assert - values should increase with severity
		((int)BatchErrorSeverity.Info).ShouldBeLessThan((int)BatchErrorSeverity.Warning);
		((int)BatchErrorSeverity.Warning).ShouldBeLessThan((int)BatchErrorSeverity.Error);
		((int)BatchErrorSeverity.Error).ShouldBeLessThan((int)BatchErrorSeverity.Critical);
	}

	[Fact]
	public void Critical_IsHighestSeverity()
	{
		// Assert
		var maxValue = Enum.GetValues<BatchErrorSeverity>().Max();
		maxValue.ShouldBe(BatchErrorSeverity.Critical);
	}
}
