// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for tenant identity processing.
/// </summary>
public sealed class TenantIdentityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether tenant identity processing is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate tenant access permissions.
	/// </summary>
	/// <value> Default is true. </value>
	public bool ValidateTenantAccess { get; set; } = true;

	/// <summary>
	/// Gets or sets the header name used for tenant ID propagation.
	/// </summary>
	/// <value> Default is "X-Tenant-ID". </value>
	public string? TenantIdHeader { get; set; } = "X-Tenant-ID";

	/// <summary>
	/// Gets or sets the header name used for tenant name propagation.
	/// </summary>
	/// <value> Default is "X-Tenant-Name". </value>
	public string? TenantNameHeader { get; set; } = "X-Tenant-Name";

	/// <summary>
	/// Gets or sets the header name used for tenant region propagation.
	/// </summary>
	/// <value> Default is "X-Tenant-Region". </value>
	public string? TenantRegionHeader { get; set; } = "X-Tenant-Region";

	/// <summary>
	/// Gets or sets the default tenant ID to use when none is specified.
	/// </summary>
	/// <value> Default is <see cref="TenantDefaults.DefaultTenantId"/>.
	/// Single-tenant applications use this value automatically without any tenant configuration. </value>
	public string? DefaultTenantId { get; set; } = TenantDefaults.DefaultTenantId;

	/// <summary>
	/// Gets or sets the minimum length for tenant identifiers.
	/// </summary>
	/// <value> Default is 1. </value>
	/// <remarks>
	/// Bounded above by <see cref="Excalibur.Dispatch.TenantId.MaxLength"/> for the same reason
	/// <see cref="MaxTenantIdLength"/> is: a minimum longer than any storable identifier would reject every
	/// tenant the framework can persist, so the configuration would be unsatisfiable rather than strict.
	/// </remarks>
	[Range(1, Excalibur.Dispatch.TenantId.MaxLength)]
	public int MinTenantIdLength { get; set; } = 1;

	/// <summary>
	/// Gets or sets the maximum length for tenant identifiers.
	/// </summary>
	/// <value> Default is <see cref="Excalibur.Dispatch.TenantId.MaxLength"/>. </value>
	/// <remarks>
	/// <para>
	/// This bound may be lowered to tighten what a deployment accepts, but it cannot be raised above
	/// <see cref="Excalibur.Dispatch.TenantId.MaxLength"/> — the narrowest tenant column any shipped
	/// provider declares. Accepting a longer identifier at the boundary would not make it storable; it
	/// would defer the failure to the store, where the caller that supplied the value is no longer in
	/// scope, and where the outcome on a provider that truncates rather than rejects is a tenant
	/// identifier that silently collides with another tenant's.
	/// </para>
	/// <para>
	/// The same ceiling is enforced without a configuration knob by the tenant identifier and scope types
	/// themselves, so a value above it would advertise an acceptance this framework does not have.
	/// </para>
	/// </remarks>
	[Range(1, Excalibur.Dispatch.TenantId.MaxLength)]
	public int MaxTenantIdLength { get; set; } = Excalibur.Dispatch.TenantId.MaxLength;

	/// <summary>
	/// Gets or sets a regex pattern for validating tenant ID format.
	/// </summary>
	/// <value> Default is null (no pattern validation). </value>
	public string? TenantIdPattern { get; set; }
}
