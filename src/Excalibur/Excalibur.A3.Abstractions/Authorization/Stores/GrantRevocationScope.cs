// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>Explicit tenant or estate-wide scope for revoking a user's grants.</summary>
public sealed record GrantRevocationScope
{
	/// <summary>Initializes an explicit tenant or all-tenants scope.</summary>
	/// <param name="tenantId">Exact tenant identifier, or null for an explicitly all-tenants scope.</param>
	/// <param name="isAllTenants">Must be explicitly true when no tenant is supplied.</param>
	public GrantRevocationScope(string? tenantId, bool isAllTenants = false)
	{
		if ((tenantId is null) != isAllTenants)
		{
			throw new ArgumentException("Supply one tenant, or explicitly select all tenants.", nameof(tenantId));
		}
		if (tenantId is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
			if (!string.Equals(tenantId, tenantId.Trim(), StringComparison.Ordinal))
			{
				throw new ArgumentException("Identifiers must not contain leading or trailing whitespace.", nameof(tenantId));
			}
		}
		TenantId = tenantId;
		IsAllTenants = isAllTenants;
	}
	/// <summary>Gets whether all tenants were explicitly selected.</summary>
	public bool IsAllTenants { get; }
	/// <summary>Gets the tenant, or null for an explicitly estate-wide scope.</summary>
	public string? TenantId { get; }
	/// <summary>Gets the explicit scope covering every tenant.</summary>
	public static GrantRevocationScope AllTenants { get; } = new(null, isAllTenants: true);
	/// <summary>Creates a scope covering exactly one tenant.</summary>
	/// <param name="tenantId">The exact tenant identifier.</param>
	/// <returns>The tenant scope.</returns>
	public static GrantRevocationScope ForTenant(string tenantId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
		if (!string.Equals(tenantId, tenantId.Trim(), StringComparison.Ordinal))
		{
			throw new ArgumentException("Identifiers must not contain leading or trailing whitespace.", nameof(tenantId));
		}
		return new(tenantId);
	}
}
