// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PipelineConfigTypesShould
{
	// --- PipelineComplexity ---

	[Fact]
	public void PipelineComplexity_HaveExpectedValues()
	{
		// Assert
		PipelineComplexity.Standard.ShouldBe((PipelineComplexity)0);
		PipelineComplexity.Reduced.ShouldBe((PipelineComplexity)1);
		PipelineComplexity.Minimal.ShouldBe((PipelineComplexity)2);
		PipelineComplexity.Direct.ShouldBe((PipelineComplexity)3);
	}

	[Fact]
	public void PipelineComplexity_HaveFourValues()
	{
		// Act
		var values = Enum.GetValues<PipelineComplexity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void PipelineComplexity_DefaultToStandard()
	{
		// Arrange
		PipelineComplexity complexity = default;

		// Assert
		complexity.ShouldBe(PipelineComplexity.Standard);
	}

	// --- Configuration.ValidationSeverity ---

	[Fact]
	public void ConfigValidationSeverity_HaveExpectedValues()
	{
		// Assert
		ValidationSeverity.Info
			.ShouldBe((ValidationSeverity)0);
		ValidationSeverity.Warning
			.ShouldBe((ValidationSeverity)1);
		ValidationSeverity.Error
			.ShouldBe((ValidationSeverity)2);
	}

	[Fact]
	public void ConfigValidationSeverity_HaveFourValues()
	{
		// Act
		var values = Enum.GetValues<ValidationSeverity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	// --- ValidationIssue ---

	[Fact]
	public void ValidationIssue_SetProperties()
	{
		// Act
		var issue = new ValidationIssue(
			ValidationSeverity.Error,
			"Missing required middleware");

		// Assert
		issue.Severity.ShouldBe(ValidationSeverity.Error);
		issue.Message.ShouldBe("Missing required middleware");
	}

	[Fact]
	public void ValidationIssue_RecordEquality_SameValues_AreEqual()
	{
		// Arrange
		var issue1 = new ValidationIssue(
			ValidationSeverity.Warning,
			"test");
		var issue2 = new ValidationIssue(
			ValidationSeverity.Warning,
			"test");

		// Assert
		issue1.ShouldBe(issue2);
		(issue1 == issue2).ShouldBeTrue();
	}

	[Fact]
	public void ValidationIssue_RecordEquality_DifferentValues_AreNotEqual()
	{
		// Arrange
		var issue1 = new ValidationIssue(
			ValidationSeverity.Warning,
			"test");
		var issue2 = new ValidationIssue(
			ValidationSeverity.Error,
			"test");

		// Assert
		issue1.ShouldNotBe(issue2);
		(issue1 != issue2).ShouldBeTrue();
	}

	// --- PipelineValidationResult ---

	[Fact]
	public void PipelineValidationResult_DefaultValues()
	{
		// Act
		var result = new PipelineValidationResult();

		// Assert
		result.IsOptimized.ShouldBeFalse();
		result.Complexity.ShouldBe(PipelineComplexity.Standard);
		result.Notes.ShouldNotBeNull();
		result.Notes.ShouldBeEmpty();
	}

	[Fact]
	public void PipelineValidationResult_SetProperties()
	{
		// Act
		var result = new PipelineValidationResult
		{
			IsOptimized = true,
			Complexity = PipelineComplexity.Minimal,
		};
		result.Notes.Add("Pipeline is optimized for low latency");

		// Assert
		result.IsOptimized.ShouldBeTrue();
		result.Complexity.ShouldBe(PipelineComplexity.Minimal);
		result.Notes.Count.ShouldBe(1);
		result.Notes[0].ShouldBe("Pipeline is optimized for low latency");
	}

	// --- SynthesisResult ---

	[Fact]
	public void SynthesisResult_SetProperties()
	{
		// Arrange
		var profiles = new Dictionary<string, IPipelineProfile>();
		var mappings = new Dictionary<MessageKinds, string>();
		var issues = Array.Empty<ValidationIssue>();

		// Act
		var result = new SynthesisResult(profiles, mappings, issues);

		// Assert
		result.Profiles.ShouldNotBeNull();
		result.Mappings.ShouldNotBeNull();
		result.ValidationIssues.ShouldNotBeNull();
		result.ValidationIssues.ShouldBeEmpty();
		result.HasErrors.ShouldBeFalse();
		result.HasWarnings.ShouldBeFalse();
		result.Errors.ShouldBeEmpty();
		result.Warnings.ShouldBeEmpty();
	}

	[Fact]
	public void SynthesisResult_ThrowOnNullProfiles()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new SynthesisResult(
			null!,
			new Dictionary<MessageKinds, string>(),
			[]));
	}

	[Fact]
	public void SynthesisResult_ThrowOnNullMappings()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			null!,
			[]));
	}

	[Fact]
	public void SynthesisResult_ThrowOnNullValidationIssues()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			null!));
	}

	[Fact]
	public void SynthesisResult_HasErrors_WhenErrorIssuePresent()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(
				ValidationSeverity.Error,
				"Critical error"),
		};

		// Act
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Assert
		result.HasErrors.ShouldBeTrue();
		result.Errors.Count().ShouldBe(1);
	}

	[Fact]
	public void SynthesisResult_HasWarnings_WhenWarningIssuePresent()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(
				ValidationSeverity.Warning,
				"Non-critical warning"),
		};

		// Act
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Assert
		result.HasWarnings.ShouldBeTrue();
		result.HasErrors.ShouldBeFalse();
		result.Warnings.Count().ShouldBe(1);
	}

	[Fact]
	public void SynthesisResult_FilterIssuesBySeverity()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(
				ValidationSeverity.Info, "Info"),
			new ValidationIssue(
				ValidationSeverity.Warning, "Warn1"),
			new ValidationIssue(
				ValidationSeverity.Warning, "Warn2"),
			new ValidationIssue(
				ValidationSeverity.Error, "Err"),
		};

		// Act
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Assert
		result.ValidationIssues.Length.ShouldBe(4);
		result.Errors.Count().ShouldBe(1);
		result.Warnings.Count().ShouldBe(2);
		result.HasErrors.ShouldBeTrue();
		result.HasWarnings.ShouldBeTrue();
	}
}
