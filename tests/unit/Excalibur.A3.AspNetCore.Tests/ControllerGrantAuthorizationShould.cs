// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

namespace Excalibur.A3.AspNetCore.Tests;

/// <summary>
/// The same grant authorization on MVC controller actions. Controllers are a separate hosting style with
/// a separate metadata and filter path, so every property is asserted again here rather than assumed to
/// transfer from the minimal API suite.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class ControllerGrantAuthorizationShould
{
	private const string User = "user-1";
	private const string Tenant = "tenant-1";

	private static Task<GrantEndpointTestHost> StartAsync(TestGrantStore grants) =>
		GrantEndpointTestHost.StartAsync(grants, static _ => { }, addControllers: true);

	[Fact]
	public async Task AuthorizeCallerHoldingTheGrantForTheRequestedResource()
	{
		// LIVENESS.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-42");
		await using var host = await StartAsync(grants);

		using var response = await host.GetAsync("/mvc/orders/order-42", User, Tenant);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await response.Content.ReadAsStringAsync()).ShouldBe("order-42");
	}

	[Fact]
	public async Task DenyCallerWithoutTheGrant()
	{
		// SAFETY.
		var grants = new TestGrantStore();
		await using var host = await StartAsync(grants);

		using var response = await host.GetAsync("/mvc/orders/order-42", User, Tenant);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DenyCallerHoldingTheGrantForADifferentResource()
	{
		// SAFETY, and the arm a blanket-allow cannot pass.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-1");
		await using var host = await StartAsync(grants);

		using var granted = await host.GetAsync("/mvc/orders/order-1", User, Tenant);
		using var denied = await host.GetAsync("/mvc/orders/order-42", User, Tenant);

		granted.StatusCode.ShouldBe(HttpStatusCode.OK);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DenyCallerHoldingTheGrantInADifferentTenant()
	{
		// SAFETY.
		var grants = new TestGrantStore().Grant(User, "tenant-1", "Read", "order-42");
		await using var host = await StartAsync(grants);

		using var ownTenant = await host.GetAsync("/mvc/orders/order-42", User, "tenant-1");
		using var otherTenant = await host.GetAsync("/mvc/orders/order-42", User, "tenant-2");

		ownTenant.StatusCode.ShouldBe(HttpStatusCode.OK);
		otherTenant.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task ChallengeAnonymousCallerRatherThanForbidding()
	{
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-42");
		await using var host = await StartAsync(grants);

		using var response = await host.GetAsync("/mvc/orders/order-42", userId: null);

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task AuthorizeAnUnscopedGrantWithoutARouteValue()
	{
		// LIVENESS for the unscoped form.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", resourceId: null);
		await using var host = await StartAsync(grants);

		using var response = await host.GetAsync("/mvc/orders", User, Tenant);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task DenyWhenTheScopedRouteParameterIsAbsentFromTheAction()
	{
		// SAFETY. Never falls back to the broader unscoped grant.
		var grants = new TestGrantStore()
			.Grant(User, Tenant, "Read", resourceId: null)
			.Grant(User, Tenant, "Read", "order-42");

		await using var host = await StartAsync(grants);

		using var response = await host.GetAsync("/mvc/reports", User, Tenant);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task ApplyTheStronglyTypedAttributeOnAControllerAction()
	{
		// The MVC action under test carries [RequireGrant("Read", "Order", "id")] rather than a literal
		// policy name, so these arms bind the attribute form on this hosting style specifically.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-1");
		await using var host = await StartAsync(grants);

		using var granted = await host.GetAsync("/mvc/orders/order-1", User, Tenant);
		using var denied = await host.GetAsync("/mvc/orders/order-42", User, Tenant);

		granted.StatusCode.ShouldBe(HttpStatusCode.OK);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task StillHonourALiteralGrantPolicyName()
	{
		// /mvc/reports carries the literal [Authorize(Policy = "grant:Read:Order:{id}")] form, which must
		// keep working for consumers who compose the name themselves.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", resourceId: null);
		await using var host = await StartAsync(grants);

		using var response = await host.GetAsync("/mvc/reports", User, Tenant);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DenyWhenThePrincipalCarriesNoTenantClaimAndNoDefaultIsConfigured()
	{
		// SAFETY.
		var grants = new TestGrantStore().Grant(User, Tenant, "Read", "order-42");
		await using var host = await StartAsync(grants);

		using var response = await host.GetAsync("/mvc/orders/order-42", User, tenantId: null);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
}
