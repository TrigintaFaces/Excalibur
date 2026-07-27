// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Versioning;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Wiring lock for the 6gfvtw seam: the advertised contract-versioning control on the Default/Strict/
/// InternalEvent pipeline profiles must be <b>actually wired</b> by the default dispatch registration,
/// resolvable with <b>zero consumer configuration</b>.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline builder null-skips any middleware type that is not registered in the container, so an
/// advertised middleware that nobody registers is <em>silently inert</em> — the control appears on the
/// profile but never runs. The fix registers both the <see cref="ContractVersionCheckMiddleware"/> and a
/// Null-Object <see cref="IContractVersionService"/> default via <c>TryAdd</c> (permissive until a consumer
/// supplies <c>SupportedVersions</c> or a richer service), so the Default profile resolves and runs the
/// version check without the consumer registering anything.
/// </para>
/// <para>
/// This lock is RED on the pre-fix registration (neither type registered → the descriptor is absent and
/// the middleware cannot resolve). Both registrations use <c>TryAdd</c>, so a consumer override still wins.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch")]
[Trait(TraitNames.Feature, TestFeatures.Configuration)]
public sealed class DefaultProfileWiresContractVersioningShould
{
	[Fact]
	public void Register_a_default_contract_version_service_with_no_consumer_config()
	{
		var services = new ServiceCollection();

		services.AddDefaultDispatchPipelines();

		var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IContractVersionService));
		descriptor.ShouldNotBeNull(
			"AddDefaultDispatchPipelines must register a default IContractVersionService so the advertised " +
			"version-check control on the Default profile is actually wired — the pipeline builder null-skips " +
			"an unregistered middleware, leaving the control silently inert.");

		var implementationType = descriptor.ImplementationType
			?? descriptor.ImplementationInstance?.GetType();
		implementationType.ShouldNotBeNull("the default IContractVersionService must be a concrete implementation type.");
		implementationType!.Name.ShouldBe(
			"DefaultContractVersionService",
			"the shipped default must be the permissive Null-Object DefaultContractVersionService (internal type).");
	}

	[Fact]
	public void Register_the_contract_version_check_middleware_with_no_consumer_config()
	{
		var services = new ServiceCollection();

		services.AddDefaultDispatchPipelines();

		services.Any(d => d.ServiceType == typeof(ContractVersionCheckMiddleware)).ShouldBeTrue(
			"AddDefaultDispatchPipelines must register ContractVersionCheckMiddleware so the Default profile's " +
			"advertised versioning control runs rather than being null-skipped as unregistered.");
	}

	[Fact]
	public void Resolve_the_version_check_middleware_end_to_end_without_a_consumer_version_service()
	{
		var services = new ServiceCollection();

		// Only framework infrastructure (logging + options) — NOT a consumer-provided version service.
		services.AddLogging();
		services.AddOptions();
		services.AddDefaultDispatchPipelines();

		using var provider = services.BuildServiceProvider(validateScopes: true);
		using var scope = provider.CreateScope();

		// The middleware resolves only if its IContractVersionService dependency is satisfied by the
		// framework default — i.e. the consumer supplied no version service. RED on the pre-fix impl,
		// which throws here because IContractVersionService is unregistered.
		var middleware = scope.ServiceProvider.GetRequiredService<ContractVersionCheckMiddleware>();
		middleware.ShouldNotBeNull();

		scope.ServiceProvider.GetRequiredService<IContractVersionService>()
			.GetType().Name.ShouldBe("DefaultContractVersionService");
	}

	[Fact]
	public void Allow_a_consumer_to_override_the_default_version_service()
	{
		var services = new ServiceCollection();
		var custom = A.Fake<IContractVersionService>();

		// Consumer registers first; the framework default is TryAdd, so the consumer wins.
		services.AddSingleton(custom);
		services.AddDefaultDispatchPipelines();

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IContractVersionService>().ShouldBeSameAs(custom);
	}
}
