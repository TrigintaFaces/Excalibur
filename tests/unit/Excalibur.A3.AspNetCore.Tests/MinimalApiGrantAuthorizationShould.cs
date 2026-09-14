// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.A3.AspNetCore.Tests;

/// <summary>
/// Grant authorization on minimal API endpoints, exercised through a real host.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class MinimalApiGrantAuthorizationShould
{
	private const string User = "user-1";
	private const string Tenant = "tenant-1";

	private static void MapOrders(WebApplication app)
	{
		_ = app.MapGet("/orders/{id}", static (string id) => Results.Ok(id))
			.RequireAuthorization(GrantPolicyName.ForRouteValue("Read", "Order", "id"));

		_ = app.MapGet("/orders", static () => Results.Ok("all"))
			.RequireAuthorization(GrantPolicyName.For("Read", "Order"));

		// The strongly-typed attribute, applied the way a minimal API endpoint applies one.
		_ = app.MapGet("/attr/orders/{id}", static (string id) => Results.Ok(id))
			.RequireAuthorization(new RequireGrantAttribute("Read", "Order", "id"));
	}

	[Fact]
	public async Task AuthorizeCallerHoldingTheGrantForTheRequestedResource()
	{
		// LIVENESS. Proves the identity and tenant reached grant evaluation from the request principal,
		// and that the resource identifier was taken from the route.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-42");
		await using var host = await GrantEndpointTestHost.StartAsync(grants, MapOrders);

		using var response = await host.GetAsync("/orders/order-42", User, Tenant);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await response.Content.ReadAsStringAsync()).ShouldBe("\"order-42\"");
	}

	[Fact]
	public async Task DenyCallerWithoutTheGrant()
	{
		// SAFETY.
		var grants = new TestGrantStore();
		await using var host = await GrantEndpointTestHost.StartAsync(grants, MapOrders);

		using var response = await host.GetAsync("/orders/order-42", User, Tenant);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DenyCallerHoldingTheGrantForADifferentResource()
	{
		// SAFETY, and the arm a blanket-allow cannot pass: the caller genuinely holds Read on Order,
		// just not on this order. Only a handler that reads the route value distinguishes them.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-1");
		await using var host = await GrantEndpointTestHost.StartAsync(grants, MapOrders);

		using var granted = await host.GetAsync("/orders/order-1", User, Tenant);
		using var denied = await host.GetAsync("/orders/order-42", User, Tenant);

		granted.StatusCode.ShouldBe(HttpStatusCode.OK);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DenyCallerHoldingTheGrantInADifferentTenant()
	{
		// SAFETY. The tenant must come from the request principal, not from whatever was ambient.
		var grants = new TestGrantStore().Grant(User, "tenant-1", "Read", "order-42");
		await using var host = await GrantEndpointTestHost.StartAsync(grants, MapOrders);

		using var ownTenant = await host.GetAsync("/orders/order-42", User, "tenant-1");
		using var otherTenant = await host.GetAsync("/orders/order-42", User, "tenant-2");

		ownTenant.StatusCode.ShouldBe(HttpStatusCode.OK);
		otherTenant.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task ChallengeAnonymousCallerRatherThanForbidding()
	{
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-42");
		await using var host = await GrantEndpointTestHost.StartAsync(grants, MapOrders);

		using var response = await host.GetAsync("/orders/order-42", userId: null);

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task AuthorizeAnUnscopedGrantWithoutARouteValue()
	{
		// LIVENESS for the unscoped form: the resource identifier is null, and the grant is held for null.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", resourceId: null);
		await using var host = await GrantEndpointTestHost.StartAsync(grants, MapOrders);

		using var response = await host.GetAsync("/orders", User, Tenant);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task DenyWhenTheScopedRouteParameterIsAbsentFromTheEndpoint()
	{
		// SAFETY. A resource-scoped policy on an endpoint that declares no such route parameter must
		// never fall back to the broader unscoped check, which would be more permissive than asked for.
		var grants = new TestGrantStore()
			.Grant(User, Tenant, "Read", resourceId: null)
			.Grant(User, Tenant, "Read", "order-42");

		await using var host = await GrantEndpointTestHost.StartAsync(
			grants,
			static app => app.MapGet("/reports", static () => Results.Ok("reports"))
				.RequireAuthorization(GrantPolicyName.ForRouteValue("Read", "Order", "id")));

		using var response = await host.GetAsync("/reports", User, Tenant);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DenyWhenThePrincipalCarriesNoTenantClaimAndNoDefaultIsConfigured()
	{
		// SAFETY. An unresolvable tenant is a denial, never an evaluation against no tenant at all.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-42");
		await using var host = await GrantEndpointTestHost.StartAsync(grants, MapOrders);

		using var response = await host.GetAsync("/orders/order-42", User, tenantId: null);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task ResolveTheConfiguredDefaultTenantWhenThePrincipalCarriesNoTenantClaim()
	{
		// LIVENESS for the single-tenant host escape hatch.
		var grants = new TestGrantStore().Grant(User, "the-only-tenant", "Read", "order-42");

		await using var host = await GrantEndpointTestHost.StartAsync(
			grants,
			MapOrders,
			configureOptions: static options => options.DefaultTenantId = "the-only-tenant");

		using var response = await host.GetAsync("/orders/order-42", User, tenantId: null);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task ApplyTheStronglyTypedAttributeOnAMinimalApiEndpoint()
	{
		// LIVENESS + SAFETY for the attribute form on this hosting style: the caller holds Read on
		// order-1 only, so the attribute must distinguish the two orders exactly as the name form does.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-1");
		await using var host = await GrantEndpointTestHost.StartAsync(grants, MapOrders);

		using var granted = await host.GetAsync("/attr/orders/order-1", User, Tenant);
		using var denied = await host.GetAsync("/attr/orders/order-42", User, Tenant);

		granted.StatusCode.ShouldBe(HttpStatusCode.OK);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task StillApplyTheDefaultPolicyToAnEndpointThatNamesNoPolicy()
	{
		// The custom provider must keep deferring GetDefaultPolicyAsync to the framework, or a bare
		// RequireAuthorization() would stop requiring authentication.
		var grants = new TestGrantStore();

		await using var host = await GrantEndpointTestHost.StartAsync(
			grants,
			static app => app.MapGet("/bare", static () => Results.Ok("bare")).RequireAuthorization());

		using var authenticated = await host.GetAsync("/bare", User, Tenant);
		using var anonymous = await host.GetAsync("/bare", userId: null);

		authenticated.StatusCode.ShouldBe(HttpStatusCode.OK);
		anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task LeaveAPolicyNameThatIsNotAGrantPolicyToTheRestOfTheApplication()
	{
		// The provider must delegate: a conventionally-registered policy still resolves.
		var grants = new TestGrantStore();

		await using var host = await GrantEndpointTestHost.StartAsync(
			grants,
			static app => app.MapGet("/ping", static () => Results.Ok("pong"))
				.RequireAuthorization("AnyAuthenticated"),
			configureServices: static services => services.AddAuthorizationBuilder()
				.AddPolicy("AnyAuthenticated", static policy => policy.RequireAuthenticatedUser()));

		using var authenticated = await host.GetAsync("/ping", User, Tenant);
		using var anonymous = await host.GetAsync("/ping", userId: null);

		authenticated.StatusCode.ShouldBe(HttpStatusCode.OK);
		anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}
}
