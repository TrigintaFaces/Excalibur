// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="DiagnosticSeverity"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class DiagnosticSeverityShould : UnitTestBase
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<DiagnosticSeverity>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(DiagnosticSeverity.Information);
		values.ShouldContain(DiagnosticSeverity.Warning);
		values.ShouldContain(DiagnosticSeverity.Error);
	}

	[Fact]
	public void Information_HasExpectedValue()
	{
		// Assert
		((int)DiagnosticSeverity.Information).ShouldBe(0);
	}

	[Fact]
	public void Warning_HasExpectedValue()
	{
		// Assert
		((int)DiagnosticSeverity.Warning).ShouldBe(1);
	}

	[Fact]
	public void Error_HasExpectedValue()
	{
		// Assert
		((int)DiagnosticSeverity.Error).ShouldBe(2);
	}

	[Fact]
	public void Information_IsDefaultValue()
	{
		// Arrange
		DiagnosticSeverity defaultSeverity = default;

		// Assert
		defaultSeverity.ShouldBe(DiagnosticSeverity.Information);
	}

	[Theory]
	[InlineData(DiagnosticSeverity.Information)]
	[InlineData(DiagnosticSeverity.Warning)]
	[InlineData(DiagnosticSeverity.Error)]
	public void BeDefinedForAllValues(DiagnosticSeverity severity)
	{
		// Assert
		Enum.IsDefined(severity).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, DiagnosticSeverity.Information)]
	[InlineData(1, DiagnosticSeverity.Warning)]
	[InlineData(2, DiagnosticSeverity.Error)]
	public void CastFromInt_ReturnsCorrectValue(int value, DiagnosticSeverity expected)
	{
		// Act
		var severity = (DiagnosticSeverity)value;

		// Assert
		severity.ShouldBe(expected);
	}

	[Fact]
	public void HaveOrderedSeverityValues()
	{
		// Assert - Values should be ordered from least to most severe
		(DiagnosticSeverity.Information < DiagnosticSeverity.Warning).ShouldBeTrue();
		(DiagnosticSeverity.Warning < DiagnosticSeverity.Error).ShouldBeTrue();
	}
}
