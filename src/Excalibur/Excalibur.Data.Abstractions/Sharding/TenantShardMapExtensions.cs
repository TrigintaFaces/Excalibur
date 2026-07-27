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
	/// Fail-fast versus default-shard routing is the underlying <see cref="ITenantShardMap"/>'s
	/// behavior (governed by <c>ShardMapOptions.DefaultShardId</c>): when no ambient tenant is set,
	/// the empty tenant is routed like any other unknown tenant — the default shard when configured,
	/// otherwise a <see cref="TenantShardNotFoundException"/>.
	/// </remarks>
	public static ShardInfo CurrentShard(this ITenantShardMap map, ITenantContext context)
	{
		ArgumentNullException.ThrowIfNull(map);
		ArgumentNullException.ThrowIfNull(context);

		return map.GetShardInfo(context.TenantId ?? string.Empty);
	}
}
