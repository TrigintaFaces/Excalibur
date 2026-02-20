// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextDiagnosticIssue"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextDiagnosticIssueShould
{
	#region Required Property Tests

	[Fact]
	public void RequireSeverity()
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Warning,
			Category = "Validation",
			Description = "Field validation issue",
		};

		// Assert
		issue.Severity.ShouldBe(DiagnosticSeverity.Warning);
	}

	[Fact]
	public void RequireCategory()
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Error,
			Category = "Integrity",
			Description = "Integrity violation",
		};

		// Assert
		issue.Category.ShouldBe("Integrity");
	}

	[Fact]
	public void RequireDescription()
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Information,
			Category = "Diagnostics",
			Description = "Informational diagnostic message",
		};

		// Assert
		issue.Description.ShouldBe("Informational diagnostic message");
	}

	#endregion

	#region Optional Property Tests

	[Fact]
	public void HaveNullFieldByDefault()
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Warning,
			Category = "General",
			Description = "General issue",
		};

		// Assert
		issue.Field.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingField()
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Error,
			Category = "Validation",
			Description = "Invalid field value",
			Field = "CorrelationId",
		};

		// Assert
		issue.Field.ShouldBe("CorrelationId");
	}

	[Fact]
	public void HaveNullRecommendationByDefault()
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Information,
			Category = "Info",
			Description = "Informational message",
		};

		// Assert
		issue.Recommendation.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingRecommendation()
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Warning,
			Category = "Performance",
			Description = "Context size is large",
			Recommendation = "Consider reducing context size to improve performance",
		};

		// Assert
		issue.Recommendation.ShouldBe("Consider reducing context size to improve performance");
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Error,
			Category = "Integrity",
			Description = "Required field is missing",
			Field = "UserId",
			Recommendation = "Ensure UserId is set before processing",
		};

		// Assert
		issue.Severity.ShouldBe(DiagnosticSeverity.Error);
		issue.Category.ShouldBe("Integrity");
		issue.Description.ShouldBe("Required field is missing");
		issue.Field.ShouldBe("UserId");
		issue.Recommendation.ShouldBe("Ensure UserId is set before processing");
	}

	[Theory]
	[InlineData(DiagnosticSeverity.Information)]
	[InlineData(DiagnosticSeverity.Warning)]
	[InlineData(DiagnosticSeverity.Error)]
	public void SupportAllSeverityLevels(DiagnosticSeverity severity)
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = severity,
			Category = "Test",
			Description = $"Test issue with {severity} severity",
		};

		// Assert
		issue.Severity.ShouldBe(severity);
	}

	[Theory]
	[InlineData("Validation")]
	[InlineData("Integrity")]
	[InlineData("Performance")]
	[InlineData("Security")]
	[InlineData("Configuration")]
	public void SupportVariousCategories(string category)
	{
		// Arrange & Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Warning,
			Category = category,
			Description = $"Issue in {category} category",
		};

		// Assert
		issue.Category.ShouldBe(category);
	}

	#endregion
}
