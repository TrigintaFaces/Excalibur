// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// Operational Dashboard Sample
// ============================
// Demonstrates wiring the free, OSS Excalibur operational dashboard into any
// ASP.NET Core host with two calls:
//
//   builder.Services.AddDashboard();   // register validated options + read-only subsystem modules
//   app.MapDashboard();                // map the read API + embedded single-page app
//
// The dashboard surfaces live outbox, dead-letter, inbox, saga, projection/CDC-lag,
// and leader-election state across every configured storage provider. Each subsystem
// module resolves its store at request time via an optional (nullable) dependency, so
// an absent subsystem FAILS OPEN (its panel is simply not advertised) rather than
// throwing — this sample configures no storage provider, so the dashboard renders with
// its capability-discovery root reporting zero subsystems.
//
// Endpoints (default RoutePrefix "/dashboard"):
// - GET /dashboard          - embedded single-page app (SPA)
// - GET /dashboard/api      - capability discovery (which subsystem panels are backed)
// - GET /dashboard/api/...  - per-subsystem read endpoints (when a provider is configured)

var builder = WebApplication.CreateBuilder(args);

// Authorization — TWO viable options; both feed the dashboard's ReadActionsPolicy/MutatingActionsPolicy
// (see README "Authorization — two viable options"). Pick whichever matches how your app authorizes.
//
// OPTION A — standard ASP.NET Core policies (roles / claims / custom). No Excalibur types:
//   builder.Services.AddAuthorizationBuilder()
//       .AddPolicy("DashboardRead",  p => p.RequireRole("ops", "sre"))
//       .AddPolicy("DashboardAdmin", p => p.RequireRole("ops-admin"));
//
// OPTION B — Excalibur A3 grant-based authorization (activity + resource scoped). The turnkey
// AddGrantAuthorization helper registers a named policy backed by an A3 grant, so you never
// hand-build a GrantsAuthorizationRequirement. (Shown active below.)
// 	 builder.Services.AddGrantAuthorization(
// 		policyName: "DashboardRead",
// 		activityName: "dashboard.read",
// 		resourceTypes: ["Dashboard"]);
// builder.Services.AddGrantAuthorization("DashboardAdmin", "dashboard.replay", ["DeadLetter"]);

// Register the operational dashboard. The parameterless overload uses defaults
// (read-only, route prefix "/dashboard", mutating actions OFF => zero attack surface).
builder.Services.AddDashboard(options =>
{
	// All options are optional; shown here to document the surface.
	options.RoutePrefix = "/dashboard";

	// Read endpoints are open by default. To gate them on the A3 grant registered above, set:
	//   options.ReadActionsPolicy = "DashboardRead";
	// (left unset here so the sample stays runnable without an authentication scheme). The policy
	// name is the standard ASP.NET Core authorization seam — see the "Grant-based authorization
	// (Excalibur A3)" section in README.md for the full recipe including mutating actions.

	// Mutating actions (e.g. dead-letter replay) are OFF by default. When enabled they
	// are auth-gated; the endpoints do not exist at all while disabled (404, not 401):
	//   options.EnableMutatingActions = true;
	//   options.MutatingActionsPolicy = "DashboardAdmin";
});

var app = builder.Build();

// Map the complete dashboard (read API + opt-in mutating endpoints + embedded SPA)
// under the configured route prefix. This is the one-call convenience most consumers use.
app.MapDashboard();

// Landing redirect so `/` opens the dashboard SPA.
app.MapGet("/", () => Results.Redirect("/dashboard"));

Console.WriteLine("Excalibur Operational Dashboard Sample");
Console.WriteLine("======================================");
Console.WriteLine();
Console.WriteLine("Open the dashboard:");
Console.WriteLine("  GET /dashboard          - single-page app");
Console.WriteLine("  GET /dashboard/api      - capability discovery (backed subsystems)");
Console.WriteLine();
Console.WriteLine("No storage provider is configured in this sample, so the dashboard");
Console.WriteLine("renders with zero subsystems advertised (fail-open). Register a provider");
Console.WriteLine("(outbox / dead-letter / inbox / saga / leader-election) to light up panels.");

app.Run();
