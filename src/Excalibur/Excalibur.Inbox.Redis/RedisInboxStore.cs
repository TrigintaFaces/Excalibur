// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Excalibur.Data.Redis.Diagnostics;
using Excalibur.Dispatch;

using Excalibur.Inbox.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.Inbox.Redis;

/// <summary>
/// Redis-based implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// Uses Redis SETNX (SET ... NX) for atomic first-writer-wins semantics.
/// Keys are formatted as: {KeyPrefix}:{messageId}:{handlerType}, so the tenant is a key segment and the
/// write/dedup/claim paths and keyed reads are isolated by construction — two tenants carrying the same
/// message id never dedup against each other.
/// Uses Redis TTL for automatic cleanup of processed entries.
/// <para>
/// The <see cref="IInboxStoreAdmin"/> operator surface (<c>GetAllEntries</c>, <c>GetFailedEntries</c>,
/// <c>GetStatistics</c>, <c>Cleanup</c>) scans <c>{KeyPrefix}:*</c> and is <b>estate-wide by design</b>:
/// it serves operators and background services (dashboards, retention, retry processors), not the
/// per-tenant request path. A message handler depends on <see cref="IInboxStore"/>, which does not expose
/// these methods, so a per-tenant caller cannot reach a cross-tenant read or delete. This estate-wide scan
/// is a deliberate global, not an oversight — matching <c>SqlServerInboxStore</c>.
/// </para>
/// </remarks>
public sealed partial class RedisInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IInboxStoreAdmin, IAsyncDisposable
{
	// Atomic conditional delete: remove the claim only if it exists and is NOT terminal (Processed), so a
	// concurrently-finalized entry is never deleted. Mirrors the Postgres "DELETE ... WHERE status <> Processed"
	// guarantee in a single round-trip (GET-then-DEL is not atomic on its own).
	private const string ReleaseClaimScript =
		"""
		local v = redis.call('GET', KEYS[1])
		if not v then return 0 end
		local ok, doc = pcall(cjson.decode, v)
		if ok and doc and tonumber(doc.Status) == tonumber(ARGV[1]) then return 0 end
		return redis.call('DEL', KEYS[1])
		""";

	// Atomic guarded transition for a NON-terminal mutation (Processing / Failed): replace the stored value
	// only if the entry exists AND its CURRENT persisted Status is not terminal (Processed). Prevents a
	// concurrent read-modify-write from DOWNGRADING a finalized entry back to a non-terminal state, which
	// would re-admit the message → double-processing. Returns 1 = transitioned, 0 = refused (terminal),
	// -1 = not found. ARGV[1] = Processed ordinal, ARGV[2] = new serialized document.
	private const string GuardedTransitionIfNotProcessedScript =
		"""
		local v = redis.call('GET', KEYS[1])
		if not v then return -1 end
		local ok, doc = pcall(cjson.decode, v)
		if ok and doc and tonumber(doc.Status) == tonumber(ARGV[1]) then return 0 end
		redis.call('SET', KEYS[1], ARGV[2])
		return 1
		""";

	// Atomic finalize to the terminal Processed state: set the new value (and optional retention TTL) only if
	// the entry exists AND is not ALREADY Processed; otherwise report the existing state so the caller can
	// honor the not-found / already-processed contracts without a separate non-atomic GET. Returns 1 =
	// finalized, 0 = already Processed, -1 = not found. ARGV[1] = Processed ordinal, ARGV[2] = new value,
	// ARGV[3] = retention TTL seconds (<= 0 => no TTL).
	private const string FinalizeProcessedScript =
		"""
		local v = redis.call('GET', KEYS[1])
		if not v then return -1 end
		local ok, doc = pcall(cjson.decode, v)
		if ok and doc and tonumber(doc.Status) == tonumber(ARGV[1]) then return 0 end
		redis.call('SET', KEYS[1], ARGV[2])
		local ttl = tonumber(ARGV[3])
		if ttl and ttl > 0 then redis.call('PEXPIRE', KEYS[1], ttl * 1000) end
		return 1
		""";

	// Atomic lease CAS. Claim IFF the entry is absent, its status is Received, or its status is Processing
	// but its lease has expired (reclaiming a dead processor). "now" comes from redis.call('TIME') (the
	// SERVER clock) so competing app instances never decide expiry with a skewed local clock. The lease
	// expiry is written into the entry JSON (LeaseExpiresAt, unix-ms) server-side via cjson.
	// KEYS[1] = entry key. ARGV[1] = new Processing entry JSON; ARGV[2] = lease ms; ARGV[3] = Received
	// status; ARGV[4] = Processing status. Returns 1 on claim/reclaim, 0 otherwise.
	private const string LeaseClaimScript =
		"""
		local cur = redis.call('GET', KEYS[1])
		local t = redis.call('TIME')
		local now_ms = (tonumber(t[1]) * 1000) + math.floor(tonumber(t[2]) / 1000)
		local lease_ms = tonumber(ARGV[2])
		local received = tonumber(ARGV[3])
		local processing = tonumber(ARGV[4])
		local failed = tonumber(ARGV[5])
		local claimable = false
		local carried_retry = 0
		if not cur then
			claimable = true
		else
			local ok, doc = pcall(cjson.decode, cur)
			if ok and doc then
				local status = tonumber(doc.Status)
				carried_retry = tonumber(doc.RetryCount) or 0
				if status == received then
					claimable = true
				elseif status == failed then
					claimable = true
				elseif status == processing then
					-- A Processing entry is reclaimable ONLY if it carries an EXPIRED lease (a dead
					-- processor). A nil LeaseExpiresAt means a live claim with no lease (e.g. the non-lease
					-- MarkProcessing path) — treat it as LIVE, never reclaimable, or two claimants could both
					-- admit the same message (double-processing).
					local exp = tonumber(doc.LeaseExpiresAt)
					if (exp ~= nil) and (exp < now_ms) then claimable = true end
				end
			end
		end
		if not claimable then return 0 end
		local ok2, newdoc = pcall(cjson.decode, ARGV[1])
		if not ok2 or not newdoc then return 0 end
		newdoc.LeaseExpiresAt = now_ms + lease_ms
		newdoc.RetryCount = carried_retry
		redis.call('SET', KEYS[1], cjson.encode(newdoc))
		return 1
		""";

	private static readonly CompositeFormat EntryAlreadyExistsFormat =
		CompositeFormat.Parse("Inbox entry already exists for message '{0}' and handler '{1}'.");

	private static readonly CompositeFormat EntryNotFoundFormat =
		CompositeFormat.Parse("Inbox entry not found for message '{0}' and handler '{1}'.");

	private static readonly CompositeFormat EntryAlreadyProcessedFormat =
		CompositeFormat.Parse("Inbox entry already processed for message '{0}' and handler '{1}'.");

	private readonly RedisInboxOptions _options;
	private readonly ILogger<RedisInboxStore> _logger;

	// Ambient tenant context. When active, the tenant is composed INTO the Redis key (via GetKey) so two
	// tenants' identical (messageId, handlerType) never collide on the dedup key, and stamped on write. When
	// null / no resolved tenant (non-multi-tenant), keys and rows are byte-identical to the un-scoped form.
	private readonly ITenantContext? _tenantContext;

	private ConnectionMultiplexer? _connection;
	private IDatabase? _database;
	private bool _ownsConnection;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStore"/> class.
	/// </summary>
	/// <param name="options">The Redis inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">Optional ambient tenant context; when active it scopes the Redis dedup key and stamps the tenant on write (null = non-multi-tenant, byte-identical behavior).</param>
	public RedisInboxStore(
		IOptions<RedisInboxOptions> options,
		ILogger<RedisInboxStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_tenantContext = tenantContext;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStore"/> class with an existing connection.
	/// </summary>
	/// <param name="connection">An existing Redis connection multiplexer.</param>
	/// <param name="options">The Redis inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">Optional ambient tenant context; when active it scopes the Redis dedup key and stamps the tenant on write (null = non-multi-tenant, byte-identical behavior).</param>
	public RedisInboxStore(
		ConnectionMultiplexer connection,
		IOptions<RedisInboxOptions> options,
		ILogger<RedisInboxStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connection = connection;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_tenantContext = tenantContext;
		_database = connection.GetDatabase(_options.DatabaseId);
	}

	// The tenant to stamp on a written row: the ambient tenant when scoped, else the entry's own tenant.
	private string? StampTenant(string? fallback = null)
	{
		var scope = TenantScope.FromContext(_tenantContext);
		return scope.IsScoped ? scope.TenantId : fallback;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(metadata);

		using var activity = InboxActivitySource.StartCreateEntryActivity(messageId, handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);
		var key = GetKey(messageId, handlerType);
		var value = SerializeEntry(entry);

		// Use SETNX for atomic first-writer-wins
		var wasSet = await db.StringSetAsync(
			key,
			value,
			when: When.NotExists).ConfigureAwait(false);

		if (!wasSet)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					EntryAlreadyExistsFormat,
					messageId,
					handlerType));
		}

		// NO TTL on a freshly-created (non-terminal, Received) entry — same reasoning as TryClaimAsync: a
		// retention TTL on an in-flight entry could expire before the handler finishes and finalizes it,
		// re-admitting the message → double-processing. The retention TTL is applied only on the terminal
		// Processed transition. Aligns with the other inbox providers.

		LogCreatedEntry(_logger, messageId, handlerType, null);
		return entry;
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await db.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					EntryNotFoundFormat,
					messageId,
					handlerType));
		}

		var entry = DeserializeEntry(value!);
		entry.MarkProcessed();

		// Atomic finalize: refuse if the entry was concurrently finalized (already Processed) or removed, so the
		// not-found / already-processed contracts hold even under concurrent finalizers (no GET-then-SET race).
		var finalizeResult = (long)await db.ScriptEvaluateAsync(
			FinalizeProcessedScript,
			[key],
			[(int)InboxStatus.Processed, SerializeEntry(entry), _options.DefaultTtlSeconds]).ConfigureAwait(false);

		if (finalizeResult == -1)
		{
			throw new InvalidOperationException(
				string.Format(CultureInfo.InvariantCulture, EntryNotFoundFormat, messageId, handlerType));
		}

		if (finalizeResult == 0)
		{
			throw new InvalidOperationException(
				string.Format(CultureInfo.InvariantCulture, EntryAlreadyProcessedFormat, messageId, handlerType));
		}

		LogProcessedEntry(_logger, messageId, handlerType, null);
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await db.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					EntryNotFoundFormat,
					messageId,
					handlerType));
		}

		var entry = DeserializeEntry(value!);
		entry.MarkProcessing();

		// Atomic guarded write: never downgrade a concurrently-finalized (Processed) entry back to Processing,
		// which would re-admit the message → double-processing. Refused (terminal) → no-op.
		var transitionResult = (long)await db.ScriptEvaluateAsync(
			GuardedTransitionIfNotProcessedScript,
			[key],
			[(int)InboxStatus.Processed, SerializeEntry(entry)]).ConfigureAwait(false);

		if (transitionResult == 1)
		{
			_logger.LogDebug("Marked inbox entry as processing for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug(
				"Skipped Processing transition for message {MessageId} and handler {HandlerType} (entry finalized or removed concurrently)",
				messageId, handlerType);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);

		// Create a minimal entry for the processed record
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			Status = InboxStatus.Processed,
			ProcessedAt = DateTimeOffset.UtcNow
		};

		var value = SerializeEntry(entry);

		// Use SETNX for atomic first-writer-wins. Value + retention TTL are written in a SINGLE
		// atomic SET so a crash between the two cannot leave a terminal (Processed) entry with no
		// expiry — an immortal dedup key. This is the terminal transition, so the retention TTL is safe.
		var expiry = _options.DefaultTtlSeconds > 0
			? TimeSpan.FromSeconds(_options.DefaultTtlSeconds)
			: (TimeSpan?)null;

		var wasSet = await db.StringSetAsync(
			key,
			value,
			expiry,
			when: When.NotExists).ConfigureAwait(false);

		if (wasSet)
		{
			LogTryMarkProcessedSuccess(_logger, messageId, handlerType, null);
			return true;
		}

		LogTryMarkProcessedDuplicate(_logger, messageId, handlerType, null);
		return false;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);
		var key = GetKey(messageId, handlerType);

		// Atomic first-writer-wins claim into the NON-TERMINAL Processing state via SET NX. true => claim
		// acquired; false => an entry already exists (already claimed/processed) => duplicate. The claim is a
		// placeholder finalized via MarkProcessedAsync (Processing -> Processed) and removed via ReleaseAsync.
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			Status = InboxStatus.Processing,
			ReceivedAt = DateTimeOffset.UtcNow,
		};

		var claimed = await db.StringSetAsync(key, SerializeEntry(entry), when: When.NotExists).ConfigureAwait(false);

		// NO TTL on a non-terminal (Processing) claim: the retention TTL is only applied once the entry
		// reaches the terminal Processed state (MarkProcessedAsync / TryMarkAsProcessedAsync). A TTL on the
		// transient claim would let the dedup key expire while the handler is still running (handler
		// runtime > TTL), re-admitting the message → double-processing. On handler failure the claim is
		// removed via ReleaseAsync. Aligns with the other inbox providers.

		if (claimed)
		{
			_logger.LogDebug("Claimed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Claim denied (already claimed/processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return claimed;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(
		string messageId,
		string handlerType,
		TimeSpan leaseDuration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

		var db = await GetDatabaseAsync().ConfigureAwait(false);
		var key = GetKey(messageId, handlerType);

		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			Status = InboxStatus.Processing,
			ReceivedAt = DateTimeOffset.UtcNow,
		};

		var result = (long)await db.ScriptEvaluateAsync(
			LeaseClaimScript,
			[key],
			[
				SerializeEntry(entry),
				(long)leaseDuration.TotalMilliseconds,
				(int)InboxStatus.Received,
				(int)InboxStatus.Processing,
				(int)InboxStatus.Failed
			]).ConfigureAwait(false);

		var claimed = result == 1;

		if (claimed)
		{
			_logger.LogDebug("Lease-claimed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Lease-claim denied (live lease or processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return claimed;
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);
		var key = GetKey(messageId, handlerType);

		// Atomically remove the non-terminal claim so a redelivery can re-admit; the Lua guard never deletes a
		// finalized (Processed) entry. No-op if already removed or never claimed.
		_ = await db.ScriptEvaluateAsync(
			ReleaseClaimScript,
			[key],
			[(int)InboxStatus.Processed]).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartExistsActivity(messageId, handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await db.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			return false;
		}

		var entry = DeserializeEntry(value!);
		return entry.Status == InboxStatus.Processed;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await db.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			return null;
		}

		return DeserializeEntry(value!);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await db.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					EntryNotFoundFormat,
					messageId,
					handlerType));
		}

		var entry = DeserializeEntry(value!);
		entry.MarkFailed(errorMessage);

		// Atomic guarded write: never downgrade a concurrently-finalized (Processed) entry to Failed.
		_ = await db.ScriptEvaluateAsync(
			GuardedTransitionIfNotProcessedScript,
			[key],
			[(int)InboxStatus.Processed, SerializeEntry(entry)]).ConfigureAwait(false);

		LogFailedEntry(_logger, messageId, handlerType, errorMessage, null);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await db.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					EntryNotFoundFormat,
					messageId,
					handlerType));
		}

		var entry = DeserializeEntry(value!);

		// Set the retry count EXACTLY (no increment) so a transient short-circuit leaves the entry
		// re-admittable without consuming a delivery attempt (FR-4).
		entry.Status = InboxStatus.Failed;
		entry.LastError = errorMessage;
		entry.RetryCount = retryCount;
		entry.LastAttemptAt = DateTimeOffset.UtcNow;

		// Atomic guarded write: never downgrade a concurrently-finalized (Processed) entry to Failed.
		_ = await db.ScriptEvaluateAsync(
			GuardedTransitionIfNotProcessedScript,
			[key],
			[(int)InboxStatus.Processed, SerializeEntry(entry)]).ConfigureAwait(false);

		LogFailedEntry(_logger, messageId, handlerType, errorMessage, null);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var entries = new List<InboxEntry>();
		var pattern = $"{_options.KeyPrefix}:*";

		foreach (var entry in await ReadAllEntriesAsync(db, pattern, cancellationToken).ConfigureAwait(false))
		{
			if (entry.Status == InboxStatus.Failed && entry.RetryCount < maxRetries)
			{
				if (!olderThan.HasValue || entry.LastAttemptAt < olderThan)
				{
					entries.Add(entry);
					if (entries.Count >= batchSize)
					{
						break;
					}
				}
			}
		}

		return entries;
	}

	/// <summary>
	/// Enumerates every entry under <paramref name="pattern"/> using a SCAN to collect the keys and a
	/// single <c>MGET</c> (<c>IDatabase.StringGetAsync</c>) round-trip to
	/// read the values, rather than one <c>StringGetAsync</c> await per key (N round-trips).
	/// </summary>
	private async Task<List<InboxEntry>> ReadAllEntriesAsync(
		IDatabase db,
		string pattern,
		CancellationToken cancellationToken)
	{
		var keys = new List<RedisKey>();
		await foreach (var key in ScanKeysAsync(pattern).ConfigureAwait(false))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			keys.Add(key);
		}

		if (keys.Count == 0)
		{
			return new List<InboxEntry>();
		}

		// Single MGET round-trip for all scanned keys.
		var values = await db.StringGetAsync(keys.ToArray()).ConfigureAwait(false);

		var entries = new List<InboxEntry>(values.Length);
		foreach (var value in values)
		{
			if (!value.IsNullOrEmpty)
			{
				entries.Add(DeserializeEntry(value!));
			}
		}

		return entries;
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var pattern = $"{_options.KeyPrefix}:*";
		return await ReadAllEntriesAsync(db, pattern, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var total = 0;
		var processed = 0;
		var failed = 0;
		var pending = 0;

		var pattern = $"{_options.KeyPrefix}:*";

		foreach (var entry in await ReadAllEntriesAsync(db, pattern, cancellationToken).ConfigureAwait(false))
		{
			total++;

			switch (entry.Status)
			{
				case InboxStatus.Processed:
					processed++;
					break;

				case InboxStatus.Failed:
					failed++;
					break;

				case InboxStatus.Received:
				case InboxStatus.Processing:
					pending++;
					break;

				default:
					pending++;
					break;
			}
		}

		return new InboxStatistics { TotalEntries = total, ProcessedEntries = processed, FailedEntries = failed, PendingEntries = pending };
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		using var activity = InboxActivitySource.StartCleanupActivity();

		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var cutoff = olderThan;
		var deleted = 0;
		var pattern = $"{_options.KeyPrefix}:*";

		const int scanChunkSize = 256;
		var buffer = new List<RedisKey>(scanChunkSize);

		await foreach (var key in ScanKeysAsync(pattern).ConfigureAwait(false))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			buffer.Add(key);
			if (buffer.Count >= scanChunkSize)
			{
				deleted += await CleanupExpiredChunkAsync(db, buffer, cutoff).ConfigureAwait(false);
				buffer.Clear();
			}
		}

		if (buffer.Count > 0 && !cancellationToken.IsCancellationRequested)
		{
			deleted += await CleanupExpiredChunkAsync(db, buffer, cutoff).ConfigureAwait(false);
		}

		LogCleanedUpEntries(_logger, deleted, null);
		return deleted;
	}

	/// <summary>
	/// MGETs a batch of scanned keys in one pipelined round-trip, then pipelines the deletes for the
	/// entries eligible for cleanup — avoiding a per-key GET+DELETE network RTT during SCAN iteration.
	/// </summary>
	private async Task<int> CleanupExpiredChunkAsync(IDatabase db, IReadOnlyList<RedisKey> keys, DateTimeOffset cutoff)
	{
		var values = await db.StringGetAsync([.. keys]).ConfigureAwait(false);

		var toDelete = new List<RedisKey>(keys.Count);
		for (var i = 0; i < values.Length; i++)
		{
			var value = values[i];
			if (value.IsNullOrEmpty)
			{
				continue;
			}

			var entry = DeserializeEntry(value!);
			if (entry.Status == InboxStatus.Processed && entry.ProcessedAt.HasValue && entry.ProcessedAt < cutoff)
			{
				toDelete.Add(keys[i]);
			}
		}

		if (toDelete.Count == 0)
		{
			return 0;
		}

		var deleteTasks = new Task<bool>[toDelete.Count];
		for (var i = 0; i < toDelete.Count; i++)
		{
			deleteTasks[i] = db.KeyDeleteAsync(toDelete[i]);
		}

		var results = await Task.WhenAll(deleteTasks).ConfigureAwait(false);

		var deleted = 0;
		foreach (var removed in results)
		{
			if (removed)
			{
				deleted++;
			}
		}

		return deleted;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Only tear down a multiplexer this store created. A caller-supplied multiplexer is shared and
		// owned by the caller — disposing it here would break other consumers of the same connection.
		if (_ownsConnection && _connection != null)
		{
			await _connection.CloseAsync().ConfigureAwait(false);
			_connection.Dispose();
		}
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Inbox entries include dynamic metadata that requires runtime serialization.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Inbox entries include dynamic metadata that requires runtime serialization.")]
	private string SerializeEntry(InboxEntry entry)
	{
		var document = new RedisInboxDocument
		{
			MessageId = entry.MessageId,
			HandlerType = entry.HandlerType,
			MessageType = entry.MessageType,
			Payload = entry.Payload,
			Metadata = new Dictionary<string, object>(entry.Metadata, StringComparer.Ordinal),
			ReceivedAt = entry.ReceivedAt,
			ProcessedAt = entry.ProcessedAt,
			Status = (int)entry.Status,
			LastError = entry.LastError,
			RetryCount = entry.RetryCount,
			LastAttemptAt = entry.LastAttemptAt,
			CorrelationId = entry.CorrelationId,
			TenantId = StampTenant(entry.TenantId),
			Source = entry.Source
		};

		return JsonSerializer.Serialize(document);
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Inbox entries include dynamic metadata that requires runtime deserialization.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Inbox entries include dynamic metadata that requires runtime deserialization.")]
	private static InboxEntry DeserializeEntry(string json)
	{
		var document = JsonSerializer.Deserialize<RedisInboxDocument>(json)
					   ?? throw new InvalidOperationException("Failed to deserialize inbox entry.");

		var metadata = document.Metadata ?? new Dictionary<string, object>(StringComparer.Ordinal);

		return new InboxEntry
		{
			MessageId = document.MessageId,
			HandlerType = document.HandlerType,
			MessageType = document.MessageType,
			Payload = document.Payload ?? [],
			Metadata = metadata,
			ReceivedAt = document.ReceivedAt,
			ProcessedAt = document.ProcessedAt,
			Status = (InboxStatus)document.Status,
			LastError = document.LastError,
			RetryCount = document.RetryCount,
			LastAttemptAt = document.LastAttemptAt,
			CorrelationId = document.CorrelationId,
			TenantId = document.TenantId,
			Source = document.Source
		};
	}

	[LoggerMessage(DataRedisEventId.InboxEntryCreated, LogLevel.Debug,
		"Created inbox entry for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogCreatedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxEntryProcessed, LogLevel.Debug,
		"Marked inbox entry as processed for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogProcessedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxTryMarkProcessedSuccess, LogLevel.Debug,
		"TryMarkAsProcessed succeeded for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedSuccess(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxTryMarkProcessedDuplicate, LogLevel.Debug,
		"TryMarkAsProcessed detected duplicate for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedDuplicate(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxEntryFailed, LogLevel.Warning,
		"Marked inbox entry as failed for message '{MessageId}' and handler '{HandlerType}': {ErrorMessage}")]
	private static partial void LogFailedEntry(ILogger logger, string messageId, string handlerType, string errorMessage,
		Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxCleanedUp, LogLevel.Information, "Cleaned up {Count} inbox entries")]
	private static partial void LogCleanedUpEntries(ILogger logger, int count, Exception? exception);

	// Composes the tenant INTO the Redis key when multi-tenancy is active (byte-identical when None), so the
	// dedup/claim key — and thus every keyed read/write — is tenant-isolated by construction.
	private string GetKey(string messageId, string handlerType)
	{
		var scope = TenantScope.FromContext(_tenantContext);
		return scope.IsScoped
			? $"{_options.KeyPrefix}:{scope.TenantId}:{messageId}:{handlerType}"
			: $"{_options.KeyPrefix}:{messageId}:{handlerType}";
	}

	/// <summary>
	/// Ensures the Redis connection is established and returns the database instance.
	/// </summary>
	private async Task<IDatabase> GetDatabaseAsync()
	{
		if (_database != null)
		{
			return _database;
		}

		var configOptions = ConfigurationOptions.Parse(_options.ConnectionString);
		configOptions.ConnectTimeout = _options.ConnectTimeoutMs;
		configOptions.SyncTimeout = _options.SyncTimeoutMs;
		configOptions.AbortOnConnectFail = _options.AbortOnConnectFail;
		configOptions.Ssl = _options.UseSsl;

		if (!string.IsNullOrEmpty(_options.Password))
		{
			configOptions.Password = _options.Password;
		}

		_connection = await ConnectionMultiplexer.ConnectAsync(configOptions).ConfigureAwait(false);
		_ownsConnection = true;
		_database = _connection.GetDatabase(_options.DatabaseId);
		return _database;
	}

	private async IAsyncEnumerable<RedisKey> ScanKeysAsync(string pattern)
	{
		var connection = _connection;
		if (connection == null)
		{
			yield break;
		}

		var server = connection.GetServers().FirstOrDefault();
		if (server == null)
		{
			yield break;
		}

		await foreach (var key in server.KeysAsync(_options.DatabaseId, pattern).ConfigureAwait(false))
		{
			yield return key;
		}
	}
}

/// <summary>
/// Internal document model for Redis serialization.
/// </summary>
internal sealed class RedisInboxDocument
{
	public string MessageId { get; set; } = string.Empty;
	public string HandlerType { get; set; } = string.Empty;
	public string MessageType { get; set; } = string.Empty;
	public byte[]? Payload { get; set; }
	public Dictionary<string, object>? Metadata { get; set; }
	public DateTimeOffset ReceivedAt { get; set; }
	public DateTimeOffset? ProcessedAt { get; set; }
	public int Status { get; set; }
	public string? LastError { get; set; }
	public int RetryCount { get; set; }
	public DateTimeOffset? LastAttemptAt { get; set; }
	public string? CorrelationId { get; set; }
	public string? TenantId { get; set; }
	public string? Source { get; set; }
}
