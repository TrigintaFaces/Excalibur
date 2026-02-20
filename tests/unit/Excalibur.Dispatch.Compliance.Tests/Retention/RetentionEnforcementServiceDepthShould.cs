using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Retention;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RetentionEnforcementServiceDepthShould
{
	private readonly RetentionEnforcementOptions _options = new();
	private readonly NullLogger<RetentionEnforcementService> _logger = NullLogger<RetentionEnforcementService>.Instance;

	[Fact]
	public async Task Enforce_retention_returns_result_with_dry_run_false()
	{
		_options.DryRun = false;
		var sut = CreateService();

		var result = await sut.EnforceRetentionAsync(CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotBeNull();
		result.IsDryRun.ShouldBeFalse();
		result.CompletedAt.ShouldNotBe(default);
		result.RecordsCleaned.ShouldBe(0);
		result.PoliciesEvaluated.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public async Task Enforce_retention_returns_result_with_dry_run_true()
	{
		_options.DryRun = true;
		var sut = CreateService();

		var result = await sut.EnforceRetentionAsync(CancellationToken.None).ConfigureAwait(false);

		result.IsDryRun.ShouldBeTrue();
	}

	[Fact]
	public async Task Get_retention_policies_discovers_annotated_properties()
	{
		var sut = CreateService();

		var policies = await sut.GetRetentionPoliciesAsync(CancellationToken.None).ConfigureAwait(false);

		// May find annotated types in loaded assemblies depending on what is loaded
		policies.ShouldNotBeNull();
	}

	[Fact]
	public async Task Enforce_retention_without_erasure_service()
	{
		var sut = new RetentionEnforcementService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

		var result = await sut.EnforceRetentionAsync(CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task Enforce_retention_with_erasure_service()
	{
		var erasureService = A.Fake<IErasureService>();
		var sut = new RetentionEnforcementService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger,
			erasureService);

		var result = await sut.EnforceRetentionAsync(CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotBeNull();
	}

	[Fact]
	public void Throw_for_null_options_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new RetentionEnforcementService(null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new RetentionEnforcementService(
				Microsoft.Extensions.Options.Options.Create(_options), null!));
	}

	[Fact]
	public async Task Enforce_retention_policies_evaluated_matches_get_policies()
	{
		var sut = CreateService();

		var policies = await sut.GetRetentionPoliciesAsync(CancellationToken.None).ConfigureAwait(false);
		var result = await sut.EnforceRetentionAsync(CancellationToken.None).ConfigureAwait(false);

		result.PoliciesEvaluated.ShouldBe(policies.Count);
	}

	private RetentionEnforcementService CreateService() =>
		new(Microsoft.Extensions.Options.Options.Create(_options), _logger);
}
