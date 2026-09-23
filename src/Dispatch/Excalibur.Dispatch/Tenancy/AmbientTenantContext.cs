// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// Default <see cref="ITenantContext"/>: a read-only view over the ambient tenant established via
/// <see cref="TenantContextHolder.BeginScope"/>. Stateless and thread-safe (the state lives in the
/// async-flow-local holder), so it is safe to register as a singleton.
/// </summary>
internal sealed class AmbientTenantContext : ITenantContext
{
	/// <inheritdoc />
	public string? TenantId => TenantContextHolder.Current;

	/// <inheritdoc />
	/// <remarks>
	/// Whitespace is <em>not</em> a tenant. The predicate is the one the conversion uses, deliberately:
	/// a caller writing the documented pattern — test this, then convert — must not be told a value is
	/// safe that the conversion then refuses. Spelling this as a non-empty test instead admits a blank
	/// id here and throws on the very next line, which is the worst available shape because the throw
	/// lands in the branch this property vouched for.
	/// </remarks>
	public bool HasTenant => !string.IsNullOrWhiteSpace(TenantContextHolder.Current);
}
