// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.A3.AspNetCore.Tests;

/// <summary>
/// Locks which tenant context a host gets when several packages contribute one: the answer depends on which
/// were registered, never on the order they were registered in.
/// </summary>
/// <remarks>
/// A web host that also runs background work normally registers both the ambient context and the HTTP
/// bridge. Each used to replace the other's registration, so whichever call ran last won, and in one of the
/// two orders the request's tenant claim was never read.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class TenantContextResolutionShould
{
	/// <summary>The HTTP context wins when it is registered after the ambient one.</summary>
	[Fact]
	public void ResolveTheHttpContext_WhenItIsRegisteredAfterTheAmbientOne()
	{
		var services = Services();
		_ = services.AddTenantContext();
		_ = services.AddHttpGrantAuthorization();

		using var provider = Build(services);

		provider.GetRequiredService<ITenantContext>().ShouldBeOfType<HttpContextTenantContext>();
	}

	/// <summary>The HTTP context also wins when it is registered first, which is the order that used to lose.</summary>
	[Fact]
	public void ResolveTheHttpContext_WhenItIsRegisteredBeforeTheAmbientOne()
	{
		var services = Services();
		_ = services.AddHttpGrantAuthorization();
		_ = services.AddTenantContext();

		using var provider = Build(services);

		provider.GetRequiredService<ITenantContext>().ShouldBeOfType<HttpContextTenantContext>();
	}

	/// <summary>With only the ambient context registered, the ambient context resolves.</summary>
	[Fact]
	public void ResolveTheAmbientContext_WhenOnlyItIsRegistered()
	{
		var services = Services();
		_ = services.AddTenantContext();

		using var provider = Build(services);

		provider.GetRequiredService<ITenantContext>().GetType().Name.ShouldBe("AmbientTenantContext");
	}

	/// <summary>With neither registered, the single-tenant default resolves.</summary>
	[Fact]
	public void ResolveTheSingleTenantDefault_WhenNeitherIsRegistered()
	{
		var services = Services();
		_ = services.AddDefaultTenantContext();

		using var provider = Build(services);

		provider.GetRequiredService<ITenantContext>().GetType().Name.ShouldBe("SingleTenantContext");
	}

	/// <summary>
	/// A singleton that depends on the tenant context can be built with scope validation on.
	/// </summary>
	/// <remarks>
	/// The HTTP context used to be scoped, so any singleton that took the tenant context -- a tenant-aware
	/// store, for example -- was refused by the container in Development and silently captured the first
	/// request's scope in Production. The context holds no per-request state, so it is shared instead.
	/// </remarks>
	[Fact]
	public void LetASingletonDependOnTheHttpTenantContext_UnderScopeValidation()
	{
		var services = Services();
		_ = services.AddHttpGrantAuthorization();
		_ = services.AddSingleton<TenantConsumer>();

		using var provider = Build(services);

		Should.NotThrow(() => provider.GetRequiredService<TenantConsumer>());
	}

	/// <summary>
	/// Registering the HTTP context also serves work that has no request: it reads the ambient tenant.
	/// </summary>
	/// <remarks>
	/// This is what makes it safe for the HTTP context to outrank the ambient one. Without it, a host that
	/// registered both would lose the ambient tenant in every background job.
	/// </remarks>
	[Fact]
	public void ReadTheAmbientTenant_WhenThereIsNoRequest()
	{
		var services = Services();
		_ = services.AddTenantContext();
		_ = services.AddHttpGrantAuthorization();

		using var provider = Build(services);
		var context = provider.GetRequiredService<ITenantContext>();

		using (TenantContextHolder.BeginScope("t1"))
		{
			context.TenantId.ShouldBe("t1");
		}
	}

	// Scope validation on and eager validation off: these arms resolve the tenant context alone, so the
	// grant services the bridge also registers need no backing store here.
	private static ServiceProvider Build(ServiceCollection services) =>
		services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });

	private static ServiceCollection Services()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddRouting();

		return services;
	}

	private sealed class TenantConsumer(ITenantContext tenantContext)
	{
		public ITenantContext TenantContext { get; } = tenantContext;
	}
}
