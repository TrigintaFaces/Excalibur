// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Configures how the tenancy pipeline behaves when no tenant resolves for a message or request.
/// </summary>
/// <remarks>
/// This is the explicit fail-fast-versus-default knob for ambient tenancy. When
/// <see cref="RequireTenant"/> is enabled and no tenant resolves on a tenant-required path, the
/// resolver pipeline fails fast (mirroring the storage-side
/// <c>Excalibur.Data.Sharding.TenantShardNotFoundException</c>) rather than proceeding with an
/// unscoped operation. <see cref="DefaultTenantId"/> is the opt-in fallback applied when a
/// tenant is not otherwise resolved.
/// </remarks>
public sealed class TenantContextOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether a resolved tenant is mandatory.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to fail fast when no tenant resolves on a tenant-required path;
	/// <see langword="false"/> (the default) to allow unscoped operation.
	/// </value>
	public bool RequireTenant { get; set; }

	/// <summary>
	/// Gets or sets the explicit default tenant identifier applied when no tenant is otherwise resolved.
	/// </summary>
	/// <value>The fallback tenant identifier, or <see langword="null"/> when no default is configured.</value>
	public string? DefaultTenantId { get; set; }
}
