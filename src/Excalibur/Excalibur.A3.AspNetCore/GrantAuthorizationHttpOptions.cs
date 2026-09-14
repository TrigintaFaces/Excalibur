// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Configures how the authenticated ASP.NET Core request principal is mapped onto the identity and
/// tenant that grant authorization evaluates.
/// </summary>
/// <remarks>
/// Identity providers disagree about which claim carries the subject and which carries the tenant, so
/// both are ordered candidate lists rather than a single fixed claim type. The first candidate present
/// on the principal with a non-empty value wins; when none match, the caller has no resolved user (or
/// tenant) and authorization fails closed.
/// </remarks>
public sealed class GrantAuthorizationHttpOptions
{
	/// <summary>
	/// Gets the claim types consulted, in order, to resolve the caller's user identifier.
	/// </summary>
	/// <value>
	/// An ordered, mutable list of claim types. Defaults to <see cref="ClaimTypes.NameIdentifier"/>,
	/// <c>sub</c>, <see cref="ClaimTypes.Upn"/> and <c>UPN</c>, which covers OpenID Connect, JWT-bearer
	/// and WS-Federation shaped principals.
	/// </value>
	public IList<string> UserIdClaimTypes { get; } =
	[
		ClaimTypes.NameIdentifier,
		"sub",
		ClaimTypes.Upn,
		"UPN",
	];

	/// <summary>
	/// Gets the claim types consulted, in order, to resolve the caller's tenant identifier.
	/// </summary>
	/// <value>
	/// An ordered, mutable list of claim types. Defaults to <c>tenant_id</c>, <c>tid</c> and the
	/// Microsoft identity platform tenant claim.
	/// </value>
	public IList<string> TenantIdClaimTypes { get; } =
	[
		"tenant_id",
		"tid",
		"http://schemas.microsoft.com/identity/claims/tenantid",
	];

	/// <summary>
	/// Gets or sets the tenant identifier used when the principal carries no tenant claim and no ambient
	/// tenant has been established for the request.
	/// </summary>
	/// <value>
	/// The fallback tenant identifier, or <see langword="null"/> (the default) to resolve no tenant, in
	/// which case authorization fails closed. Set this in a single-tenant host whose identity provider
	/// issues no tenant claim.
	/// </value>
	public string? DefaultTenantId { get; set; }
}
