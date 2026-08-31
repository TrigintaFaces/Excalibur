// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Data.Sharding;

/// <summary>
/// Ambient-context bridging helpers for <see cref="ITenantShardMap"/>.
/// </summary>
public static class TenantShardMapExtensions
{
	/// <summary>
	/// Resolves the <see cref="ShardInfo"/> for the <b>current ambient tenant</b> read from
	/// <paramref name="context"/>, delegating to <see cref="ITenantShardMap.GetShardInfo(string)"/>.
	/// </summary>
	/// <param name="map">The tenant shard map.</param>
	/// <param name="context">The ambient tenant context.</param>
	/// <returns>The shard routing information for the ambient tenant.</returns>
	/// <remarks>
	/// <para>
	/// The tenant term is read through <see cref="TenantScope.FromContext(ITenantContext)"/>, so a
	/// context that has resolved no tenant <b>fails closed</b> here. It is not routed as an unknown
	/// tenant: an unknown tenant is a real tenant the map has no entry for, which the map may answer
	/// with the configured default shard, whereas an unresolved context has no tenant to route at all.
	/// Routing it anyway silently returns whichever shard the map answers with, and the caller then
	/// reads and writes another tenant's data with nothing raised.
	/// </para>
	/// <para>
	/// A caller that genuinely belongs to no tenant supplies <see cref="UntenantedContext"/>, whose
	/// tenant term is the reserved untenanted partition and routes like any other key.
	/// </para>
	/// <para>
	/// Once a tenant term exists, fail-fast versus default-shard routing remains the underlying
	/// <see cref="ITenantShardMap"/>'s behavior (governed by <c>ShardMapOptions.DefaultShardId</c>):
	/// the default shard when configured, otherwise a <see cref="TenantShardNotFoundException"/>.
	/// </para>
	/// </remarks>
	/// <exception cref="TenantRequiredException">
	/// <paramref name="context"/> has resolved no tenant.
	/// </exception>
	/// <exception cref="TenantShardNotFoundException">
	/// The tenant cannot be routed to a shard and no default shard is configured.
	/// </exception>
	public static ShardInfo CurrentShard(this ITenantShardMap map, ITenantContext context)
	{
		ArgumentNullException.ThrowIfNull(map);
		ArgumentNullException.ThrowIfNull(context);

		return map.GetShardInfo(TenantScope.FromContext(context).TenantId);
	}
}
