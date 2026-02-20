// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides default values for tenant configuration.
/// </summary>
/// <remarks>
/// Single-tenant applications use <see cref="DefaultTenantId"/> automatically when no explicit
/// tenant identifier is configured. Multi-tenant applications should register a custom
/// <see cref="ITenantId"/> implementation that resolves the tenant from the current context.
/// </remarks>
public static class TenantDefaults
{
	/// <summary>
	/// The tenant identifier used when multi-tenancy is not configured.
	/// Single-tenant applications use this value automatically.
	/// </summary>
	public const string DefaultTenantId = "Default";

	/// <summary>
	/// A wildcard tenant identifier indicating all tenants.
	/// Used by infrastructure services (e.g., job hosts, background processors)
	/// that operate across all tenants rather than within a single tenant scope.
	/// </summary>
	public const string AllTenants = "*";
}
