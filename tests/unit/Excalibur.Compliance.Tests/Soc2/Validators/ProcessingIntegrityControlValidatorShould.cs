using Excalibur.Compliance;
using Excalibur.Compliance.Soc2.Validators;

namespace Excalibur.Compliance.Tests.Soc2.Validators;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ProcessingIntegrityControlValidatorShould
{
	[Fact]
	public void Return_three_supported_controls()
	{
		var sut = new ProcessingIntegrityControlValidator();

		sut.SupportedControls.Count.ShouldBe(3);
		sut.SupportedControls.ShouldContain("INT-001");
		sut.SupportedControls.ShouldContain("INT-002");
		sut.SupportedControls.ShouldContain("INT-003");
	}

	[Fact]
	public void Return_supported_criteria()
	{
		var sut = new ProcessingIntegrityControlValidator();

		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.PI1_InputValidation);
		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.PI2_ProcessingAccuracy);
		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.PI3_OutputCompleteness);
	}

	[Fact]
	public async Task Validate_input_validation_always_passes()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var result = await sut.ValidateAsync("INT-001", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("INT-001");
		// This arm was named "always passes" and required exactly that: Effective at full
		// score from a method that observes nothing. The capability really is shipped, which is
		// what the Configuration evidence says; whether this deployment operates it is not
		// observable from here, so the control is unverified rather than effective.
		result.Outcome.ShouldNotBe(ControlOutcome.Effective);
		// 1..99 meant "neither absent nor effective". Over a closed band set that is
		// exactly these two exclusions, and it names the facts excluded rather than
		// describing a range on a scale the value never lived on.
		result.EffectivenessScore.ShouldNotBe(ControlEffectiveness.MechanismAbsent);
		result.EffectivenessScore.ShouldNotBe(ControlEffectiveness.Effective);
		result.ConfigurationIssues.ShouldNotBeEmpty();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_idempotency_always_passes()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var result = await sut.ValidateAsync("INT-002", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("INT-002");
		// This arm was named "always passes" and required exactly that: Effective at full
		// score from a method that observes nothing. The capability really is shipped, which is
		// what the Configuration evidence says; whether this deployment operates it is not
		// observable from here, so the control is unverified rather than effective.
		result.Outcome.ShouldNotBe(ControlOutcome.Effective);
		// 1..99 meant "neither absent nor effective". Over a closed band set that is
		// exactly these two exclusions, and it names the facts excluded rather than
		// describing a range on a scale the value never lived on.
		result.EffectivenessScore.ShouldNotBe(ControlEffectiveness.MechanismAbsent);
		result.EffectivenessScore.ShouldNotBe(ControlEffectiveness.Effective);
		result.ConfigurationIssues.ShouldNotBeEmpty();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_delivery_confirmation_always_passes()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var result = await sut.ValidateAsync("INT-003", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("INT-003");
		// This arm was named "always passes" and required exactly that: Effective at full
		// score from a method that observes nothing. The capability really is shipped, which is
		// what the Configuration evidence says; whether this deployment operates it is not
		// observable from here, so the control is unverified rather than effective.
		result.Outcome.ShouldNotBe(ControlOutcome.Effective);
		// 1..99 meant "neither absent nor effective". Over a closed band set that is
		// exactly these two exclusions, and it names the facts excluded rather than
		// describing a range on a scale the value never lived on.
		result.EffectivenessScore.ShouldNotBe(ControlEffectiveness.MechanismAbsent);
		result.EffectivenessScore.ShouldNotBe(ControlEffectiveness.Effective);
		result.ConfigurationIssues.ShouldNotBeEmpty();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Return_failure_for_unknown_control()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var result = await sut.ValidateAsync("UNKNOWN", CancellationToken.None).ConfigureAwait(false);

		result.Outcome.ShouldNotBe(ControlOutcome.Effective);
		result.ConfigurationIssues.ShouldContain(i => i.Contains("Unknown control"));
	}

	[Fact]
	public void Return_control_description_for_int_001()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var description = sut.GetControlDescription("INT-001");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("INT-001");
		description.Name.ShouldBe("Input Validation");
		description.Type.ShouldBe(ControlType.Preventive);
		description.Frequency.ShouldBe(ControlFrequency.PerTransaction);
	}

	[Fact]
	public void Return_control_description_for_int_002()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var description = sut.GetControlDescription("INT-002");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("INT-002");
		description.Name.ShouldBe("Idempotency");
	}

	[Fact]
	public void Return_control_description_for_int_003()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var description = sut.GetControlDescription("INT-003");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("INT-003");
		description.Name.ShouldBe("Delivery Confirmation");
		description.Type.ShouldBe(ControlType.Detective);
	}

	[Fact]
	public void Return_null_description_for_unknown_control()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var description = sut.GetControlDescription("UNKNOWN");

		description.ShouldBeNull();
	}

	[Fact]
	public async Task Run_test_delegates_to_validation()
	{
		var sut = new ProcessingIntegrityControlValidator();
		var parameters = new ControlTestParameters { SampleSize = 10 };

		var result = await sut.RunTestAsync("INT-001", parameters, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("INT-001");
		// The verdict, not NotTested: a validator ran. What did not happen is SAMPLING, which the
		// item count and the null finding count below carry.
		result.Outcome.ShouldBe(TestOutcome.NotTested);
		// Zero, not the requested 10: RunTestAsync forwards a verdict and samples nothing. The
		// requested size stays available on Parameters, where it is a request rather than a measurement.
		result.ItemsTested.ShouldBe(0);
	}
}
