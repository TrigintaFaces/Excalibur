// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>
/// Maps the <strong>mutating</strong> (state-changing) endpoints for a dashboard subsystem, such as
/// dead-letter replay.
/// </summary>
/// <remarks>
/// Mutating modules are mapped <strong>only</strong> when <see cref="DashboardOptions.EnableMutatingActions"/>
/// is <see langword="true"/>. When it is <see langword="false"/> the mutating group is not mapped at all,
/// so these endpoints do not exist (a request yields <c>404</c>, zero attack surface). When it is
/// <see langword="true"/> the group additionally requires an authenticated, authorized caller.
/// </remarks>
public interface IDashboardMutatingModule
{
	/// <summary>
	/// Gets the stable subsystem key this module's mutating endpoints belong to (for example <c>dlq</c>).
	/// </summary>
	/// <value> A lowercase, URL-safe subsystem identifier. </value>
	string Subsystem { get; }

	/// <summary>
	/// Maps this subsystem's mutating endpoints onto the supplied, already auth-gated mutating route group.
	/// </summary>
	/// <param name="group"> The auth-gated mutating route group to map endpoints onto. </param>
	/// <param name="options"> The resolved dashboard options. </param>
	void MapMutatingEndpoints(RouteGroupBuilder group, DashboardOptions options);
}
