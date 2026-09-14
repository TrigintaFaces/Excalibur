// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authentication;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using A3PolicyProvider = Excalibur.A3.Authorization.IAuthorizationPolicyProvider;

namespace Excalibur.A3.AspNetCore.Tests;

/// <summary>
/// The composed container, built the way ASP.NET Core builds it in Development — with scope validation
/// on, so a captive dependency is a failure rather than a silent cross-request identity leak.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class GrantAuthorizationRegistrationShould
{
	private static ServiceCollection GrantEvaluationServices()
	{
		var services = HostServices();

		// Scoped, exactly as AddA3AuthorizationCore registers it: it reads the current caller.
		_ = services.AddScoped(static _ => A.Fake<A3PolicyProvider>());

		return services;
	}

	/// <summary>
	/// The services any ASP.NET Core host already has. Routing is included because the authorization
	/// policy cache resolves the endpoint data source, which only exists in a routed host.
	/// </summary>
	private static ServiceCollection HostServices()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddRouting();

		return services;
	}

	/// <summary>
	/// Builds the container the way ASP.NET Core does in Development: scope validation on, so a captive
	/// dependency is a hard failure.
	/// </summary>
	private static ServiceProvider Build(ServiceCollection services) =>
		services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = true,
			ValidateOnBuild = true,
		});

	/// <summary>
	/// Builds the container the way ASP.NET Core does outside Development: no eager validation, so a
	/// missing registration is not caught by the container and the startup check is what must catch it.
	/// This is the shape in which the misleading-403 failure actually reaches production.
	/// </summary>
	private static ServiceProvider BuildUnvalidated(ServiceCollection services) =>
		services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false });

	[Fact]
	public void BuildAValidContainerForTheNamedPolicyHelper()
	{
		// A singleton handler over the scoped grant policy provider cannot be constructed: the container
		// refuses it in Development, and in Production it would capture the first caller's identity for
		// the lifetime of the process.
		var services = GrantEvaluationServices();
		_ = services.AddGrantAuthorization("orders", "Read", ["Order"]);

		using var provider = Build(services);

		provider.ShouldNotBeNull();
	}

	[Fact]
	public void BuildAValidContainerForTheHttpBridge()
	{
		var services = GrantEvaluationServices();
		_ = services.AddHttpGrantAuthorization();

		using var provider = Build(services);

		provider.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterTheGrantAwarePolicyProviderEvenAfterTenantContextReplacesTheAmbientOne()
	{
		// AddTenantContext registers ITenantContext with Replace. Registering the bridge afterwards must
		// win, or the tenant claim on the request principal would never be read.
		var services = GrantEvaluationServices();
		_ = services.AddTenantContext();
		_ = services.AddHttpGrantAuthorization();

		using var provider = Build(services);
		using var scope = provider.CreateScope();

		scope.ServiceProvider.GetRequiredService<ITenantContext>()
			.ShouldBeOfType<HttpContextTenantContext>();
		scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>()
			.ShouldBeOfType<GrantAuthorizationPolicyProvider>();
		scope.ServiceProvider.GetRequiredService<IAuthenticationToken>()
			.ShouldBeOfType<HttpContextAuthenticationToken>();
	}

	[Fact]
	public async Task PassTheStartupCheckWhenTheBridgeIsComplete()
	{
		// LIVENESS for the guard: a correctly composed host must start.
		var services = GrantEvaluationServices();
		_ = services.AddHttpGrantAuthorization();

		using var provider = Build(services);
		var validator = provider.GetServices<IHostedService>()
			.OfType<GrantAuthorizationBridgeStartupValidator>()
			.Single();

		await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task FailTheStartupCheckWithAnActionableMessageWhenTheGrantEvaluatorIsMissing()
	{
		// SAFETY for the guard. Without it this composition denies every request with an indistinguishable
		// 403 instead of failing at start.
		var services = HostServices();
		_ = services.AddHttpGrantAuthorization();

		using var provider = BuildUnvalidated(services);
		var validator = provider.GetServices<IHostedService>()
			.OfType<GrantAuthorizationBridgeStartupValidator>()
			.Single();

		var error = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(CancellationToken.None));

		error.Message.ShouldContain("AddExcaliburA3");
	}

	[Fact]
	public async Task FailTheStartupCheckWhenAnotherRegistrationDisplacedTheGrantAwarePolicyProvider()
	{
		// SAFETY. A later Replace would leave grant policy names unresolvable at runtime.
		var services = GrantEvaluationServices();
		_ = services.AddHttpGrantAuthorization();
		services.Replace(
			ServiceDescriptor.Transient<IAuthorizationPolicyProvider, DefaultAuthorizationPolicyProvider>());

		using var provider = Build(services);
		var validator = provider.GetServices<IHostedService>()
			.OfType<GrantAuthorizationBridgeStartupValidator>()
			.Single();

		var error = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(CancellationToken.None));

		error.Message.ShouldContain("grant:Approve:Order");
	}
}
