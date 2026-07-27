// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Fail-closed default <see cref="ITenantContext"/> for deployments that do not enable multi-tenancy.
/// </summary>
/// <remarks>
/// A safety control is a required dependency whose absence is expressed as an explicit value, never a
/// null. This context represents "one tenant, always present": <see cref="TenantId"/> is a fixed,
/// non-null sentinel so tenant-scoped queries emit their discriminator unconditionally and can never
/// silently widen to a cross-tenant read or write. It is registered as the default so
/// <c>GetRequiredService&lt;ITenantContext&gt;()</c> always resolves; the multi-tenancy composition
/// replaces it with the ambient, resolver-driven context.
/// </remarks>
internal sealed class SingleTenantContext : ITenantContext
{
	/// <inheritdoc />
	public string? TenantId => TenantDefaults.DefaultTenantId;

	/// <inheritdoc />
	public bool HasTenant => true;
}
