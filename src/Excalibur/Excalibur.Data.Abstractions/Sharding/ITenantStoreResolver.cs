// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Sharding;

/// <summary>
/// Resolves a store instance for the specified tenant, using the shard map
/// to route to the correct data store.
/// </summary>
/// <typeparam name="TStore">The store abstraction type (e.g., <c>IEventStore</c>, <c>IProjectionStore&lt;T&gt;</c>).</typeparam>
/// <remarks>
/// <para>
/// Implementations should cache store instances per shard ID to avoid
/// creating new connections on every call. Typical implementation uses
/// <c>ConcurrentDictionary&lt;string, TStore&gt;</c> keyed by shard ID.
/// </para>
/// </remarks>
#pragma warning disable RS0016 // Add public types and members to the declared API (covariant generic not representable in baseline)
public interface ITenantStoreResolver<out TStore>
{
#pragma warning restore RS0016
	/// <summary>
	/// Resolves the store instance for the specified tenant.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <returns>The store instance routed to the tenant's shard.</returns>
	/// <exception cref="TenantShardNotFoundException">
	/// Thrown when the tenant cannot be resolved to a shard.
	/// </exception>
#pragma warning disable RS0016 // Covariant generic member not representable in baseline
	TStore Resolve(string tenantId);
#pragma warning restore RS0016
}
