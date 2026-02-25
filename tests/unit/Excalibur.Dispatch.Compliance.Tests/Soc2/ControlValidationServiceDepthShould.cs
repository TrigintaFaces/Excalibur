using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ControlValidationServiceDepthShould
{
	[Fact]
	public async Task Return_not_configured_for_unknown_control()
	{
		var sut = new ControlValidationService([]);

		var result = await sut.ValidateControlAsync("unknown-ctrl", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("unknown-ctrl");
		result.IsConfigured.ShouldBeFalse();
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBe(0);
		result.ConfigurationIssues.ShouldNotBeEmpty();
		result.ConfigurationIssues[0].ShouldContain("No validator registered");
	}

	[Fact]
	public async Task Delegate_to_validator_for_known_control()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["SEC-001"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([TrustServicesCriterion.CC6_LogicalAccess]);
		A.CallTo(() => validator.ValidateAsync("SEC-001", A<CancellationToken>._))
			.Returns(new ControlValidationResult
			{
				ControlId = "SEC-001",
				IsConfigured = true,
				IsEffective = true,
				EffectivenessScore = 95
			});

		var sut = new ControlValidationService([validator]);

		var result = await sut.ValidateControlAsync("SEC-001", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-001");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.EffectivenessScore.ShouldBe(95);
	}

	[Fact]
	public async Task Validate_criterion_returns_results_for_all_controls()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["SEC-001", "SEC-002"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([TrustServicesCriterion.CC6_LogicalAccess]);
		A.CallTo(() => validator.ValidateAsync(A<string>._, A<CancellationToken>._))
			.Returns(new ControlValidationResult
			{
				ControlId = "SEC-001",
				IsConfigured = true,
				IsEffective = true,
				EffectivenessScore = 90
			});

		var sut = new ControlValidationService([validator]);

		var results = await sut.ValidateCriterionAsync(
			TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task Validate_criterion_returns_empty_for_unknown_criterion()
	{
		var sut = new ControlValidationService([]);

		var results = await sut.ValidateCriterionAsync(
			TrustServicesCriterion.CC1_ControlEnvironment, CancellationToken.None).ConfigureAwait(false);

		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task Run_control_test_returns_failure_for_unknown_control()
	{
		var sut = new ControlValidationService([]);
		var parameters = new ControlTestParameters { SampleSize = 10 };

		var result = await sut.RunControlTestAsync("unknown", parameters, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("unknown");
		result.ItemsTested.ShouldBe(0);
		result.Outcome.ShouldBe(TestOutcome.ControlFailure);
		result.Exceptions.ShouldNotBeEmpty();
		result.Exceptions[0].Description.ShouldContain("No validator registered");
		result.Exceptions[0].Severity.ShouldBe(GapSeverity.Critical);
	}

	[Fact]
	public async Task Run_control_test_delegates_to_validator()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["SEC-001"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([TrustServicesCriterion.CC6_LogicalAccess]);
		var testResult = new ControlTestResult
		{
			ControlId = "SEC-001",
			Parameters = new ControlTestParameters { SampleSize = 25 },
			ItemsTested = 25,
			ExceptionsFound = 0,
			Outcome = TestOutcome.NoExceptions
		};
		A.CallTo(() => validator.RunTestAsync("SEC-001", A<ControlTestParameters>._, A<CancellationToken>._))
			.Returns(testResult);

		var sut = new ControlValidationService([validator]);

		var result = await sut.RunControlTestAsync("SEC-001", new ControlTestParameters(), CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-001");
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
	}

	[Fact]
	public void Get_available_controls_returns_all_registered()
	{
		var validator1 = A.Fake<IControlValidator>();
		A.CallTo(() => validator1.SupportedControls).Returns(["SEC-001", "SEC-002"]);
		A.CallTo(() => validator1.SupportedCriteria).Returns([]);
		var validator2 = A.Fake<IControlValidator>();
		A.CallTo(() => validator2.SupportedControls).Returns(["AVL-001"]);
		A.CallTo(() => validator2.SupportedCriteria).Returns([]);

		var sut = new ControlValidationService([validator1, validator2]);

		var controls = sut.GetAvailableControls();

		controls.Count.ShouldBe(3);
		controls.ShouldContain("SEC-001");
		controls.ShouldContain("SEC-002");
		controls.ShouldContain("AVL-001");
	}

	[Fact]
	public void Get_available_controls_returns_empty_when_no_validators()
	{
		var sut = new ControlValidationService([]);

		var controls = sut.GetAvailableControls();

		controls.ShouldBeEmpty();
	}

	[Fact]
	public void Get_controls_for_criterion_returns_matching_controls()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["SEC-001"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([TrustServicesCriterion.CC6_LogicalAccess]);

		var sut = new ControlValidationService([validator]);

		var controls = sut.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess);

		controls.ShouldContain("SEC-001");
	}

	[Fact]
	public void Get_controls_for_criterion_returns_empty_for_unknown()
	{
		var sut = new ControlValidationService([]);

		var controls = sut.GetControlsForCriterion(TrustServicesCriterion.CC1_ControlEnvironment);

		controls.ShouldBeEmpty();
	}

	[Fact]
	public void Last_validator_wins_for_same_control_id()
	{
		var validator1 = A.Fake<IControlValidator>();
		A.CallTo(() => validator1.SupportedControls).Returns(["SEC-001"]);
		A.CallTo(() => validator1.SupportedCriteria).Returns([]);
		var validator2 = A.Fake<IControlValidator>();
		A.CallTo(() => validator2.SupportedControls).Returns(["SEC-001"]);
		A.CallTo(() => validator2.SupportedCriteria).Returns([]);

		var sut = new ControlValidationService([validator1, validator2]);

		// Should have only one entry for SEC-001
		var controls = sut.GetAvailableControls();
		controls.Count.ShouldBe(1);
	}

	[Fact]
	public void Map_multiple_criteria_for_single_validator()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["SEC-001"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([
			TrustServicesCriterion.CC6_LogicalAccess,
			TrustServicesCriterion.CC7_SystemOperations
		]);

		var sut = new ControlValidationService([validator]);

		sut.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess).ShouldContain("SEC-001");
		sut.GetControlsForCriterion(TrustServicesCriterion.CC7_SystemOperations).ShouldContain("SEC-001");
	}
}
