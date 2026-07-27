// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// An ambient tenant scope fixed for the lifetime of a store, standing in for the per-request context a
/// host would supply.
/// </summary>
/// <remarks>
/// <para>
/// It takes its tenant in the constructor and has no null state. A sibling implementation elsewhere yields
/// a null tenant until an explicit switch is called, which fails closed on the first read if that call is
/// ever missed -- a present-but-unresolved ambient context throws rather than degrading. Requiring the
/// tenant up front makes that mistake inexpressible here rather than merely absent today.
/// </para>
/// <para>
/// Tests use this instead of setting <c>AuditQuery.TenantId</c>. The stores resolve their partition from
/// the ambient scope and never read that field; a test that supplied it and asserted the result narrowed
/// would only pass if a store trusted a caller-supplied tenant, which is the cross-tenant impersonation
/// defect rather than the contract.
/// </para>
/// </remarks>
internal sealed class FixedTenantContext(string tenantId) : ITenantContext
{
	public string TenantId { get; } = !string.IsNullOrWhiteSpace(tenantId)
		? tenantId
		: throw new ArgumentException("An ambient tenant is required.", nameof(tenantId));

	string? ITenantContext.TenantId => TenantId;

	public bool HasTenant => true;
}
