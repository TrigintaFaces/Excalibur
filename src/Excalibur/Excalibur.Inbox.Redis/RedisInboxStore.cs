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
/// <para>
/// Keys are formatted as <c>{KeyPrefix}:{tenant}:{messageId}:{handlerType}</c>. The tenant segment is
/// <b>always present</b>: a multi-tenant host composes the resolved tenant identifier, and a host with no
/// resolved tenant composes the framework's reserved untenanted marker, so the key shape is identical in
/// every deployment. The write, dedup, claim, and keyed-read paths are therefore isolated by construction —
/// two tenants carrying the same message id never dedup against each other. The tenant, message id, and
/// handler type are percent-escaped (<c>%</c> becomes <c>%25</c> and <c>:</c> becomes <c>%3A</c>) so a term
/// containing a colon cannot shift the segment boundaries; the key prefix is not escaped, so a
/// <c>{KeyPrefix}:*</c> pattern still matches every key. An external tool that scans, audits, or cleans
/// these keys must parse four segments and unescape the last three.
/// </para>
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
public sealed partial class RedisInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, ILeasedInboxStore, IInboxStoreAdmin, IAsyncDisposable
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
	// expiry is written into the entry JSON (LeaseExpiresAt, unix-ms) server-side via cjson, and that SAME
	// server-resolved value is returned as the caller's ownership term (LeaseToken) — never recomputed in
	// C#. Returns the term as a plain-decimal string (via string.format('%.0f', ...), never tostring(),
	// which can fall back to scientific notation for large magnitudes) on claim/reclaim; false (-> a RESP
	// Nil reply, i.e. RedisResult.IsNull) otherwise.
	// KEYS[1] = entry key. ARGV[1] = new Processing entry JSON; ARGV[2] = lease ms; ARGV[3] = Received
	// status; ARGV[4] = Processing status.
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
		if not claimable then return false end
		local ok2, newdoc = pcall(cjson.decode, ARGV[1])
		if not ok2 or not newdoc then return false end
		local expiresAt = now_ms + lease_ms
		newdoc.LeaseExpiresAt = expiresAt
		newdoc.RetryCount = carried_retry
		redis.call('SET', KEYS[1], cjson.encode(newdoc))
		return string.format('%.0f', expiresAt)
		""";

	// Atomic lease-fenced finalize to Processed. Succeeds ONLY while ARGV[1] (the term returned by
	// TryAcquireLeaseAsync, formatted identically via string.format('%.0f', ...)) still matches the
	// entry's current LeaseExpiresAt. A caller whose lease has lapsed presents a stale term that matches
	// no row, so its finalize takes no effect — this is the fence, not a status predicate: at this
	// instant the entry is legitimately Processing, its successor's. A terminal (already-Processed) or
	// non-lease entry has no LeaseExpiresAt field, so it never matches and this correctly refuses.
	// KEYS[1] = entry key. ARGV[1] = caller's term. ARGV[2] = new serialized Processed document.
	// ARGV[3] = retention TTL seconds (<= 0 => no TTL). Returns 1 = finalized, 0 = stale term / missing.
	private const string LeaseCompleteScript =
		"""
		local v = redis.call('GET', KEYS[1])
		if not v then return 0 end
		local ok, doc = pcall(cjson.decode, v)
		if not ok or not doc then return 0 end
		local exp = doc.LeaseExpiresAt
		if exp == nil then return 0 end
		if string.format('%.0f', tonumber(exp)) ~= ARGV[1] then return 0 end
		redis.call('SET', KEYS[1], ARGV[2])
		local ttl = tonumber(ARGV[3])
		if ttl and ttl > 0 then redis.call('PEXPIRE', KEYS[1], ttl * 1000) end
		return 1
		""";

	// Atomic lease-fenced transition to Failed. Same term fence as LeaseCompleteScript. The written
	// document carries no LeaseExpiresAt field (the caller's ARGV[2] never sets one), so the lease term is
	// cleared as a side effect of the CAS — a failed entry has no holder. Carries the current RetryCount
	// forward incremented by one, since ARGV[2] was built without seeing the stored count.
	// KEYS[1] = entry key. ARGV[1] = caller's term. ARGV[2] = new serialized Failed document.
	// Returns 1 = recorded, 0 = stale term / missing.
	private const string LeaseFailScript =
		"""
		local v = redis.call('GET', KEYS[1])
		if not v then return 0 end
		local ok, doc = pcall(cjson.decode, v)
		if not ok or not doc then return 0 end
		local exp = doc.LeaseExpiresAt
		if exp == nil then return 0 end
		if string.format('%.0f', tonumber(exp)) ~= ARGV[1] then return 0 end
		local ok2, newdoc = pcall(cjson.decode, ARGV[2])
		if not ok2 or not newdoc then return 0 end
		newdoc.RetryCount = (tonumber(doc.RetryCount) or 0) + 1
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

	// Ambient tenant context. The tenant is composed INTO the Redis key (via GetKey) so two tenants'
	// identical (messageId, handlerType) never collide on the dedup key, and stamped on write. TenantScope
	// is total, so there is no "no tenant" case: a host with no resolved tenant composes the reserved
	// untenanted sentinel and the key carries the same four segments as a tenanted one.
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);


	private ConnectionMultiplexer? _connection;
	private IDatabase? _database;
	private bool _ownsConnection;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStore"/> class.
	/// </summary>
	/// <param name="options">The Redis inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public RedisInboxStore(
		IOptions<RedisInboxOptions> options,
		ILogger<RedisInboxStore> logger,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStore"/> class with an existing connection.
	/// </summary>
	/// <param name="connection">An existing Redis connection multiplexer.</param>
	/// <param name="options">The Redis inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public RedisInboxStore(
		ConnectionMultiplexer connection,
		IOptions<RedisInboxOptions> options,
		ILogger<RedisInboxStore> logger,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connection = connection;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_database = connection.GetDatabase(_options.DatabaseId);
	}

	// The tenant to stamp on a written row. The ambient scope always binds a concrete term, so a written
	// row always carries one.
	private string StampTenant()
	{
		var scope = CurrentTenantScope;
		return scope.TenantId;
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
	public async ValueTask<LeaseToken?> TryAcquireLeaseAsync(
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

		var result = await db.ScriptEvaluateAsync(
			LeaseClaimScript,
			[key],
			[
				SerializeEntry(entry),
				(long)leaseDuration.TotalMilliseconds,
				(int)InboxStatus.Received,
				(int)InboxStatus.Processing,
				(int)InboxStatus.Failed
			]).ConfigureAwait(false);

		if (result.IsNull)
		{
			_logger.LogDebug("Lease-claim denied (live lease or processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return null;
		}

		_logger.LogDebug("Lease-claimed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		return new LeaseToken((string)result!);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> CompleteAsync(
		string messageId,
		string handlerType,
		LeaseToken lease,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var db = await GetDatabaseAsync().ConfigureAwait(false);
		var key = GetKey(messageId, handlerType);

		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			Status = InboxStatus.Processed,
			ProcessedAt = DateTimeOffset.UtcNow,
		};

		var result = (long)await db.ScriptEvaluateAsync(
			LeaseCompleteScript,
			[key],
			[
				lease.Value,
				SerializeEntry(entry),
				_options.DefaultTtlSeconds
			]).ConfigureAwait(false);

		var completed = result == 1;

		if (completed)
		{
			_logger.LogDebug("Lease-completed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Lease-complete refused (stale term or missing entry) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return completed;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> FailAsync(
		string messageId,
		string handlerType,
		LeaseToken lease,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var db = await GetDatabaseAsync().ConfigureAwait(false);
		var key = GetKey(messageId, handlerType);

		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			Status = InboxStatus.Failed,
			LastError = errorMessage,
			LastAttemptAt = DateTimeOffset.UtcNow,
		};

		var result = (long)await db.ScriptEvaluateAsync(
			LeaseFailScript,
			[key],
			[
				lease.Value,
				SerializeEntry(entry)
			]).ConfigureAwait(false);

		var recorded = result == 1;

		if (recorded)
		{
			LogFailedEntry(_logger, messageId, handlerType, errorMessage, null);
		}
		else
		{
			_logger.LogDebug("Lease-fail refused (stale term or missing entry) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return recorded;
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
		// re-admittable without consuming a delivery attempt.
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
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var entries = new List<InboxEntry>();
		var pattern = $"{_options.KeyPrefix}:*";

		// The drain's re-admission predicate. A Failed entry is retryable, and so is a Processing entry
		// whose lease has run out: that is a processor that died holding the entry, and admitting it is the
		// only way it ever reaches a terminal state. Without the expired-lease arm, leasing the drain would
		// move an entry to Processing and never select it again -- trading a duplicate dispatch for a
		// permanently stranded entry, which is the worse failure.
		//
		// This comparison uses the app clock while the lease CAS uses the SERVER clock, and that asymmetry
		// is safe BECAUSE this read is not the fence. A skewed reader can only offer an entry early or late;
		// the Lua compare-and-set still refuses it while the lease is genuinely live, so a duplicate
		// dispatch remains impossible. Only the fence needs the single skew-free clock.
		var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		foreach (var (entry, leaseExpiresAt) in await ReadAllDocumentsAsync(db, pattern, cancellationToken).ConfigureAwait(false))
		{
			// A null lease means a claim taken through the lease-less overload, which is held until its own
			// caller finalises or releases it. Absent is "no expiry", never "expired".
			var retryEligible = entry.Status == InboxStatus.Failed
				|| (entry.Status == InboxStatus.Processing && leaseExpiresAt is { } expiry && expiry < nowUnixMs);

			if (retryEligible && entry.RetryCount < maxRetries)
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
		var documents = await ReadAllDocumentsAsync(db, pattern, cancellationToken).ConfigureAwait(false);
		var mapped = new List<InboxEntry>(documents.Count);

		foreach (var (entry, _) in documents)
		{
			mapped.Add(entry);
		}

		return mapped;
	}

	/// <summary>
	/// The same scan as <see cref="ReadAllEntriesAsync"/>, but keeping each entry's lease term alongside it.
	/// </summary>
	/// <remarks>
	/// <see cref="InboxEntry"/> carries no lease term, so a caller that must distinguish a live claim from a
	/// dead processor's expired one cannot do it from the mapped entry alone. Only the retry drain's read
	/// needs that; every other caller uses the mapped overload above.
	/// </remarks>
	private async Task<List<(InboxEntry Entry, long? LeaseExpiresAt)>> ReadAllDocumentsAsync(
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
			return [];
		}

		// Single MGET round-trip for all scanned keys.
		var values = await db.StringGetAsync(keys.ToArray()).ConfigureAwait(false);

		var entries = new List<(InboxEntry Entry, long? LeaseExpiresAt)>(values.Length);
		foreach (var value in values)
		{
			if (!value.IsNullOrEmpty)
			{
				entries.Add(DeserializeDocument(value!));
			}
		}

		return entries;
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsEntriesAsync(CancellationToken cancellationToken)
	{
		var db = await GetDatabaseAsync().ConfigureAwait(false);

		var pattern = $"{_options.KeyPrefix}:*";
		return await ReadAllEntriesAsync(db, pattern, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
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
	public async ValueTask<int> CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
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
			TenantId = StampTenant(),
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
	private static InboxEntry DeserializeEntry(string json) => DeserializeDocument(json).Entry;

	/// <summary>
	/// Deserializes an entry together with its lease term, which <see cref="InboxEntry"/> does not carry.
	/// </summary>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Inbox entries include dynamic metadata that requires runtime deserialization.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Inbox entries include dynamic metadata that requires runtime deserialization.")]
	private static (InboxEntry Entry, long? LeaseExpiresAt) DeserializeDocument(string json)
	{
		var document = JsonSerializer.Deserialize<RedisInboxDocument>(json)
					   ?? throw new InvalidOperationException("Failed to deserialize inbox entry.");

		var metadata = document.Metadata ?? new Dictionary<string, object>(StringComparer.Ordinal);

		var entry = new InboxEntry
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

		return (entry, document.LeaseExpiresAt);
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

	// Composes the tenant INTO the Redis key, so the dedup/claim key — and thus every keyed read/write — is
	// tenant-isolated by construction. An untenanted deployment composes the reserved sentinel, so the key
	// shape is the same in every deployment and a single-tenant host cannot collide with a tenanted one.
	private string GetKey(string messageId, string handlerType)
	{
		var scope = CurrentTenantScope;
		return $"{_options.KeyPrefix}:{EscapeSegment(scope.TenantId)}:{EscapeSegment(messageId)}:{EscapeSegment(handlerType)}";
	}

	// The ':' joining the terms is not injective on its own. Neither the tenant term nor the message id is
	// validated against any charset -- both are caller data -- so tenant "a:b" with message "c" and tenant
	// "a" with message "b:c" both composed the same Redis key and shared one entry. This is the dedup key,
	// so the collision does not surface as an error: the second message reads as already-processed and is
	// dropped, silently, across a tenant boundary.
	//
	// '%' is escaped FIRST and is what makes the encoding reversible. Escaping only ':' would map the
	// distinct terms "a:b" and "a%3Ab" onto one key -- a collision introduced by the escaping itself.
	//
	// The KeyPrefix is deliberately NOT escaped. It is configuration rather than caller data, it is
	// constant within a deployment so it cannot vary a key across messages, and the maintenance scans
	// match on "{KeyPrefix}:*" -- escaping it would break that pattern for a prefix containing a colon.
	//
	// This is deliberately a no-op for any term containing neither '%' nor ':'. The key is PERSISTED, so an
	// encoding that moved every existing key would orphan every in-flight dedup record on upgrade and
	// re-deliver already-processed messages. Only the previously-ambiguous keys change.
	private static string EscapeSegment(string value) =>
		value.Replace("%", "%25", StringComparison.Ordinal)
			.Replace(":", "%3A", StringComparison.Ordinal);

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

	// Unix-ms lease expiry, written SERVER-side by the lease Lua scripts and read back here so the retry
	// drain can tell a live claim from a dead processor's. Deserialize-only in practice: every C# write
	// builds this document from an InboxEntry, which carries no lease term, so this stays null on those
	// paths and they continue to clear the lease exactly as before. The lease is owned by the Lua scripts,
	// which are the only writers that may set it.
	public long? LeaseExpiresAt { get; set; }
}
