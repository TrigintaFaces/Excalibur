// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Redis;

/// <summary>
/// Configuration options for the Redis event store.
/// </summary>
public sealed class RedisEventStoreOptions
{
	/// <summary>
	/// Gets or sets the Redis connection string.
	/// </summary>
	/// <value>The Redis connection string.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the key prefix for event streams.
	/// </summary>
	/// <value>The key prefix for event streams. Defaults to "es".</value>
	public string StreamKeyPrefix { get; set; } = "es";

	/// <summary>
	/// Gets or sets the key for the undispatched events sorted set.
	/// </summary>
	/// <value>The undispatched sorted set key. Defaults to "es:undispatched".</value>
	public string UndispatchedSetKey { get; set; } = "es:undispatched";

	/// <summary>
	/// Gets or sets the default batch size for undispatched event retrieval.
	/// </summary>
	/// <value>The default batch size. Defaults to 100.</value>
	[Range(1, 10000)]
	public int DefaultBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the Redis database index.
	/// </summary>
	/// <value>The database index. Defaults to -1 (default database).</value>
	[Range(-1, 15)]
	public int DatabaseIndex { get; set; } = -1;
}
