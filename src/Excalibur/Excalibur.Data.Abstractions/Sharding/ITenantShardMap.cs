// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Sharding;

/// <summary>
/// Resolves shard routing information for a given tenant.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must be fast (target &lt; 1us) since shard resolution is on every
/// data operation's hot path. Dictionary lookup or hash-based routing is expected.
/// </para>
/// <para>
/// When the tenant is not found and no default shard is configured,
/// implementations should throw <see cref="TenantShardNotFoundException"/>.
/// </para>
/// </remarks>
public interface ITenantShardMap
{
	/// <summary>
	/// Gets the shard information for the specified tenant.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <returns>The shard routing information for the tenant.</returns>
	/// <exception cref="TenantShardNotFoundException">
	/// Thrown when the tenant is not mapped to any shard and no default shard is configured.
	/// </exception>
	ShardInfo GetShardInfo(string tenantId);

	/// <summary>
	/// Gets all registered shard IDs in this map.
	/// </summary>
	/// <returns>The collection of registered shard identifiers.</returns>
	IReadOnlyCollection<string> GetRegisteredShardIds();
}
