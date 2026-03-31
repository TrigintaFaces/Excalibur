// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Sharding;

/// <summary>
/// Thrown when a tenant cannot be resolved to a data shard and no default shard is configured.
/// </summary>
public sealed class TenantShardNotFoundException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TenantShardNotFoundException"/> class.
	/// </summary>
	/// <param name="tenantId">The tenant identifier that could not be resolved.</param>
	public TenantShardNotFoundException(string tenantId)
		: base($"No shard mapping found for tenant '{tenantId}' and no default shard is configured.")
	{
		TenantId = tenantId;
	}

	/// <summary>
	/// Gets the tenant identifier that could not be resolved.
	/// </summary>
	/// <value>The tenant identifier.</value>
	public string TenantId { get; }
}
