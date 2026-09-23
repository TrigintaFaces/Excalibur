// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// Grant authorization on ordinary ASP.NET Core endpoints.
//
// Two endpoints do the same job in the two hosting styles, both gated on an Excalibur A3 grant and both
// narrowed to the single order named by the route. Nothing here goes through Dispatch.
//
// Run it with `dotnet run`. It drives itself: on startup it issues the requests below and prints the
// status code each one produced, so the behaviour is visible without a separate client.

using System.Security.Claims;
using System.Text.Encodings.Web;

using Excalibur.A3.AspNetCore;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.Dispatch;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

// Excalibur.A3.Authorization and Microsoft.AspNetCore.Authorization both declare a type of this name.
// They are different contracts: A3's supplies the caller's GRANTS; Microsoft's supplies ASP.NET Core
// POLICIES. Alias the A3 one when both namespaces are in scope.
using GrantAuthorizedApiSample;

using A3PolicyProvider = Excalibur.A3.Authorization.IAuthorizationPolicyProvider;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Warning);

// 1. Authentication. Any scheme works — this sample uses request headers so it runs with no identity
//    provider. Replace it with JWT bearer, cookies or OpenID Connect; grant authorization reads whatever
//    ClaimsPrincipal your scheme produces and never authenticates anything itself.
builder.Services
	.AddAuthentication(DemoAuthenticationHandler.SchemeName)
	.AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(
		DemoAuthenticationHandler.SchemeName,
		_ => { });

// 2. Where grants come from. A real host registers the A3 grant services (AddExcaliburA3) and its grant
//    store; this sample supplies a small in-memory set through the same public seam so it runs with no
//    database. The user and tenant it reads are NOT substituted — those come from the request principal
//    via the bridge registered below, which is the part this sample is demonstrating.
builder.Services.AddSingleton(new DemoGrants(
	// alice may read every order in tenant-a; bob may read only order-1.
	["alice|tenant-a|Read|order-1", "alice|tenant-a|Read|order-2", "bob|tenant-a|Read|order-1"]));
builder.Services.AddScoped<A3PolicyProvider, DemoGrantPolicyProvider>();

// 3. The one call that makes grants usable on an endpoint. It bridges the authenticated principal into
//    grant evaluation and enables the grant policy-name convention.
builder.Services.AddHttpGrantAuthorization(options =>
{
	// This demo's principals carry the tenant in a "tenant_id" claim, which is already a default.
	// Add your provider's claim type here if it differs.
	options.TenantIdClaimTypes.Insert(0, "tenant_id");
});

builder.Services.AddControllers();

// Unhandled exceptions become RFC 9457 Problem Details responses, with the status code taken from
// the exception (404 for ResourceNotFoundException, 409 for ConcurrencyException). Details of a
// 5xx response are hidden outside Development.
builder.Services.AddGlobalExceptionHandler();

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// Minimal API. The attribute is strongly typed, so a typo is a compile error rather than a runtime
// failure. Where a policy NAME is required instead — a seam that only accepts a string — build it with
// GrantPolicyName.ForRouteValue("Read", "Order", "id") and pass that.
app.MapGet("/orders/{id}", (string id) => Results.Ok($"minimal-api order {id}"))
   .RequireAuthorization(new RequireGrantAttribute("Read", "Order", "id"));

app.MapControllers();

await app.StartAsync();
await DemoRunner.RunAsync(app);
await app.StopAsync();
