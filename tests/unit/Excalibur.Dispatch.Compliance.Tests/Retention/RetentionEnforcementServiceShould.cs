using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Retention;

public class RetentionEnforcementServiceShould
{
	private readonly RetentionEnforcementOptions _options;
	private readonly RetentionEnforcementService _sut;

	public RetentionEnforcementServiceShould()
	{
		_options = new RetentionEnforcementOptions();
		_sut = new RetentionEnforcementService(
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<RetentionEnforcementService>.Instance);
	}

	[Fact]
	public async Task Enforce_retention_returns_result_without_throwing_type_load_exceptions()
	{
		var result = await _sut.EnforceRetentionAsync(CancellationToken.None);

		result.ShouldNotBeNull();
		result.PoliciesEvaluated.ShouldBeGreaterThanOrEqualTo(0);
		result.RecordsCleaned.ShouldBe(0);
		result.IsDryRun.ShouldBeFalse();
		result.CompletedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task Enforce_retention_with_dry_run()
	{
		_options.DryRun = true;

		var sut = new RetentionEnforcementService(
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<RetentionEnforcementService>.Instance);

		var result = await sut.EnforceRetentionAsync(CancellationToken.None);

		result.IsDryRun.ShouldBeTrue();
	}

	[Fact]
	public async Task Get_retention_policies_returns_list()
	{
		var policies = await _sut.GetRetentionPoliciesAsync(CancellationToken.None);

		policies.ShouldNotBeNull();
	}

	[Fact]
	public void Throw_when_options_is_null()
	{
		Should.Throw<ArgumentNullException>(
			() => new RetentionEnforcementService(
				null!,
				NullLogger<RetentionEnforcementService>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(
			() => new RetentionEnforcementService(
				Microsoft.Extensions.Options.Options.Create(new RetentionEnforcementOptions()),
				null!));
	}

	[Fact]
	public void Accept_optional_erasure_service()
	{
		var erasureService = A.Fake<IErasureService>();

		var sut = new RetentionEnforcementService(
			Microsoft.Extensions.Options.Options.Create(new RetentionEnforcementOptions()),
			NullLogger<RetentionEnforcementService>.Instance,
			erasureService);

		sut.ShouldNotBeNull();
	}
}

public class RetentionEnforcementOptionsShould
{
	[Fact]
	public void Have_24_hour_default_scan_interval()
	{
		var options = new RetentionEnforcementOptions();

		options.ScanInterval.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void Have_dry_run_disabled_by_default()
	{
		var options = new RetentionEnforcementOptions();

		options.DryRun.ShouldBeFalse();
	}

	[Fact]
	public void Have_100_default_batch_size()
	{
		var options = new RetentionEnforcementOptions();

		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void Have_enabled_by_default()
	{
		var options = new RetentionEnforcementOptions();

		options.Enabled.ShouldBeTrue();
	}
}
