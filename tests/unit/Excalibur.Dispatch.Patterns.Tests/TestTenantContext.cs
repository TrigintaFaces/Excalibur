// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Patterns.Tests;

/// <summary>
/// A minimal ambient <see cref="ITenantContext"/> pinned to one tenant identifier, for tests that need to
/// supply the required tenant context to a store constructor.
/// </summary>
/// <remarks>
/// Defaults to <see cref="TenantDefaults.DefaultTenantId"/> — the identity a single-tenant host actually
/// operates as — which is what a test that never distinguished tenants is asserting about.
/// </remarks>
/// <param name="tenantId">The tenant identifier this context resolves.</param>
internal sealed class TestTenantContext(string? tenantId) : ITenantContext
{
	/// <summary>Initializes a context pinned to the single canonical tenant of a single-tenant host.</summary>
	public TestTenantContext()
		: this(TenantDefaults.DefaultTenantId)
	{
	}

	/// <inheritdoc/>
	public string? TenantId { get; } = tenantId;

	/// <inheritdoc/>
	public bool HasTenant => !string.IsNullOrEmpty(TenantId);
}
