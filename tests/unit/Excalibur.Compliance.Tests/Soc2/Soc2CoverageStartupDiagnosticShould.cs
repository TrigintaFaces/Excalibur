using Excalibur.Compliance;
using Excalibur.Compliance.Soc2;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Tests.Shared.Helpers;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// A category enabled here that nothing can assess produces a report which enumerates it and
/// substantiates none of it. The report says so; the person who wrote the configuration never reads
/// the report. These arms check the signal reaches the configuration site — as a warning, because
/// validators are opt-in and zero coverage is a reportable state rather than a broken one.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2CoverageStartupDiagnosticShould
{
	private const string WarningFragment = "no registered IControlValidator supports";

	[Fact]
	public async Task Warn_when_an_enabled_category_has_no_validator_that_can_assess_it()
	{
		// Availability is enabled; the only registered validator covers Security.
		var logger = new CapturingLogger<Soc2CoverageStartupDiagnostic>();
		var sut = Diagnostic(logger, TrustServicesCategory.Availability, TrustServicesCriterion.CC6_LogicalAccess);

		await sut.StartAsync(TestContext.Current.CancellationToken);

		logger.HasLogged(LogLevel.Warning, "Availability").ShouldBeTrue();
		logger.Entries.ShouldHaveSingleItem().Message.ShouldContain(WarningFragment);
	}

	[Fact]
	public async Task Stay_silent_when_a_registered_validator_covers_the_enabled_category()
	{
		// The liveness half: the diagnostic must not cry wolf over a configuration that is fine, or the
		// warning stops being read at exactly the sites that need it.
		var logger = new CapturingLogger<Soc2CoverageStartupDiagnostic>();
		var sut = Diagnostic(logger, TrustServicesCategory.Availability, TrustServicesCriterion.A3_BackupRecovery);

		await sut.StartAsync(TestContext.Current.CancellationToken);

		logger.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Stay_silent_on_PARTIAL_coverage_because_validators_are_opt_in()
	{
		// Deliberate, and the line the whole check is drawn at. Availability has three criteria; one
		// covered is the ordinary case for an opt-in model, and warning there would punish the consumers
		// doing the work. Only ZERO coverage is worth a word.
		var logger = new CapturingLogger<Soc2CoverageStartupDiagnostic>();
		var sut = Diagnostic(logger, TrustServicesCategory.Availability, TrustServicesCriterion.A1_InfrastructureManagement);

		await sut.StartAsync(TestContext.Current.CancellationToken);

		logger.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Resolve_its_options_and_warn_rather_than_refuse_on_stock_defaults()
	{
		// The wiring arm, and the whole point of the seam: AddSoc2Compliance with nothing further
		// registers no IControlValidator while EnabledCategories defaults to Security. That pairing used
		// to make IOptions<Soc2Options> unresolvable. It must now RESOLVE, and the gap must be reported
		// where it is caused instead.
		var capture = new CapturingLoggerProvider();
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(capture));
		_ = services.AddSoc2Compliance(new ConfigurationBuilder().Build());

		await using var provider = services.BuildServiceProvider(
			new ServiceProviderOptions { ValidateOnBuild = false });

		var options = provider.GetRequiredService<IOptions<Soc2Options>>().Value;
		options.EnabledCategories.ShouldBe([TrustServicesCategory.Security]);

		var diagnostic = provider.GetServices<IHostedService>().OfType<Soc2CoverageStartupDiagnostic>().ShouldHaveSingleItem();
		await diagnostic.StartAsync(TestContext.Current.CancellationToken);

		capture.Entries.ShouldHaveSingleItem().Message.ShouldContain("Security");
		capture.Entries[0].Level.ShouldBe(LogLevel.Warning);
	}

	private static Soc2CoverageStartupDiagnostic Diagnostic(
		ILogger<Soc2CoverageStartupDiagnostic> logger,
		TrustServicesCategory enabled,
		TrustServicesCriterion covered)
	{
		var validator = A.Fake<IControlValidator>();
		A.CallTo(() => validator.SupportedCriteria).Returns([covered]);

		return new Soc2CoverageStartupDiagnostic(
			[validator],
			Options.Create(new Soc2Options { EnabledCategories = [enabled] }),
			logger);
	}
}
