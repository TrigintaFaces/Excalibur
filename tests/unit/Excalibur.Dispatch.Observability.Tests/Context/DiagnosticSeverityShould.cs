// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="DiagnosticSeverity"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class DiagnosticSeverityShould
{
	[Theory]
	[InlineData(DiagnosticSeverity.Information, 0)]
	[InlineData(DiagnosticSeverity.Warning, 1)]
	[InlineData(DiagnosticSeverity.Error, 2)]
	public void HaveCorrectIntegerValue(DiagnosticSeverity severity, int expectedValue)
	{
		// Assert
		((int)severity).ShouldBe(expectedValue);
	}

	[Fact]
	public void HaveThreeValues()
	{
		// Assert
		Enum.GetValues<DiagnosticSeverity>().ShouldBe([
			DiagnosticSeverity.Information,
			DiagnosticSeverity.Warning,
			DiagnosticSeverity.Error,
		]);
	}

	[Theory]
	[InlineData("Information", DiagnosticSeverity.Information)]
	[InlineData("Warning", DiagnosticSeverity.Warning)]
	[InlineData("Error", DiagnosticSeverity.Error)]
	public void ParseFromString(string value, DiagnosticSeverity expected)
	{
		// Act & Assert
		Enum.Parse<DiagnosticSeverity>(value).ShouldBe(expected);
	}

	[Theory]
	[InlineData(DiagnosticSeverity.Information, "Information")]
	[InlineData(DiagnosticSeverity.Warning, "Warning")]
	[InlineData(DiagnosticSeverity.Error, "Error")]
	public void ConvertToString(DiagnosticSeverity severity, string expected)
	{
		// Act & Assert
		severity.ToString().ShouldBe(expected);
	}

	[Fact]
	public void DefaultToInformation()
	{
		// Arrange
		DiagnosticSeverity defaultValue = default;

		// Assert
		defaultValue.ShouldBe(DiagnosticSeverity.Information);
	}
}
