// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
}
