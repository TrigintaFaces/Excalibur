// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Event IDs for grant authorization log messages emitted on the ASP.NET Core request path.
/// </summary>
/// <remarks>
/// Event IDs are allocated within the <c>Excalibur.A3.AspNetCore</c> range (2700–2799).
/// </remarks>
internal static class GrantAuthorizationEventId
{
	/// <summary>
	/// A grant requirement was evaluated and satisfied.
	/// </summary>
	internal const int GrantAuthorized = 2700;

	/// <summary>
	/// A grant requirement was evaluated and the caller did not hold the grant.
	/// </summary>
	internal const int GrantDenied = 2701;

	/// <summary>
	/// The caller's identity or tenant could not be resolved, so no grant could be evaluated.
	/// </summary>
	internal const int IdentityUnresolved = 2702;

	/// <summary>
	/// A resource-scoped requirement could not read its resource identifier from the request.
	/// </summary>
	internal const int ResourceScopeUnresolved = 2703;
}
