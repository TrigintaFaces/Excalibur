// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Data.Sharding;

/// <summary>
/// Resolves the store instance for the <b>current ambient tenant</b>, bridging the ambient
/// <see cref="ITenantContext"/> to the tenant-keyed <see cref="ITenantStoreResolver{TStore}"/>.
/// </summary>
/// <typeparam name="TStore">The store abstraction type (e.g., <c>IEventStore</c>, <c>IProjectionStore&lt;T&gt;</c>).</typeparam>
/// <remarks>
/// This is the ambient-context bridge over the existing sharding seam: subsystems consume the
/// current tenant's store without threading a tenant identifier by hand. The tenant is read from
/// <see cref="ITenantContext.TenantId"/>; routing, fail-fast, and default-shard behavior are the
/// existing <see cref="ITenantStoreResolver{TStore}"/>/<see cref="ITenantShardMap"/> seam's
/// (a <see cref="TenantShardNotFoundException"/> surfaces when the tenant cannot be routed and no
/// default shard is configured).
/// </remarks>
public interface IAmbientTenantStoreResolver<out TStore>
{
	/// <summary>
	/// Resolves the store instance routed to the current ambient tenant's shard.
	/// </summary>
	/// <returns>The store instance for the ambient tenant.</returns>
	/// <exception cref="TenantShardNotFoundException">
	/// Thrown when the ambient tenant cannot be resolved to a shard and no default shard is configured.
	/// </exception>
	TStore ResolveCurrent();
}
