// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
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
/// Stores snapshots in Redis Hash keys: <c>snap:t:{tenantId}:{aggregateType}:{aggregateId}</c>. The
/// tenant segment is always present — a single-tenant host resolves the framework's single-tenant
/// identity, never an omitted segment — so the key shape does not vary between single- and multi-tenant
/// hosts.
/// Each hash contains the snapshot data, version, and metadata.
/// Only the latest snapshot is stored per aggregate and tenant. A save replaces the stored snapshot
/// only when it carries a HIGHER version, so the stored snapshot never moves backwards: a save at an
/// older version is refused rather than applied, and a save at the same version is a no-op. Saves may
/// therefore arrive concurrently and in any order without the reader observing a snapshot that is not
/// the latest one written.
/// </para>
/// </remarks>
public sealed partial class RedisSnapshotStore : ISnapshotStore
{
	private readonly ConnectionMultiplexer _connection;
	private readonly RedisSnapshotStoreOptions _options;
	private readonly ILogger<RedisSnapshotStore> _logger;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	/// <summary>
	/// Compare-and-set save: writes the snapshot hash only when doing so moves the stored version
	/// FORWARD. Returns 1 when the snapshot was written, 0 when an equal-or-newer one was already there.
	/// </summary>
	/// <remarks>
	/// KEYS[1] = snapshot hash key. ARGV[1] = incoming version; ARGV[2] = TTL in seconds (0 = none);
	/// ARGV[3] = the name of the version field; ARGV[4..] = the hash's field/value pairs.
	/// </remarks>
	private static readonly string SaveIfNewerScript = """
		local key = KEYS[1]
		local new_version = tonumber(ARGV[1])
		local ttl_seconds = tonumber(ARGV[2])
		local version_field = ARGV[3]

		-- Only ever move the snapshot FORWARD. The write here was an unconditional HSET, so it was
		-- last-writer-wins by arrival: a slower save carrying an older version replaced a newer
		-- snapshot, and GetLatestSnapshotAsync then returned one that was not the latest. Concurrent
		-- saves are ordinary -- several instances can snapshot the same aggregate at once and their
		-- writes land in no guaranteed order. Every other snapshot store already enforces this in its
		-- write: the SQL providers guard their upsert, the document stores compare the stored version
		-- first. Redis was the last one that did not.
		-- The read and the write are one script because they must be one step. Doing the compare from
		-- the client would reintroduce the race it exists to close.
		local stored = redis.call('HGET', key, version_field)
		if stored ~= false and tonumber(stored) >= new_version then
			return 0
		end

		-- DEL first, so the hash is REPLACED rather than merged. HSET on its own leaves fields the
		-- incoming snapshot does not carry -- an absent metadata or tenant field left over from the
		-- previous version -- attached to the new one, handing back a snapshot whose body and version
		-- disagree. That is the same defect as a stale version, wearing the next version's number.
		redis.call('DEL', key)
		for i = 4, #ARGV, 2 do
			redis.call('HSET', key, ARGV[i], ARGV[i + 1])
		end

		-- Inside the guard: a losing save must be a complete no-op, and must not extend the lifetime
		-- of the snapshot it failed to replace.
		if ttl_seconds > 0 then
			redis.call('EXPIRE', key, ttl_seconds)
		end

		return 1
		""";

	/// <summary>
	/// Deletes the stored snapshot only when its version is below the caller's threshold. Returns 1
	/// when the key was deleted, 0 when it was absent or at/above the threshold.
	/// </summary>
	/// <remarks>KEYS[1] = snapshot hash key. ARGV[1] = threshold version; ARGV[2] = version field name.</remarks>
	private static readonly string DeleteIfOlderScript = """
		local key = KEYS[1]
		local threshold = tonumber(ARGV[1])
		local version_field = ARGV[2]

		-- One script for the same reason the save is one script: this was a HGET followed by a
		-- separate DEL, so a save landing between them was deleted on the strength of the version it
		-- had just replaced -- discarding a snapshot the threshold says to keep.
		local stored = redis.call('HGET', key, version_field)
		if stored == false or tonumber(stored) >= threshold then
			return 0
		end

		return redis.call('DEL', key)
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisSnapshotStore"/> class.
	/// </summary>
	/// <param name="connection">The Redis connection multiplexer.</param>
	/// <param name="options">The snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public RedisSnapshotStore(
		ConnectionMultiplexer connection,
		IOptions<RedisSnapshotStoreOptions> options,
		ILogger<RedisSnapshotStore> logger,
		ITenantContext tenantContext)
	{
		_connection = connection ?? throw new ArgumentNullException(nameof(connection));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
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

		var entries = ToHashEntries(snapshot, CurrentTenantScope.TenantId);

		// ARGV layout: the incoming version, the TTL, the version field's NAME, then the field/value
		// pairs. The name is passed rather than spelled inside the script so that renaming
		// HashFields.Version cannot silently disarm the guard: a script reading a field nobody writes
		// finds nothing, concludes no snapshot is stored, and overwrites on every save -- the original
		// defect restored, with a guard sitting above it that looks like it is working.
		var args = new RedisValue[3 + (entries.Length * 2)];
		args[0] = snapshot.Version;
		args[1] = _options.SnapshotTtlSeconds;
		args[2] = HashFields.Version;
		for (var i = 0; i < entries.Length; i++)
		{
			args[3 + (i * 2)] = entries[i].Name;
			args[4 + (i * 2)] = entries[i].Value;
		}

		// The TTL is applied inside the script rather than by a following KeyExpireAsync, so a losing
		// save cannot renew the lifetime of the snapshot it was refused permission to replace.
		var applied = (long)await db.ScriptEvaluateAsync(SaveIfNewerScript, [key], args).ConfigureAwait(false);

		if (applied == 0)
		{
			// Not an error: a stale save is refused, not failed. The contract is that the stored
			// snapshot never moves backwards, and it has not.
			LogStaleSnapshotIgnored(snapshot.AggregateId, snapshot.AggregateType, snapshot.Version);
			return;
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

		// Redis stores only one snapshot per aggregate, so the whole key goes when the stored version
		// is below the threshold. Read and delete are one script: as two round trips, a save landing
		// between them was deleted on the strength of the version it had just replaced.
		var deleted = (long)await db.ScriptEvaluateAsync(
			DeleteIfOlderScript,
			[key],
			[olderThanVersion, HashFields.Version]).ConfigureAwait(false);

		if (deleted == 1)
		{
			LogSnapshotsDeleted(aggregateId, aggregateType);
		}
	}

	private static HashEntry[] ToHashEntries(ISnapshot snapshot, string? tenantId)
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

		if (!string.IsNullOrEmpty(tenantId))
		{
			entries.Add(new HashEntry(HashFields.TenantId, tenantId));
		}

		if (snapshot.Metadata != null)
		{
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			entries.Add(new HashEntry(HashFields.Metadata, JsonSerializer.Serialize(snapshot.Metadata)));
#pragma warning restore IL2026, IL3050
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
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metaValue.ToString());
#pragma warning restore IL2026, IL3050
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
			TenantId = dict.TryGetValue("tenantId", out var tenant) && tenant.HasValue ? tenant.ToString() : null,
		};
	}

	private IDatabase GetDatabase() =>
		_options.DatabaseIndex >= 0
			? _connection.GetDatabase(_options.DatabaseIndex)
			: _connection.GetDatabase();

	/// <summary>
	/// Builds the key identifying a single aggregate's snapshot, including the tenant when the host is
	/// multi-tenant. Every read, write, and delete path routes through this one method, so the tenant
	/// cannot be applied to some operations and forgotten on others.
	/// </summary>
	/// <remarks>
	/// The tenant segment is emitted unconditionally, for every host. <see cref="TenantScope.FromContext"/>
	/// never returns an empty or absent tenant id — a single-tenant host resolves
	/// <c>TenantDefaults.DefaultTenantId</c>, and an untenanted write resolves the reserved untenanted
	/// sentinel — so there is no state in which the segment could be omitted.
	/// </remarks>
	private string GetSnapshotKey(string aggregateType, string aggregateId)
	{
		var scope = CurrentTenantScope;
		return $"{_options.KeyPrefix}:t:{scope.TenantId}:{aggregateType}:{aggregateId}";
	}

	[LoggerMessage(RedisEventSourcingEventId.SnapshotLoaded, LogLevel.Debug,
		"Loaded snapshot for aggregate {AggregateId} of type {AggregateType} at version {Version}")]
	private partial void LogSnapshotLoaded(string aggregateId, string aggregateType, long version);

	[LoggerMessage(RedisEventSourcingEventId.SnapshotSaved, LogLevel.Debug,
		"Saved snapshot for aggregate {AggregateId} of type {AggregateType} at version {Version}")]
	private partial void LogSnapshotSaved(string aggregateId, string aggregateType, long version);

	[LoggerMessage(RedisEventSourcingEventId.SnapshotsDeleted, LogLevel.Debug,
		"Deleted snapshots for aggregate {AggregateId} of type {AggregateType}")]
	private partial void LogSnapshotsDeleted(string aggregateId, string aggregateType);

	[LoggerMessage(RedisEventSourcingEventId.SnapshotSaveSkippedAsStale, LogLevel.Debug,
		"Ignored stale snapshot for aggregate {AggregateId} of type {AggregateType} at version {Version}: "
		+ "an equal or newer snapshot is already stored")]
	private partial void LogStaleSnapshotIgnored(string aggregateId, string aggregateType, long version);

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
		public static readonly RedisValue TenantId = "tenantId";
	}
}

/// <summary>
/// Internal snapshot implementation for Redis Hash deserialization.
/// </summary>
internal sealed class RedisSnapshot : ISnapshot
{
	/// <inheritdoc/>
	/// <remarks>
	/// Hydrated from the hash rather than the key. The key carries the tenant so that entries are
	/// isolated; this property carries it so a snapshot read back from Redis reports the tenant it
	/// belongs to rather than silently claiming to be single-tenant.
	/// </remarks>
	public string? TenantId { get; init; }

	/// <inheritdoc/>
	public required string SnapshotId { get; init; }

	/// <inheritdoc/>
	public required string AggregateId { get; init; }

	/// <inheritdoc/>
	public required long Version { get; init; }

	/// <inheritdoc/>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <inheritdoc/>
	public required ReadOnlyMemory<byte> Data { get; init; }

	/// <inheritdoc/>
	public required string AggregateType { get; init; }

	/// <inheritdoc/>
	public IDictionary<string, object>? Metadata { get; init; }
}
