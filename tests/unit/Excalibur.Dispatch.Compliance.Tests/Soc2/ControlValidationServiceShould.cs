using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ControlValidationServiceShould
{
	[Fact]
	public async Task Validate_registered_control()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["CC1.1"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([TrustServicesCriterion.CC1_ControlEnvironment]);
		A.CallTo(() => validator.ValidateAsync("CC1.1", A<CancellationToken>._))
			.Returns(new ControlValidationResult
			{
				ControlId = "CC1.1",
				IsConfigured = true,
				IsEffective = true,
				EffectivenessScore = 90
			});

		var sut = new ControlValidationService([validator]);

		var result = await sut.ValidateControlAsync("CC1.1", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("CC1.1");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
	}

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
	}

	[Fact]
	public async Task Validate_criterion_delegates_to_controls()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["CC1.1", "CC1.2"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([TrustServicesCriterion.CC1_ControlEnvironment]);
		A.CallTo(() => validator.ValidateAsync(A<string>._, A<CancellationToken>._))
			.Returns(new ControlValidationResult
			{
				ControlId = "test",
				IsConfigured = true,
				IsEffective = true,
				EffectivenessScore = 85
			});

		var sut = new ControlValidationService([validator]);

		var results = await sut.ValidateCriterionAsync(
			TrustServicesCriterion.CC1_ControlEnvironment, CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task Return_empty_for_unknown_criterion()
	{
		var sut = new ControlValidationService([]);

		var results = await sut.ValidateCriterionAsync(
			TrustServicesCriterion.CC1_ControlEnvironment, CancellationToken.None).ConfigureAwait(false);

		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task Run_control_test_for_registered_control()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["CC1.1"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([]);
		var parameters = new ControlTestParameters { SampleSize = 10 };
		A.CallTo(() => validator.RunTestAsync("CC1.1", parameters, A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "CC1.1",
				Parameters = parameters,
				ItemsTested = 10,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions
			});

		var sut = new ControlValidationService([validator]);

		var result = await sut.RunControlTestAsync("CC1.1", parameters, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("CC1.1");
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
	}

	[Fact]
	public async Task Return_failure_test_for_unknown_control()
	{
		var sut = new ControlValidationService([]);
		var parameters = new ControlTestParameters { SampleSize = 10 };

		var result = await sut.RunControlTestAsync("unknown", parameters, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("unknown");
		result.Outcome.ShouldBe(TestOutcome.ControlFailure);
		result.Exceptions.ShouldNotBeEmpty();
	}

	[Fact]
	public void Return_available_controls()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["CC1.1", "CC2.1"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([]);

		var sut = new ControlValidationService([validator]);

		var controls = sut.GetAvailableControls();

		controls.Count.ShouldBe(2);
		controls.ShouldContain("CC1.1");
		controls.ShouldContain("CC2.1");
	}

	[Fact]
	public void Return_controls_for_criterion()
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedControls).Returns(["CC1.1"]);
		A.CallTo(() => validator.SupportedCriteria).Returns([TrustServicesCriterion.CC1_ControlEnvironment]);

		var sut = new ControlValidationService([validator]);

		var controls = sut.GetControlsForCriterion(TrustServicesCriterion.CC1_ControlEnvironment);

		controls.ShouldHaveSingleItem();
		controls[0].ShouldBe("CC1.1");
	}

	[Fact]
	public void Return_empty_for_criterion_with_no_controls()
	{
		var sut = new ControlValidationService([]);

		var controls = sut.GetControlsForCriterion(TrustServicesCriterion.CC1_ControlEnvironment);

		controls.ShouldBeEmpty();
	}

	[Fact]
	public void Handle_multiple_validators()
	{
		var validator1 = A.Fake<IControlValidator>();
		A.CallTo(() => validator1.SupportedControls).Returns(["CC1.1"]);
		A.CallTo(() => validator1.SupportedCriteria).Returns([TrustServicesCriterion.CC1_ControlEnvironment]);

		var validator2 = A.Fake<IControlValidator>();
		A.CallTo(() => validator2.SupportedControls).Returns(["CC5.1"]);
		A.CallTo(() => validator2.SupportedCriteria).Returns([TrustServicesCriterion.CC5_ControlActivities]);

		var sut = new ControlValidationService([validator1, validator2]);

		sut.GetAvailableControls().Count.ShouldBe(2);
		sut.GetControlsForCriterion(TrustServicesCriterion.CC1_ControlEnvironment).ShouldHaveSingleItem();
		sut.GetControlsForCriterion(TrustServicesCriterion.CC5_ControlActivities).ShouldHaveSingleItem();
	}
}
