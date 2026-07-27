// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Thrown when a tenant is required (<see cref="TenantContextOptions.RequireTenant"/> is enabled) but no
/// tenant could be resolved for a message or request and no default tenant is configured.
/// </summary>
/// <remarks>
/// This is the fail-fast signal for ambient tenancy: rather than proceeding with an unscoped operation on a
/// tenant-required path, resolution fails loudly (mirroring the storage-side
/// <c>Excalibur.Data.Sharding.TenantShardNotFoundException</c>).
/// </remarks>
public sealed class TenantRequiredException : InvalidOperationException
{
	/// <summary>Initializes a new instance of the <see cref="TenantRequiredException"/> class.</summary>
	public TenantRequiredException()
		: base("A tenant is required (RequireTenant is enabled) but none was resolved and no default tenant is configured.")
	{
	}

	/// <summary>Initializes a new instance of the <see cref="TenantRequiredException"/> class.</summary>
	/// <param name="message">The message that describes the error.</param>
	public TenantRequiredException(string message)
		: base(message)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="TenantRequiredException"/> class.</summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of this exception.</param>
	public TenantRequiredException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
