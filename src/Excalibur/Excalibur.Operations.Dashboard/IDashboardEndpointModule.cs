// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>
/// Maps the read endpoints for a single dashboard subsystem (outbox, dead-letter, inbox, saga,
/// projection-lag, leader) onto the shared dashboard route group.
/// </summary>
/// <remarks>
/// <para>
/// Each subsystem contributes one module, registered in DI via <c>TryAddEnumerable</c> so
/// <c>MapDashboardApi</c> discovers and maps every registered module automatically — there is no
/// central endpoint table to edit when a subsystem is added. Implement this interface and register it
/// with <c>TryAddEnumerable</c> to expose an additional read-only subsystem panel.
/// </para>
/// <para>
/// Modules MUST fail open: when the subsystem they surface is not configured, they map endpoints that
/// return an empty/not-configured payload rather than throwing, so an absent subsystem never yields a
/// 500.
/// </para>
/// </remarks>
public interface IDashboardEndpointModule
{
	/// <summary>
	/// Gets the stable subsystem key this module surfaces (for example <c>outbox</c>, <c>dlq</c>,
	/// <c>saga</c>), used by capability discovery and as the endpoint sub-route.
	/// </summary>
	/// <value> A lowercase, URL-safe subsystem identifier. </value>
	string Subsystem { get; }

	/// <summary>
	/// Maps this subsystem's read endpoints onto the supplied dashboard route group.
	/// </summary>
	/// <param name="group"> The dashboard API route group to map endpoints onto. </param>
	/// <param name="options"> The resolved dashboard options (page-size bounds, route prefix). </param>
	void MapEndpoints(RouteGroupBuilder group, DashboardOptions options);
}
