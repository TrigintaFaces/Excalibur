using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2.Validators;

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
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_idempotency_always_passes()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var result = await sut.ValidateAsync("INT-002", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("INT-002");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_delivery_confirmation_always_passes()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var result = await sut.ValidateAsync("INT-003", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("INT-003");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Return_failure_for_unknown_control()
	{
		var sut = new ProcessingIntegrityControlValidator();

		var result = await sut.ValidateAsync("UNKNOWN", CancellationToken.None).ConfigureAwait(false);

		result.IsEffective.ShouldBeFalse();
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
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
		result.ItemsTested.ShouldBe(10);
	}
}
