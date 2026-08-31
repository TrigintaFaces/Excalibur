// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Metapackages.Tests;

/// <summary>
/// Container-validation lock for the resilience wiring the AWS and Azure experience metapackages pull in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks.</b> Both bundles call <c>UseResilience()</c>, which registered a distributed
/// circuit breaker factory whose constructor requires <see cref="IDistributedCache"/>. Nothing on that path
/// registered a cache, so a consumer calling the one-line entry point built a container holding a service
/// that could not be constructed. <c>ValidateOnBuild</c> -- on by default in the Development environment --
/// turned that into a startup failure naming a type the consumer never asked for.
/// </para>
/// <para>
/// <b>Why the fix is a move rather than a default.</b> Seating an in-memory cache underneath a distributed
/// breaker would have silently made it per-instance: reads and writes hit the same process, a miss reads as
/// closed, and cache faults fall back to the last known state, so every replica would trip alone and the
/// shared-state guarantee in the type's name would vanish with nothing surfacing. The registration instead
/// moved to the opt-in path that already guarantees a cache, which keeps the breaker available exactly where
/// it can honour its name.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> These arms assert a negative over validation output, so they are only worth their
/// runtime if that output can contain the token. On unmodified sources it does: validation fails naming
/// <see cref="IDistributedCache"/>, and both arms are RED. The sibling arm below is the positive control --
/// it proves the same helper still reports the failures a bare <see cref="ServiceCollection"/> genuinely has,
/// so an empty report here would be a finding rather than a pass.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metapackages")]
public sealed class CloudTransportMetapackageResilienceShould : UnitTestBase
{
	private const string AwsRegion = "us-east-1";
	private const string AzureNamespace = "excalibur-lock.servicebus.windows.net";

	[Fact]
	public void Never_fail_container_validation_for_a_missing_distributed_cache_aws() =>
		ValidationReportFor(static services => services.AddDispatchAws(sqs => sqs.UseRegion(AwsRegion)))
			.ShouldNotContain(
				nameof(IDistributedCache),
				Case.Sensitive,
				"AddDispatchAws must not leave a service in the container that cannot be constructed -- "
				+ "a distributed circuit breaker belongs only on a path that guarantees a distributed cache");

	[Fact]
	public void Never_fail_container_validation_for_a_missing_distributed_cache_azure() =>
		ValidationReportFor(static services =>
				services.AddDispatchAzure(sb => sb.FullyQualifiedNamespace(AzureNamespace)))
			.ShouldNotContain(
				nameof(IDistributedCache),
				Case.Sensitive,
				"AddDispatchAzure must not leave a service in the container that cannot be constructed -- "
				+ "a distributed circuit breaker belongs only on a path that guarantees a distributed cache");

	/// <summary>
	/// Positive control for the two arms above: proves the helper still surfaces real validation failures.
	/// </summary>
	/// <remarks>
	/// A bare <see cref="ServiceCollection"/> supplies no host services, so the bundles' host-provided
	/// dependencies genuinely cannot be resolved and validation says so. That is expected -- a real host
	/// supplies them -- and it is what makes the negative assertions above meaningful: the report is
	/// non-empty, so <see cref="IDistributedCache"/> being absent from it is evidence rather than silence.
	/// </remarks>
	[Fact]
	public void Still_report_the_host_services_a_bare_collection_cannot_supply() =>
		ValidationReportFor(static services => services.AddDispatchAws(sqs => sqs.UseRegion(AwsRegion)))
			.ShouldNotBeEmpty(
				"if validation reported nothing at all, the sibling arms would be asserting a negative "
				+ "over an empty string and would pass without testing anything");

	/// <summary>
	/// Builds the composition with <c>ValidateOnBuild</c> and returns everything validation had to say.
	/// </summary>
	/// <param name="compose">The metapackage entry point under test.</param>
	/// <returns>The validation failure text, or the empty string when validation found nothing.</returns>
	private static string ValidationReportFor(Action<IServiceCollection> compose)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		compose(services);

		try
		{
			services.BuildServiceProvider(
				new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }).Dispose();
			return string.Empty;
		}
		catch (AggregateException validation)
		{
			return validation.ToString();
		}
	}
}
