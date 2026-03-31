// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Sharding;

/// <summary>
/// Configuration options for tenant shard mapping.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="DefaultShardId"/> is <see langword="null"/>, unknown tenants
/// cause a <see cref="TenantShardNotFoundException"/> (fail-fast). When set,
/// unknown tenants route to the default shard.
/// </para>
/// </remarks>
public sealed class ShardMapOptions
{
	/// <summary>
	/// Gets or sets the default shard ID for unknown tenants.
	/// </summary>
	/// <value>
	/// The default shard ID, or <see langword="null"/> to fail-fast on unknown tenants.
	/// Default is <see langword="null"/>.
	/// </value>
	public string? DefaultShardId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether tenant-aware data sharding is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable tenant sharding (re-registers stores as Scoped);
	/// <see langword="false"/> to keep default Singleton behavior. Default is <see langword="false"/>.
	/// </value>
	public bool EnableTenantSharding { get; set; }
}
