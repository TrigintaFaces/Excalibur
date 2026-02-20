// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextDiagnosticIssue" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Observability")]
[Trait("Feature", "Context")]
public sealed class ContextDiagnosticIssueShould : UnitTestBase
{
	#region Property Tests

	[Fact]
	public void Create_WithRequiredProperties_StoresValues()
	{
		// Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Warning,
			Category = "Validation",
			Description = "Field value exceeds recommended length"
		};

		// Assert
		issue.Severity.ShouldBe(DiagnosticSeverity.Warning);
		issue.Category.ShouldBe("Validation");
		issue.Description.ShouldBe("Field value exceeds recommended length");
	}

	[Fact]
	public void HaveNullFieldByDefault()
	{
		// Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Information,
			Category = "General",
			Description = "Test"
		};

		// Assert
		issue.Field.ShouldBeNull();
	}

	[Fact]
	public void HaveNullRecommendationByDefault()
	{
		// Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Information,
			Category = "General",
			Description = "Test"
		};

		// Assert
		issue.Recommendation.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingField()
	{
		// Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Error,
			Category = "Structure",
			Description = "Circular reference detected",
			Field = "Parent.Child.Parent"
		};

		// Assert
		issue.Field.ShouldBe("Parent.Child.Parent");
	}

	[Fact]
	public void AllowSettingRecommendation()
	{
		// Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Warning,
			Category = "Performance",
			Description = "Large context detected",
			Recommendation = "Consider using claim-check pattern"
		};

		// Assert
		issue.Recommendation.ShouldBe("Consider using claim-check pattern");
	}

	[Fact]
	public void AllowAllSeverityLevels()
	{
		// Arrange & Act
		var information = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Information,
			Category = "Test",
			Description = "Info"
		};

		var warning = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Warning,
			Category = "Test",
			Description = "Warning"
		};

		var error = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Error,
			Category = "Test",
			Description = "Error"
		};

		// Assert
		information.Severity.ShouldBe(DiagnosticSeverity.Information);
		warning.Severity.ShouldBe(DiagnosticSeverity.Warning);
		error.Severity.ShouldBe(DiagnosticSeverity.Error);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Act
		var issue = new ContextDiagnosticIssue
		{
			Severity = DiagnosticSeverity.Error,
			Category = "Security",
			Description = "Sensitive data detected in context",
			Field = "UserInfo.Password",
			Recommendation = "Remove sensitive fields from context or encrypt"
		};

		// Assert
		issue.Severity.ShouldBe(DiagnosticSeverity.Error);
		issue.Category.ShouldBe("Security");
		issue.Description.ShouldBe("Sensitive data detected in context");
		issue.Field.ShouldBe("UserInfo.Password");
		issue.Recommendation.ShouldBe("Remove sensitive fields from context or encrypt");
	}

	#endregion
}
