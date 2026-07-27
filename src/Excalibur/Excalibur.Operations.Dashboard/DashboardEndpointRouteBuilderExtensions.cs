// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Operations.Dashboard;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Endpoint mapping extensions for the Excalibur operational dashboard.
/// </summary>
public static class DashboardEndpointRouteBuilderExtensions
{
	/// <summary>
	/// Maps the complete dashboard: the read API (and opt-in auth-gated mutating endpoints) plus the
	/// embedded single-page app, both under the configured route prefix. This is the one-call convenience
	/// most consumers use after <c>AddDashboard</c>.
	/// </summary>
	/// <param name="endpoints"> The endpoint route builder to map onto. </param>
	/// <returns> The same endpoint route builder, for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpoints"/> is null. </exception>
	public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder endpoints)
	{
		ArgumentNullException.ThrowIfNull(endpoints);

		var routePrefix = endpoints.ServiceProvider.GetRequiredService<IOptions<DashboardOptions>>().Value.RoutePrefix;

		endpoints.MapDashboardApi();
		_ = endpoints.MapDashboardSpa(routePrefix);

		return endpoints;
	}

	/// <summary>
	/// Maps the dashboard read API: a capability-discovery root endpoint plus every registered
	/// subsystem module's read endpoints, under the configured route prefix.
	/// </summary>
	/// <param name="endpoints"> The endpoint route builder to map onto. </param>
	/// <returns> The same endpoint route builder, for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpoints"/> is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the dashboard services are not registered (call <c>AddDashboard</c>). </exception>
	public static IEndpointRouteBuilder MapDashboardApi(this IEndpointRouteBuilder endpoints)
	{
		ArgumentNullException.ThrowIfNull(endpoints);

		var services = endpoints.ServiceProvider;
		var options = services.GetRequiredService<IOptions<DashboardOptions>>().Value;
		var modules = services.GetServices<IDashboardEndpointModule>().ToArray();

		var group = endpoints.MapGroup($"{options.RoutePrefix}/api");

		// Optional read-side authorization (symmetric with the mutating group; health-checks parity).
		// Default null = open read-only access (unchanged). When a policy is set, the convention propagates
		// to every read endpoint added to the group, so all reads require an authorized caller.
		if (!string.IsNullOrWhiteSpace(options.ReadActionsPolicy))
		{
			_ = group.RequireAuthorization(options.ReadActionsPolicy);
		}

		var subsystems = modules.Select(static m => m.Subsystem).OrderBy(static s => s, StringComparer.Ordinal).ToArray();

		// Capability-discovery root: lets the SPA render only panels backed by a configured subsystem.
		group.MapGet("/", (IOptions<DashboardOptions> opts) =>
		{
			var caps = new DashboardCapabilities
			{
				Subsystems = subsystems,
				MutatingActionsEnabled = opts.Value.EnableMutatingActions,
			};
			return Results.Json(caps, DashboardJsonSerializerContext.Default.DashboardCapabilities);
		});

		foreach (var module in modules)
		{
			module.MapEndpoints(group, options);
		}

		// Mutating (state-changing) endpoints are mapped ONLY when explicitly enabled. When disabled the
		// group is never created, so the endpoints do not exist (404 — zero attack surface), not merely
		// auth-gated. When enabled the group additionally requires an authenticated, authorized caller.
		if (options.EnableMutatingActions)
		{
			var mutating = endpoints.MapGroup($"{options.RoutePrefix}/api");
			_ = string.IsNullOrWhiteSpace(options.MutatingActionsPolicy)
				? mutating.RequireAuthorization()
				: mutating.RequireAuthorization(options.MutatingActionsPolicy);

			foreach (var module in services.GetServices<IDashboardMutatingModule>())
			{
				module.MapMutatingEndpoints(mutating, options);
			}
		}

		return endpoints;
	}
}
