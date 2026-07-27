using Excalibur.Compliance;
using Excalibur.Compliance.Soc2.Validators;

namespace Excalibur.Compliance.Tests.Soc2.Validators;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ConfidentialityControlValidatorShould
{
	private readonly IEncryptionProvider _encryptionProvider = A.Fake<IEncryptionProvider>();
	private readonly IErasureService _erasureService = A.Fake<IErasureService>();

	[Fact]
	public void Return_three_supported_controls()
	{
		var sut = new ConfidentialityControlValidator(_encryptionProvider, _erasureService);

		sut.SupportedControls.Count.ShouldBe(3);
		sut.SupportedControls.ShouldContain("CNF-001");
		sut.SupportedControls.ShouldContain("CNF-002");
		sut.SupportedControls.ShouldContain("CNF-003");
	}

	[Fact]
	public void Return_supported_criteria()
	{
		var sut = new ConfidentialityControlValidator(_encryptionProvider, _erasureService);

		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.C1_DataClassification);
		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.C2_DataProtection);
		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.C3_DataDisposal);
	}

	[Fact]
	public async Task Validate_data_classification_always_passes()
	{
		var sut = new ConfidentialityControlValidator();

		var result = await sut.ValidateAsync("CNF-001", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("CNF-001");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_data_protection_with_encryption_provider()
	{
		var sut = new ConfidentialityControlValidator(_encryptionProvider);

		var result = await sut.ValidateAsync("CNF-002", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("CNF-002");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
	}

	[Fact]
	public async Task Fail_data_protection_without_encryption_provider()
	{
		var sut = new ConfidentialityControlValidator(encryptionProvider: null);

		var result = await sut.ValidateAsync("CNF-002", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("CNF-002");
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("not configured"));
	}

	[Fact]
	public async Task Validate_data_disposal_with_erasure_service()
	{
		var sut = new ConfidentialityControlValidator(erasureService: _erasureService);

		var result = await sut.ValidateAsync("CNF-003", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("CNF-003");
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldContain(e => e.Description.Contains("Cryptographic erasure"));
	}

	[Fact]
	public async Task Surface_the_gap_when_data_disposal_has_no_erasure_service()
	{
		var sut = new ConfidentialityControlValidator(erasureService: null);

		var result = await sut.ValidateAsync("CNF-003", CancellationToken.None).ConfigureAwait(false);

		// CNF-003's declared disposal control is automated cryptographic erasure. With no IErasureService
		// the declared mechanism is absent, so the result must SURFACE the gap (fail-closed-with-visibility)
		// rather than launder a PASS on unverifiable manual procedures.
		result.ControlId.ShouldBe("CNF-003");
		result.IsEffective.ShouldBeFalse();
		// The issue must name the missing dependency AND say the declared control is unverified. It must NOT
		// carry an internal document reference: this text is emitted into the consumer's audit evidence, and
		// asserting on such a reference here is what kept one in the shipped output.
		result.ConfigurationIssues.ShouldContain(i =>
			i.Contains("IErasureService", StringComparison.Ordinal)
			&& i.Contains("unverified", StringComparison.Ordinal));
		result.ConfigurationIssues.ShouldNotContain(i => i.Contains("ADR-", StringComparison.Ordinal));
		// Partial score: compensating manual procedures may exist, but the declared control is unverified.
		result.EffectivenessScore.ShouldBe(40);
	}

	[Fact]
	public async Task Return_failure_for_unknown_control()
	{
		var sut = new ConfidentialityControlValidator(_encryptionProvider, _erasureService);

		var result = await sut.ValidateAsync("UNKNOWN", CancellationToken.None).ConfigureAwait(false);

		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("Unknown control"));
	}

	[Fact]
	public void Return_control_description_for_cnf_001()
	{
		var sut = new ConfidentialityControlValidator();

		var description = sut.GetControlDescription("CNF-001");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("CNF-001");
		description.Name.ShouldBe("Data Classification");
		description.Type.ShouldBe(ControlType.Preventive);
		description.Frequency.ShouldBe(ControlFrequency.Continuous);
	}

	[Fact]
	public void Return_control_description_for_cnf_002()
	{
		var sut = new ConfidentialityControlValidator();

		var description = sut.GetControlDescription("CNF-002");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("CNF-002");
		description.Name.ShouldBe("Data Protection");
	}

	[Fact]
	public void Return_control_description_for_cnf_003()
	{
		var sut = new ConfidentialityControlValidator();

		var description = sut.GetControlDescription("CNF-003");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("CNF-003");
		description.Name.ShouldBe("Data Disposal");
		description.Type.ShouldBe(ControlType.Corrective);
		description.Frequency.ShouldBe(ControlFrequency.OnDemand);
	}

	[Fact]
	public void Return_null_description_for_unknown_control()
	{
		var sut = new ConfidentialityControlValidator();

		var description = sut.GetControlDescription("UNKNOWN");

		description.ShouldBeNull();
	}

	[Fact]
	public async Task Run_test_delegates_to_validation()
	{
		var sut = new ConfidentialityControlValidator(_encryptionProvider, _erasureService);
		var parameters = new ControlTestParameters { SampleSize = 10 };

		var result = await sut.RunTestAsync("CNF-001", parameters, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("CNF-001");
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
		result.ItemsTested.ShouldBe(10);
	}
}
