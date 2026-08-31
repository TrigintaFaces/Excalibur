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
/// <para>
/// This is the ambient-context bridge over the existing sharding seam: subsystems consume the
/// current tenant's store without threading a tenant identifier by hand. The tenant term is read
/// through <see cref="TenantScope.FromContext(ITenantContext)"/>, so a context that has resolved no
/// tenant <b>fails closed</b> rather than resolving to some store: there is no unresolved tenant term
/// to route with, and routing one anyway would hand the caller a store belonging to whichever tenant
/// the shard map happened to answer with.
/// </para>
/// <para>
/// A caller that genuinely belongs to no tenant says so with a value rather than by leaving the
/// tenant unset: supply <see cref="UntenantedContext"/>, whose tenant term is the reserved untenanted
/// partition and routes like any other key. An operation that spans <em>every</em> tenant is not
/// expressible here at all — this contract resolves one tenant's store, so drive such an operation
/// per tenant, or through a store method that is estate-wide by name.
/// </para>
/// <para>
/// Once a tenant term exists, routing, fail-fast, and default-shard behavior remain the existing
/// <see cref="ITenantStoreResolver{TStore}"/>/<see cref="ITenantShardMap"/> seam's (a
/// <see cref="TenantShardNotFoundException"/> surfaces when the tenant cannot be routed and no default
/// shard is configured).
/// </para>
/// </remarks>
public interface IAmbientTenantStoreResolver<out TStore>
{
	/// <summary>
	/// Resolves the store instance routed to the current ambient tenant's shard.
	/// </summary>
	/// <returns>The store instance for the ambient tenant.</returns>
	/// <exception cref="TenantRequiredException">
	/// Thrown when the ambient context has resolved no tenant. The call is refused rather than routed
	/// with a substituted tenant term.
	/// </exception>
	/// <exception cref="TenantShardNotFoundException">
	/// Thrown when the ambient tenant cannot be resolved to a shard and no default shard is configured.
	/// </exception>
	TStore ResolveCurrent();
}
