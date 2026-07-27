// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Resolves an options instance for the current ambient tenant. Per-tenant configuration is registered as
/// named options keyed by tenant id (<c>services.Configure&lt;TOptions&gt;(tenantId, …)</c>); this resolves
/// the named instance for the tenant established by <see cref="TenantContextHolder"/>, falling back to the
/// default (unnamed) options when no tenant is ambient or the tenant has no specific configuration.
/// </summary>
/// <typeparam name="TOptions">The options type.</typeparam>
public interface ITenantOptions<out TOptions>
	where TOptions : class
{
	/// <summary>Gets the options configured for the current ambient tenant.</summary>
	/// <value>The tenant's options instance (never <see langword="null"/>).</value>
	TOptions Value { get; }
}
