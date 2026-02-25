// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Redis;

/// <summary>
/// Configuration options for the Redis snapshot store.
/// </summary>
public sealed class RedisSnapshotStoreOptions
{
	/// <summary>
	/// Gets or sets the Redis connection string.
	/// </summary>
	/// <value>The Redis connection string.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the key prefix for snapshot hashes.
	/// </summary>
	/// <value>The key prefix for snapshot hashes. Defaults to "snap".</value>
	public string KeyPrefix { get; set; } = "snap";

	/// <summary>
	/// Gets or sets the optional TTL for snapshots in seconds.
	/// </summary>
	/// <value>The snapshot TTL in seconds. Set to 0 for no expiration. Defaults to 0.</value>
	[Range(0, int.MaxValue)]
	public int SnapshotTtlSeconds { get; set; }

	/// <summary>
	/// Gets or sets the Redis database index.
	/// </summary>
	/// <value>The database index. Defaults to -1 (default database).</value>
	[Range(-1, 15)]
	public int DatabaseIndex { get; set; } = -1;
}
