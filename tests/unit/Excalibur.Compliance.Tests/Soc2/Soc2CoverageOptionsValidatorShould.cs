using Excalibur.Compliance;
using Excalibur.Compliance.Soc2;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// A category enabled here that nothing can assess produces a report which enumerates it and
/// substantiates none of it. The report says so; the person who wrote the configuration never reads
/// the report. These arms check the signal reaches the configuration site.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2CoverageOptionsValidatorShould
{
	[Fact]
	public void Fail_when_an_enabled_category_has_no_validator_that_can_assess_it()
	{
		// Availability is enabled; the only registered validator covers Security.
		var sut = new Soc2CoverageOptionsValidator([ValidatorFor(TrustServicesCriterion.CC6_LogicalAccess)]);

		var result = sut.Validate(
			name: null,
			new Soc2Options { EnabledCategories = [TrustServicesCategory.Availability] });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Availability");
	}

	[Fact]
	public void Succeed_when_a_registered_validator_covers_the_enabled_category()
	{
		// The liveness half: the check must not reject a configuration that is fine, or it is just a
		// different way of failing everyone.
		var sut = new Soc2CoverageOptionsValidator([ValidatorFor(TrustServicesCriterion.A3_BackupRecovery)]);

		var result = sut.Validate(
			name: null,
			new Soc2Options { EnabledCategories = [TrustServicesCategory.Availability] });

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_on_PARTIAL_coverage_because_validators_are_opt_in()
	{
		// Deliberate, and the line the whole check is drawn at. Availability has three criteria; one
		// covered is the ordinary case for an opt-in model, and failing it would punish the consumers
		// doing the work. Only ZERO coverage is a configuration mistake.
		var sut = new Soc2CoverageOptionsValidator([ValidatorFor(TrustServicesCriterion.A1_InfrastructureManagement)]);

		var result = sut.Validate(
			name: null,
			new Soc2Options { EnabledCategories = [TrustServicesCategory.Availability] });

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Refuse_when_the_options_are_resolved_not_when_a_report_is_generated()
	{
		// The wiring is what is under test here, not the predicate: a correct validator that nothing
		// registers is inert. Named for what it actually establishes -- the refusal happens when the
		// options are RESOLVED, which is upstream of any report. It does not exercise host start, so
		// it must not claim to; ValidateOnStart is registered alongside and moves that same failure
		// earlier still, to startup.
		var services = new ServiceCollection();
		_ = services.AddSoc2Compliance(o => o.EnabledCategories = [TrustServicesCategory.Availability]);

		using var provider = services.BuildServiceProvider(
			new ServiceProviderOptions { ValidateOnBuild = false });

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<Soc2Options>>().Value);

		ex.Message.ShouldContain("Availability");
	}

	private static IControlValidator ValidatorFor(TrustServicesCriterion criterion)
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedCriteria).Returns([criterion]);
		return validator;
	}
}
