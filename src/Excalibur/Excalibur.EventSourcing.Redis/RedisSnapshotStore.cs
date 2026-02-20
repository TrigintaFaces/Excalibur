// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text.Json;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Redis.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.EventSourcing.Redis;

/// <summary>
/// Redis Hash-based implementation of <see cref="ISnapshotStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores snapshots in Redis Hash keys: <c>snap:{aggregateType}:{aggregateId}</c>.
/// Each hash contains the snapshot data, version, and metadata.
/// Only the latest snapshot is stored per aggregate (overwritten on save).
/// </para>
/// </remarks>
public sealed partial class RedisSnapshotStore : ISnapshotStore
{
	private readonly ConnectionMultiplexer _connection;
	private readonly RedisSnapshotStoreOptions _options;
	private readonly ILogger<RedisSnapshotStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisSnapshotStore"/> class.
	/// </summary>
	/// <param name="connection">The Redis connection multiplexer.</param>
	/// <param name="options">The snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	public RedisSnapshotStore(
		ConnectionMultiplexer connection,
		IOptions<RedisSnapshotStoreOptions> options,
		ILogger<RedisSnapshotStore> logger)
	{
		_connection = connection ?? throw new ArgumentNullException(nameof(connection));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var db = GetDatabase();
		var key = GetSnapshotKey(aggregateType, aggregateId);

		var entries = await db.HashGetAllAsync(key).ConfigureAwait(false);
		if (entries.Length == 0)
		{
			LogSnapshotNotFound(aggregateId, aggregateType);
			return null;
		}

		var snapshot = FromHashEntries(entries);
		LogSnapshotLoaded(aggregateId, aggregateType, snapshot.Version);

		return snapshot;
	}

	/// <inheritdoc/>
	public async ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var db = GetDatabase();
		var key = GetSnapshotKey(snapshot.AggregateType, snapshot.AggregateId);

		var entries = ToHashEntries(snapshot);

		await db.HashSetAsync(key, entries).ConfigureAwait(false);

		// Apply TTL if configured
		if (_options.SnapshotTtlSeconds > 0)
		{
			await db.KeyExpireAsync(key, TimeSpan.FromSeconds(_options.SnapshotTtlSeconds)).ConfigureAwait(false);
		}

		LogSnapshotSaved(snapshot.AggregateId, snapshot.AggregateType, snapshot.Version);
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var db = GetDatabase();
		var key = GetSnapshotKey(aggregateType, aggregateId);

		await db.KeyDeleteAsync(key).ConfigureAwait(false);

		LogSnapshotsDeleted(aggregateId, aggregateType);
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var db = GetDatabase();
		var key = GetSnapshotKey(aggregateType, aggregateId);

		// Redis stores only one snapshot per aggregate, so if the stored version
		// is older than the threshold, delete the entire key.
		var versionValue = await db.HashGetAsync(key, HashFields.Version).ConfigureAwait(false);
		if (versionValue.HasValue && (long)versionValue < olderThanVersion)
		{
			await db.KeyDeleteAsync(key).ConfigureAwait(false);
			LogSnapshotsDeleted(aggregateId, aggregateType);
		}
	}

	private static HashEntry[] ToHashEntries(ISnapshot snapshot)
	{
		var entries = new List<HashEntry>
		{
			new(HashFields.SnapshotId, snapshot.SnapshotId),
			new(HashFields.AggregateId, snapshot.AggregateId),
			new(HashFields.AggregateType, snapshot.AggregateType),
			new(HashFields.Version, snapshot.Version),
			new(HashFields.CreatedAt, snapshot.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
			new(HashFields.Data, snapshot.Data),
		};

		if (snapshot.Metadata != null)
		{
			entries.Add(new HashEntry(HashFields.Metadata, JsonSerializer.Serialize(snapshot.Metadata)));
		}

		return entries.ToArray();
	}

	private static RedisSnapshot FromHashEntries(HashEntry[] entries)
	{
		var dict = entries.ToDictionary(
			static e => e.Name.ToString(),
			static e => e.Value,
			StringComparer.Ordinal);

		IDictionary<string, object>? metadata = null;
		if (dict.TryGetValue("metadata", out var metaValue) && metaValue.HasValue)
		{
			metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metaValue.ToString());
		}

		return new RedisSnapshot
		{
			SnapshotId = dict.GetValueOrDefault("snapshotId", RedisValue.EmptyString).ToString(),
			AggregateId = dict.GetValueOrDefault("aggregateId", RedisValue.EmptyString).ToString(),
			AggregateType = dict.GetValueOrDefault("aggregateType", RedisValue.EmptyString).ToString(),
			Version = (long)dict.GetValueOrDefault("version", 0L),
			CreatedAt = dict.TryGetValue("createdAt", out var ts)
				? DateTimeOffset.Parse(ts.ToString(), CultureInfo.InvariantCulture)
				: DateTimeOffset.UtcNow,
			Data = dict.TryGetValue("data", out var data) ? (byte[])data! : [],
			Metadata = metadata,
		};
	}

	private IDatabase GetDatabase() =>
		_options.DatabaseIndex >= 0
			? _connection.GetDatabase(_options.DatabaseIndex)
			: _connection.GetDatabase();

	private string GetSnapshotKey(string aggregateType, string aggregateId) =>
		$"{_options.KeyPrefix}:{aggregateType}:{aggregateId}";

	[LoggerMessage(RedisEventSourcingEventId.SnapshotLoaded, LogLevel.Debug,
		"Loaded snapshot for aggregate {AggregateId} of type {AggregateType} at version {Version}")]
	private partial void LogSnapshotLoaded(string aggregateId, string aggregateType, long version);

	[LoggerMessage(RedisEventSourcingEventId.SnapshotSaved, LogLevel.Debug,
		"Saved snapshot for aggregate {AggregateId} of type {AggregateType} at version {Version}")]
	private partial void LogSnapshotSaved(string aggregateId, string aggregateType, long version);

	[LoggerMessage(RedisEventSourcingEventId.SnapshotsDeleted, LogLevel.Debug,
		"Deleted snapshots for aggregate {AggregateId} of type {AggregateType}")]
	private partial void LogSnapshotsDeleted(string aggregateId, string aggregateType);

	[LoggerMessage(RedisEventSourcingEventId.SnapshotNotFound, LogLevel.Debug,
		"No snapshot found for aggregate {AggregateId} of type {AggregateType}")]
	private partial void LogSnapshotNotFound(string aggregateId, string aggregateType);

	private static class HashFields
	{
		public static readonly RedisValue SnapshotId = "snapshotId";
		public static readonly RedisValue AggregateId = "aggregateId";
		public static readonly RedisValue AggregateType = "aggregateType";
		public static readonly RedisValue Version = "version";
		public static readonly RedisValue CreatedAt = "createdAt";
		public static readonly RedisValue Data = "data";
		public static readonly RedisValue Metadata = "metadata";
	}
}

/// <summary>
/// Internal snapshot implementation for Redis Hash deserialization.
/// </summary>
internal sealed class RedisSnapshot : ISnapshot
{
	/// <inheritdoc/>
	public required string SnapshotId { get; init; }

	/// <inheritdoc/>
	public required string AggregateId { get; init; }

	/// <inheritdoc/>
	public required long Version { get; init; }

	/// <inheritdoc/>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <inheritdoc/>
	public required byte[] Data { get; init; }

	/// <inheritdoc/>
	public required string AggregateType { get; init; }

	/// <inheritdoc/>
	public IDictionary<string, object>? Metadata { get; init; }
}
