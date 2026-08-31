// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Fixed <see cref="ITenantContext"/> test double for store constructors that require an ambient tenant.
/// </summary>
/// <remarks>
/// Implements <see cref="ITenantContext"/> DIRECTLY, inheriting no first-party base, so a store exercised
/// through this double binds the interface's own contract rather than an inherited convenience.
/// <see cref="SingleTenantDefault"/> reproduces what a single-tenant host receives from the framework
/// default context: the one canonical tenant identifier, always resolved.
/// </remarks>
/// <param name="tenantId">The tenant identifier this context resolves to.</param>
internal sealed class TestTenantContext(string? tenantId) : ITenantContext
{
	/// <summary>
	/// Gets a context resolving to the single canonical tenant a single-tenant host operates as.
	/// </summary>
	public static ITenantContext SingleTenantDefault { get; } = new TestTenantContext(TenantDefaults.DefaultTenantId);

	/// <inheritdoc />
	public string? TenantId { get; } = tenantId;

	/// <inheritdoc />
	public bool HasTenant => !string.IsNullOrEmpty(TenantId);
}
