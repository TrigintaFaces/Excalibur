using Excalibur.Compliance.Soc2.Validators;
using Excalibur.Compliance.Soc2;
using Excalibur.Compliance;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Tests.Soc2.Validators;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class BaseControlValidatorShould
{
	[Fact]
	// Renamed: both arms assert TestOutcome.NotTested, because the base forwards a verdict without
	// testing and the enum has no member for that yet. The old names promised an Outcome DISTINCTION
	// the code does not make, so a reader comparing the two would have concluded the pair was
	// broken. What they genuinely discriminate is the EXCEPTION LIST, and they now say so.
	public async Task RunTestAsyncReportNoExceptionListWhenValidationPasses()
	{
		// Arrange
		var validator = new PassingValidator();
		var parameters = new ControlTestParameters { SampleSize = 10 };

		// Act
		var result = await validator.RunTestAsync("CTRL-001", parameters, CancellationToken.None);

		// Assert
		result.ControlId.ShouldBe("CTRL-001");
		result.Parameters.ShouldBe(parameters);
		// RunTestAsync forwards a ValidateAsync verdict; it samples nothing. It used to report
		// ItemsTested = the REQUESTED sample size (10 here) as though those items had been examined,
		// and a finding count derived from a bool. Zero items examined is true and needs no other
		// state; a finding count has none, because no search ran.
		result.ItemsTested.ShouldBe(0);
		result.ExceptionsFound.ShouldBeNull();
		result.Outcome.ShouldBe(TestOutcome.NotTested);
		result.Exceptions.ShouldBeEmpty();
		result.Evidence.ShouldNotBeNull();
	}

	[Fact]
	public async Task RunTestAsyncReportTheValidationIssuesAsExceptionsWhenValidationFails()
	{
		// Arrange
		var validator = new FailingValidator("Issue A", "Issue B");
		var parameters = new ControlTestParameters { SampleSize = 50 };

		// Act
		var result = await validator.RunTestAsync("CTRL-002", parameters, CancellationToken.None);

		// Assert
		result.ControlId.ShouldBe("CTRL-002");
		result.Parameters.ShouldBe(parameters);
		// RunTestAsync forwards a ValidateAsync verdict; it samples nothing. It used to report
		// ItemsTested = the REQUESTED sample size (50 here) as though those items had been examined,
		// and a finding count derived from a bool. Zero items examined is true and needs no other
		// state; a finding count has none, because no search ran.
		result.ItemsTested.ShouldBe(0);
		result.ExceptionsFound.ShouldBeNull();
		result.Outcome.ShouldBe(TestOutcome.NotTested);
		result.Exceptions.Count.ShouldBe(1);
		result.Exceptions[0].ItemId.ShouldBe("CTRL-002");
		result.Exceptions[0].Description.ShouldBe("Issue A; Issue B");
		result.Exceptions[0].Severity.ShouldBe(GapSeverity.High);
	}

	[Fact]
	public async Task RunTestAsyncPreserveEvidenceFromValidation()
	{
		// Arrange
		var validator = new PassingValidatorWithEvidence();
		var parameters = new ControlTestParameters { SampleSize = 5 };

		// Act
		var result = await validator.RunTestAsync("CTRL-003", parameters, CancellationToken.None);

		// Assert
		result.Evidence.Count.ShouldBe(1);
		result.Evidence[0].Description.ShouldBe("Test evidence");
	}

	[Fact]
	public void CreateSuccessResultShouldSetCorrectDefaults()
	{
		// Arrange
		var validator = new TestableValidator();

		// Act
		var result = validator.InvokeCreateSuccessResult("CTRL-004");

		// Assert
		result.ControlId.ShouldBe("CTRL-004");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.EffectivenessScore.ShouldBe(100);
		result.ConfigurationIssues.ShouldBeEmpty();
		result.Evidence.ShouldBeEmpty();
		result.ValidatedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void CreateSuccessResultShouldIncludeProvidedEvidence()
	{
		// Arrange
		var validator = new TestableValidator();
		var evidence = new List<EvidenceItem>
		{
			new()
			{
				EvidenceId = "ev-1",
				Type = EvidenceType.Configuration,
				Description = "Config verified",
				Source = "unit-test",
				CollectedAt = DateTimeOffset.UtcNow
			}
		};

		// Act
		var result = validator.InvokeCreateSuccessResult("CTRL-005", evidence);

		// Assert
		result.Evidence.Count.ShouldBe(1);
		result.Evidence[0].Description.ShouldBe("Config verified");
	}

	[Fact]
	public void CreateFailureResultShouldSetCorrectProperties()
	{
		// Arrange
		var validator = new TestableValidator();
		var issues = new List<string> { "Missing config", "Invalid setting" };

		// Act
		var result = validator.InvokeCreateFailureResult("CTRL-006", issues, effectivenessScore: 30);

		// Assert
		result.ControlId.ShouldBe("CTRL-006");
		// Not "because there are issues" any more -- the caller did not claim the mechanism is
		// present, so it is not reported as present. The complaint count no longer decides it.
		result.IsConfigured.ShouldBeFalse();
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBe(30);
		result.ConfigurationIssues.Count.ShouldBe(2);
		result.Evidence.ShouldBeEmpty();
	}

	[Fact]
	public void CreateFailureResultShouldNotInferConfigurationFromAnEmptyIssueList()
	{
		// Arrange
		var validator = new TestableValidator();
		var issues = new List<string>();

		// Act
		var result = validator.InvokeCreateFailureResult("CTRL-007", issues);

		// Assert
		// This arm encoded the defect in its own name and comment: an empty issue list was read as
		// evidence that the mechanism is configured. Those are unrelated facts -- a caller can have
		// nothing to complain about and still be looking at a component that is not there.
		result.IsConfigured.ShouldBeFalse();
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBe(0);
	}

	[Fact]
	public void CreateFailureResultShouldIncludeProvidedEvidence()
	{
		// Arrange
		var validator = new TestableValidator();
		var issues = new List<string> { "Problem" };
		var evidence = new List<EvidenceItem>
		{
			new()
			{
				EvidenceId = "ev-2",
				Type = EvidenceType.TestResult,
				Description = "Test failed",
				Source = "unit-test",
				CollectedAt = DateTimeOffset.UtcNow
			}
		};

		// Act
		var result = validator.InvokeCreateFailureResult("CTRL-008", issues, evidence: evidence);

		// Assert
		result.Evidence.Count.ShouldBe(1);
		result.Evidence[0].Description.ShouldBe("Test failed");
	}

	[Fact]
	public void CreateEvidenceShouldSetAllProperties()
	{
		// Arrange
		var validator = new TestableValidator();

		// Act
		var evidence = validator.InvokeCreateEvidence(
			EvidenceType.AuditLog,
			"Audit log entry",
			"compliance-store",
			"ref-123");

		// Assert
		evidence.EvidenceId.ShouldNotBeNullOrWhiteSpace();
		evidence.Type.ShouldBe(EvidenceType.AuditLog);
		evidence.Description.ShouldBe("Audit log entry");
		evidence.Source.ShouldBe("compliance-store");
		evidence.DataReference.ShouldBe("ref-123");
		evidence.CollectedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void CreateEvidenceWithNullDataReferenceShouldSucceed()
	{
		// Arrange
		var validator = new TestableValidator();

		// Act
		var evidence = validator.InvokeCreateEvidence(
			EvidenceType.Configuration,
			"Config check",
			"system");

		// Assert
		evidence.DataReference.ShouldBeNull();
	}

	[Fact]
	public void CreateEvidenceShouldGenerateUniqueIds()
	{
		// Arrange
		var validator = new TestableValidator();

		// Act
		var ev1 = validator.InvokeCreateEvidence(EvidenceType.Configuration, "A", "src");
		var ev2 = validator.InvokeCreateEvidence(EvidenceType.Configuration, "B", "src");

		// Assert
		ev1.EvidenceId.ShouldNotBe(ev2.EvidenceId);
	}

	// ---- Test doubles ----

	/// <summary>
	/// Validator that always passes validation.
	/// </summary>
	private sealed class PassingValidator : BaseControlValidator
	{
		public override IReadOnlyList<string> SupportedControls { get; } = ["CTRL-001"];
		public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria { get; } = [];

		public override Task<ControlValidationResult> ValidateAsync(
			string controlId, CancellationToken cancellationToken)
		{
			return Task.FromResult(CreateSuccessResult(controlId));
		}

		public override ControlDescription? GetControlDescription(string controlId) => null;
	}

	/// <summary>
	/// Validator that always fails validation with the given issues.
	/// </summary>
	private sealed class FailingValidator : BaseControlValidator
	{
		private readonly string[] _issues;

		public FailingValidator(params string[] issues) => _issues = issues;

		public override IReadOnlyList<string> SupportedControls { get; } = ["CTRL-002"];
		public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria { get; } = [];

		public override Task<ControlValidationResult> ValidateAsync(
			string controlId, CancellationToken cancellationToken)
		{
			return Task.FromResult(CreateFailureResult(controlId, _issues));
		}

		public override ControlDescription? GetControlDescription(string controlId) => null;
	}

	/// <summary>
	/// Validator that passes with evidence.
	/// </summary>
	private sealed class PassingValidatorWithEvidence : BaseControlValidator
	{
		public override IReadOnlyList<string> SupportedControls { get; } = ["CTRL-003"];
		public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria { get; } = [];

		public override Task<ControlValidationResult> ValidateAsync(
			string controlId, CancellationToken cancellationToken)
		{
			var evidence = new List<EvidenceItem>
			{
				CreateEvidence(EvidenceType.TestResult, "Test evidence", "unit-test")
			};
			return Task.FromResult(CreateSuccessResult(controlId, evidence));
		}

		public override ControlDescription? GetControlDescription(string controlId) => null;
	}

	/// <summary>
	/// Exposes protected methods for direct testing.
	/// </summary>
	private sealed class TestableValidator : BaseControlValidator
	{
		public override IReadOnlyList<string> SupportedControls { get; } = [];
		public override IReadOnlyList<TrustServicesCriterion> SupportedCriteria { get; } = [];

		public override Task<ControlValidationResult> ValidateAsync(
			string controlId, CancellationToken cancellationToken)
			=> throw new NotImplementedException();

		public override ControlDescription? GetControlDescription(string controlId) => null;

		public ControlValidationResult InvokeCreateSuccessResult(
			string controlId, IReadOnlyList<EvidenceItem>? evidence = null)
			=> CreateSuccessResult(controlId, evidence);

		public ControlValidationResult InvokeCreateFailureResult(
			string controlId,
			IReadOnlyList<string> issues,
			int effectivenessScore = 0,
			IReadOnlyList<EvidenceItem>? evidence = null)
			=> CreateFailureResult(controlId, issues, effectivenessScore, evidence);

		public EvidenceItem InvokeCreateEvidence(
			EvidenceType type,
			string description,
			string source,
			string? dataReference = null)
			=> CreateEvidence(type, description, source, dataReference);
	}
}
