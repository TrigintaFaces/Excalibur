// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Excalibur.Data.Redis.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Metadata;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.Outbox.Redis;

/// <summary>
/// Redis-based implementation of <see cref="IOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses Redis sorted sets for priority-based message retrieval and Lua scripts
/// for atomic status transitions (critical for MarkSentAsync to prevent race conditions).
/// </para>
/// <para>
/// Key structure:
/// - {prefix}:msg:{id} - Hash containing message data
/// - {prefix}:idx:staged - Sorted set of staged messages (score = priority * 1e12 + timestamp)
/// - {prefix}:idx:failed - Sorted set of failed messages (score = retryCount * 1e12 + timestamp)
/// - {prefix}:idx:scheduled - Sorted set of scheduled messages (score = scheduledAt timestamp)
/// - {prefix}:idx:sent - Sorted set of sent messages (score = sentAt timestamp)
/// - {prefix}:idx:leased - Sorted set of claimed (in-flight) messages (score = lease expiry timestamp)
/// </para>
/// <para>
/// Concurrent delivery is made safe by an atomic Lua <em>lease-claim</em> (see
/// <see cref="GetUnsentMessagesAsync"/>): claiming a message moves its id from the staged index to the
/// leased index in a single indivisible step, so two concurrent pollers can never claim the same message
/// (disjoint claim). A leased message stays hidden until it reaches a terminal state — which clears the
/// lease — or its lease expires, at which point it is reclaimed back to the staged index (so a poller
/// that crashes mid-delivery cannot strand a message). This mirrors the SQL Server outbox's
/// <c>LeasedAt</c>/<c>LeasedBy</c> claim.
/// </para>
/// </remarks>
public sealed partial class RedisOutboxStore : IOutboxStore, IOutboxStoreAdmin, IDeadLetterableOutboxStore, IBackoffSchedulableOutboxStore, IAsyncDisposable
{
	// Lua script for atomic MarkSent - checks status before updating
	// Returns plain strings (not {err=...}) to avoid RedisServerException
	private const string MarkSentLuaScript = """
	                                         local key = KEYS[1]
	                                         local stagedIdx = KEYS[2]
	                                         local sentIdx = KEYS[3]
	                                         local scheduledIdx = KEYS[4]
	                                         local leasedIdx = KEYS[5]
	                                         local failedIdx = KEYS[6]
	                                         local messageId = ARGV[1]
	                                         local sentAt = ARGV[2]
	                                         local sentStatus = ARGV[3]
	                                         local ttlSeconds = tonumber(ARGV[4])

	                                         -- Check if message exists
	                                         local exists = redis.call('EXISTS', key)
	                                         if exists == 0 then
	                                         	return 'NOT_FOUND'
	                                         end

	                                         -- Check current status atomically
	                                         local currentStatus = redis.call('HGET', key, 'Status')
	                                         if currentStatus == sentStatus then
	                                         	return 'ALREADY_SENT'
	                                         end

	                                         -- Update the message
	                                         redis.call('HMSET', key, 'Status', sentStatus, 'SentAt', sentAt)

	                                         -- Apply the retention TTL in the SAME atomic script as the status
	                                         -- write, so a crash can never leave a Sent message with no expiry.
	                                         if ttlSeconds and ttlSeconds > 0 then
	                                         	redis.call('EXPIRE', key, ttlSeconds)
	                                         end

	                                         -- Remove from every claimable/in-flight index (message could be in any), including the failed
	                                         -- index so a reclaimed-then-sent message no longer appears in GetFailedMessages.
	                                         redis.call('ZREM', stagedIdx, messageId)
	                                         redis.call('ZREM', scheduledIdx, messageId)
	                                         redis.call('ZREM', leasedIdx, messageId)
	                                         redis.call('ZREM', failedIdx, messageId)
	                                         redis.call('ZADD', sentIdx, sentAt, messageId)

	                                         return 'SUCCESS'
	                                         """;

	// Lua script for atomic MarkDeadLettered - transitions to terminal DeadLettered status
	// Removes the id from every claimable index (staged + failed) so the poller can never re-claim it.
	private const string MarkDeadLetteredLuaScript = """
	                                                 local key = KEYS[1]
	                                                 local stagedIdx = KEYS[2]
	                                                 local failedIdx = KEYS[3]
	                                                 local leasedIdx = KEYS[4]
	                                                 local messageId = ARGV[1]
	                                                 local reason = ARGV[2]
	                                                 local deadLetteredStatus = ARGV[3]

	                                                 -- Silent no-op when message does not exist
	                                                 local exists = redis.call('EXISTS', key)
	                                                 if exists == 0 then
	                                                 	return {ok = 'NOT_FOUND'}
	                                                 end

	                                                 -- Update the message hash to terminal status
	                                                 redis.call('HMSET', key,
	                                                 	'Status', deadLetteredStatus,
	                                                 	'LastError', reason)

	                                                 -- Remove from every claimable/in-flight index so the poller can never re-claim this id
	                                                 redis.call('ZREM', stagedIdx, messageId)
	                                                 redis.call('ZREM', failedIdx, messageId)
	                                                 redis.call('ZREM', leasedIdx, messageId)

	                                                 return {ok = 'SUCCESS'}
	                                                 """;

	// Lua script for atomic MarkFailed - updates status and retry count
	private const string MarkFailedLuaScript = """
	                                           local key = KEYS[1]
	                                           local stagedIdx = KEYS[2]
	                                           local failedIdx = KEYS[3]
	                                           local leasedIdx = KEYS[4]
	                                           local scheduledIdx = KEYS[5]
	                                           local messageId = ARGV[1]
	                                           local errorMessage = ARGV[2]
	                                           local retryCount = ARGV[3]
	                                           local lastAttemptAt = ARGV[4]
	                                           local failedStatus = ARGV[5]
	                                           local leasedBy = ARGV[6]
	                                           local nextAttemptAt = ARGV[7]

	                                           local exists = redis.call('EXISTS', key)
	                                           if exists == 0 then
	                                           	return {ok = 'NOT_FOUND'}
	                                           end

	                                           -- R2 dispatcher-ownership guard IN the atomic write: only the current lease owner (or an
	                                           -- already-released row) may mark-fail, so a stale processor cannot overwrite a peer's re-claim.
	                                           local currentLease = redis.call('HGET', key, 'LeasedBy')
	                                           if currentLease and currentLease ~= false and currentLease ~= '' and currentLease ~= leasedBy then
	                                           	return {ok = 'NOT_OWNER'}
	                                           end

	                                           -- R3 monotonic: never lower RetryCount (a stale late writer must not weaken the DLQ ceiling).
	                                           local newRetry = tonumber(retryCount)
	                                           local currentRetry = tonumber(redis.call('HGET', key, 'RetryCount')) or 0
	                                           if currentRetry > newRetry then newRetry = currentRetry end

	                                           redis.call('HMSET', key,
	                                           	'Status', failedStatus,
	                                           	'LastError', errorMessage,
	                                           	'RetryCount', newRetry,
	                                           	'LastAttemptAt', lastAttemptAt,
	                                           	'NextAttemptAt', nextAttemptAt)

	                                           -- Free the lease (single-write parity with SqlServer/Mongo) + drop the in-flight index entries.
	                                           redis.call('HDEL', key, 'LeasedAt', 'LeasedBy')
	                                           redis.call('ZREM', stagedIdx, messageId)
	                                           redis.call('ZREM', leasedIdx, messageId)

	                                           -- R1 floor: re-queue into scheduledIdx scored by NextAttemptAt (now+F) so MoveScheduledToStaged
	                                           -- re-surfaces it only after the floor (at-least-once; no zero-backoff hot-loop).
	                                           redis.call('ZADD', scheduledIdx, tonumber(nextAttemptAt), messageId)

	                                           -- Keep it in the failed index for GetFailedMessages reporting (retry-count-ordered).
	                                           local score = newRetry * 1000000000000 + tonumber(lastAttemptAt)
	                                           redis.call('ZADD', failedIdx, score, messageId)

	                                           return {ok = 'SUCCESS'}
	                                           """;

	// Lua script for atomic MarkFailedWithBackoff (mnq685) - records the failure but re-queues the message
	// into the SCHEDULED index scored by nextAttemptAt, which is Redis's native "do not surface before this
	// time" gate (MoveScheduledToStaged only promotes scheduled ids whose score <= now). So the computed
	// exponential backoff genuinely throttles re-delivery rather than the coarse lease cadence. The message
	// stays re-claimable (Staged) but invisible to the claim until nextAttemptAt elapses.
	// KEYS: [messageKey, stagedIdx, scheduledIdx, leasedIdx]; ARGV: [messageId, error, retryCount,
	// lastAttemptAt, nextAttemptAtMs, stagedStatus, leasedBy]. Carries the R2 ownership guard + R3 monotonic
	// RetryCount (wseau9), same as the plain MarkFailed path.
	private const string MarkFailedWithBackoffLuaScript = """
	                                                       local key = KEYS[1]
	                                                       local stagedIdx = KEYS[2]
	                                                       local scheduledIdx = KEYS[3]
	                                                       local leasedIdx = KEYS[4]
	                                                       local messageId = ARGV[1]
	                                                       local errorMessage = ARGV[2]
	                                                       local retryCount = ARGV[3]
	                                                       local lastAttemptAt = ARGV[4]
	                                                       local nextAttemptAt = ARGV[5]
	                                                       local stagedStatus = ARGV[6]
	                                                       local leasedBy = ARGV[7]

	                                                       local exists = redis.call('EXISTS', key)
	                                                       if exists == 0 then
	                                                       	return {ok = 'NOT_FOUND'}
	                                                       end

	                                                       -- R2 ownership guard IN the atomic write (parity with MarkFailed).
	                                                       local currentLease = redis.call('HGET', key, 'LeasedBy')
	                                                       if currentLease and currentLease ~= false and currentLease ~= '' and currentLease ~= leasedBy then
	                                                       	return {ok = 'NOT_OWNER'}
	                                                       end

	                                                       -- R3 monotonic RetryCount.
	                                                       local newRetry = tonumber(retryCount)
	                                                       local currentRetry = tonumber(redis.call('HGET', key, 'RetryCount')) or 0
	                                                       if currentRetry > newRetry then newRetry = currentRetry end

	                                                       redis.call('HMSET', key,
	                                                       	'Status', stagedStatus,
	                                                       	'LastError', errorMessage,
	                                                       	'RetryCount', newRetry,
	                                                       	'LastAttemptAt', lastAttemptAt,
	                                                       	'NextAttemptAt', nextAttemptAt)

	                                                       -- Free the lease + re-queue into the scheduled index gated by nextAttemptAt (stays re-claimable, Staged).
	                                                       redis.call('HDEL', key, 'LeasedAt', 'LeasedBy')
	                                                       redis.call('ZREM', stagedIdx, messageId)
	                                                       redis.call('ZREM', leasedIdx, messageId)
	                                                       redis.call('ZADD', scheduledIdx, tonumber(nextAttemptAt), messageId)

	                                                       return {ok = 'SUCCESS'}
	                                                       """;

	// Lua script for atomic StageMessage (bd-5jo6tm) - dedup-claim + full-hash write + index add
	// in a SINGLE indivisible step. Redis executes the whole script atomically, so a crash can
	// never leave an orphan partial hash (a claimed id with no payload and absent from every
	// claimable index, which would be invisible to the poller AND block re-staging the id).
	// KEYS[1]=message key, KEYS[2]=target index (staged | scheduled).
	// ARGV[1]=messageId, ARGV[2]=index score, ARGV[3..]=flattened hash field/value pairs.
	private const string StageMessageLuaScript = """
	                                             local key = KEYS[1]
	                                             local indexKey = KEYS[2]
	                                             local messageId = ARGV[1]
	                                             local score = ARGV[2]

	                                             -- Atomic dedup: claim by whole-key existence. Exactly ONE concurrent stager wins.
	                                             if redis.call('EXISTS', key) == 1 then
	                                             	return 'ALREADY_EXISTS'
	                                             end

	                                             -- Write ALL message fields and add to the claimable index in one atomic step.
	                                             redis.call('HSET', key, unpack(ARGV, 3))
	                                             redis.call('ZADD', indexKey, score, messageId)

	                                             return 'SUCCESS'
	                                             """;

	// Lua script for atomic lease-claim (bd-5gtfje) — disjoint claim + crash-reclaim in ONE indivisible step.
	// Step 1 reclaims every expired lease (lease-expiry score <= now) back to the staged index, recomputing
	// the original staged score from the message's Priority + CreatedAt and clearing its lease fields, so a
	// poller that crashed mid-delivery cannot strand a message (no data loss, FR-J1.2/J1.5).
	// Step 2 claims up to batchSize staged ids by MOVING each from the staged index to the leased index
	// (ZREM staged -> ZADD leased with the new lease expiry) and stamping LeasedAt/LeasedBy. Because the
	// whole script runs atomically, two concurrent pollers can never both claim the same id — the loser's
	// ZRANGE no longer contains it (disjoint claim, FR-J1.1/J1.4). Orphan index entries (an id in staged
	// with no message hash) are pruned, never leased.
	// KEYS[1]=staged index, KEYS[2]=leased index.
	// ARGV[1]=now(ms), ARGV[2]=batchSize, ARGV[3]=leaseExpiry(ms), ARGV[4]=leasedBy, ARGV[5]=message-key prefix.
	private const string ClaimMessagesLuaScript = """
		local stagedIdx = KEYS[1]
		local leasedIdx = KEYS[2]
		local now = tonumber(ARGV[1])
		local batchSize = tonumber(ARGV[2])
		local leaseExpiry = tonumber(ARGV[3])
		local leasedBy = ARGV[4]
		local msgPrefix = ARGV[5]

		-- Step 1: reclaim expired leases back to the staged index.
		local expired = redis.call('ZRANGEBYSCORE', leasedIdx, '-inf', now)
		for i = 1, #expired do
			local id = expired[i]
			local mkey = msgPrefix .. id
			if redis.call('EXISTS', mkey) == 1 then
				local priority = tonumber(redis.call('HGET', mkey, 'Priority')) or 0
				local createdAt = tonumber(redis.call('HGET', mkey, 'CreatedAt')) or 0
				local score = priority * 1000000000000 + createdAt
				redis.call('ZADD', stagedIdx, score, id)
				redis.call('HDEL', mkey, 'LeasedAt', 'LeasedBy')
			end
			redis.call('ZREM', leasedIdx, id)
		end

		-- Step 2: claim up to batchSize staged ids (lowest score first), moving each to the leased index.
		local claimable = redis.call('ZRANGE', stagedIdx, 0, batchSize - 1)
		local claimed = {}
		for i = 1, #claimable do
			local id = claimable[i]
			local mkey = msgPrefix .. id
			redis.call('ZREM', stagedIdx, id)
			if redis.call('EXISTS', mkey) == 1 then
				redis.call('ZADD', leasedIdx, leaseExpiry, id)
				redis.call('HSET', mkey, 'LeasedAt', now, 'LeasedBy', leasedBy)
				claimed[#claimed + 1] = id
			end
		end

		return claimed
		""";

	private static readonly CompositeFormat MessageAlreadyExistsFormat =
		CompositeFormat.Parse("Message with ID '{0}' already exists in the outbox.");

	private static readonly CompositeFormat MessageNotFoundFormat =
		CompositeFormat.Parse("Message with ID '{0}' not found.");

	private static readonly CompositeFormat MessageAlreadySentFormat =
		CompositeFormat.Parse("Message with ID '{0}' is already marked as sent.");

	private readonly RedisOutboxOptions _options;
	private readonly ILogger<RedisOutboxStore> _logger;
	private readonly TimeProvider _timeProvider;
	private ConnectionMultiplexer? _connection;
	private bool _ownsConnection;
	private IDatabase? _database;
	private string? _resolvedProcessorId;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The Redis outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">The time provider used for backoff/claim-gate decisions. Defaults to <see cref="TimeProvider.System"/>.</param>
	public RedisOutboxStore(
		IOptions<RedisOutboxOptions> options,
		ILogger<RedisOutboxStore> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisOutboxStore"/> class with an existing connection.
	/// </summary>
	/// <param name="connection">An existing Redis connection multiplexer.</param>
	/// <param name="options">The Redis outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">The time provider used for backoff/claim-gate decisions. Defaults to <see cref="TimeProvider.System"/>.</param>
	public RedisOutboxStore(
		ConnectionMultiplexer connection,
		IOptions<RedisOutboxOptions> options,
		ILogger<RedisOutboxStore> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connection = connection;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_database = connection.GetDatabase(_options.DatabaseId);
	}

	/// <inheritdoc/>
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetMessageKey(message.Id);

		// bd-5jo6tm: stage the message ATOMICALLY — dedup-claim + full-hash write + index add happen
		// in a single Lua script, so a crash can never leave an orphan partial hash. The prior path was
		// three separate round-trips (HSETNX claim -> HashSet remaining fields -> SortedSetAdd to index):
		// a crash between the claim and the field-write left a hash holding only the claimed MessageType,
		// with no payload and absent from every claimable index — invisible to the poller AND blocking
		// re-staging the id (the message was silently lost). Folding all three into one indivisible script
		// makes that partial state structurally inexpressible. The bd-grjjz0 atomic-dedup guarantee is
		// preserved as the whole-key EXISTS guard inside the script (exactly one concurrent stager wins).
		RedisKey indexKey;
		long score;
		if (message.ScheduledAt.HasValue)
		{
			// Scheduled messages always go to the scheduled index (even if scheduled in the past);
			// GetUnsentMessagesAsync moves due scheduled messages to the staged index.
			indexKey = GetScheduledIndexKey();
			score = message.ScheduledAt.Value.ToUnixTimeMilliseconds();
		}
		else
		{
			// Score: priority (inverted, lower = higher priority) + creation timestamp for ordering.
			indexKey = GetStagedIndexKey();
			score = (message.Priority * 1_000_000_000_000L) + message.CreatedAt.ToUnixTimeMilliseconds();
		}

		var entries = SerializeToHashEntries(message);
		var argv = new RedisValue[2 + (entries.Length * 2)];
		argv[0] = message.Id;
		argv[1] = score;
		var argvIndex = 2;
		foreach (var entry in entries)
		{
			argv[argvIndex++] = entry.Name;
			argv[argvIndex++] = entry.Value;
		}

		var result = await _database!.ScriptEvaluateAsync(
			StageMessageLuaScript,
			[key, indexKey],
			argv).ConfigureAwait(false);

		if (string.Equals(result.ToString(), "ALREADY_EXISTS", StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					MessageAlreadyExistsFormat,
					message.Id));
		}

		LogMessageStaged(message.Id, message.MessageType, message.Destination);
	}

	/// <inheritdoc/>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox payloads use runtime serialization for message types.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox payloads use runtime serialization for message types.")]
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var messageType = message.GetType().FullName ?? message.GetType().Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), EventSerializationDefaults.Canonical);

		// xnyhjd: honor a consumer-set routing destination (TransactionalOutboxWriter.SetDestination →
		// context) rather than persisting the message type name as the destination — parity with the
		// SQL/Postgres outbox stores. Falls back to the type name when no destination was set.
		var destination = context.ExtractMetadata().GetDestination() ?? message.GetType().Name;
		var outbound = OutboundMessage.FromContext(messageType, payload, destination, context);

		await StageMessageAsync(outbound, cancellationToken).ConfigureAwait(false);

		LogMessageEnqueued(outbound.Id, messageType);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

		// First, move any scheduled messages that are now due to the staged index
		await MoveScheduledToStagedAsync(now).ConfigureAwait(false);

		// Atomically lease-claim up to batchSize staged messages: the script reclaims any expired leases,
		// then moves the claimed ids from the staged index to the leased index (with a fresh lease expiry)
		// in a single indivisible step. The ZREM-from-staged inside the atomic script is what makes the
		// claim disjoint — a second poller running concurrently can never observe the same ids.
		var leaseExpiry = now + (_options.LeaseTimeoutSeconds * 1000L);
		var claimResult = await _database!.ScriptEvaluateAsync(
			ClaimMessagesLuaScript,
			[GetStagedIndexKey(), GetLeasedIndexKey()],
			[now, batchSize, leaseExpiry, GetProcessorId(), GetMessageKeyPrefix()]).ConfigureAwait(false);

		if (claimResult.IsNull)
		{
			return [];
		}

		var claimedIds = (RedisValue[])claimResult!;
		return await GetMessagesByIdsAsync(claimedIds, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Batch-loads the message hashes for a set of ids in a single pipelined round-trip. The per-id
	/// <c>HASHGETALL</c> commands are issued concurrently so StackExchange.Redis multiplexes them onto
	/// the connection (IBatch semantics) instead of paying one network RTT per id.
	/// </summary>
	private async Task<List<OutboundMessage>> GetMessagesByIdsAsync(
		IReadOnlyList<RedisValue> ids,
		CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return [];
		}

		var tasks = new Task<HashEntry[]>[ids.Count];
		for (var i = 0; i < ids.Count; i++)
		{
			tasks[i] = _database!.HashGetAllAsync(GetMessageKey(ids[i]!));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		var messages = new List<OutboundMessage>(ids.Count);
		for (var i = 0; i < results.Length; i++)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			var entries = results[i];
			if (entries.Length == 0)
			{
				continue;
			}

			messages.Add(DeserializeFromHashEntries(ids[i]!, entries));
		}

		return messages;
	}

	/// <inheritdoc/>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetMessageKey(messageId);
		var sentAt = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds().ToString();

		// Use Lua script for atomic check-and-update
		var result = await _database!.ScriptEvaluateAsync(
			MarkSentLuaScript,
			[key, GetStagedIndexKey(), GetSentIndexKey(), GetScheduledIndexKey(), GetLeasedIndexKey(), GetFailedIndexKey()],
			[messageId, sentAt, ((int)OutboxStatus.Sent).ToString(),
				_options.SentMessageTtlSeconds.ToString(CultureInfo.InvariantCulture)]).ConfigureAwait(false);

		var resultStr = result.ToString();
		if (resultStr == "NOT_FOUND")
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					MessageNotFoundFormat,
					messageId));
		}

		if (resultStr == "ALREADY_SENT")
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					MessageAlreadySentFormat,
					messageId));
		}

		// Retention TTL is applied atomically inside MarkSentLuaScript (above) — no separate
		// KeyExpire call, so a crash can never leave a Sent message as an immortal key.
		LogMessageSent(messageId);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetMessageKey(messageId);
		var lastAttemptAt = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds().ToString();
		var nextAttemptAtMs = _timeProvider.GetUtcNow().AddSeconds(_options.FailureBackoffFloorSeconds)
			.ToUnixTimeMilliseconds().ToString();

		// One atomic Lua write (SA #1/#2): R2 dispatcher-ownership guard IN the script, R1 floor (re-queue to
		// the scheduled index at now+F so MoveScheduledToStaged re-surfaces it only after the floor — at-least-once,
		// no hot-loop), R3 monotonic RetryCount, free-lease. A missing key is a silent NOT_FOUND no-op inside the
		// script — no separate exists round-trip (no read→check→write TOCTOU).
		_ = await _database!.ScriptEvaluateAsync(
			MarkFailedLuaScript,
			[key, GetStagedIndexKey(), GetFailedIndexKey(), GetLeasedIndexKey(), GetScheduledIndexKey()],
			[messageId, errorMessage, retryCount.ToString(), lastAttemptAt, ((int)OutboxStatus.Failed).ToString(),
				GetProcessorId(), nextAttemptAtMs]).ConfigureAwait(false);

		LogMessageFailed(messageId, errorMessage, retryCount);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedWithBackoffAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ArgumentOutOfRangeException.ThrowIfNegative(retryCount);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetMessageKey(messageId);

		// Silent return if the message no longer exists (conformance parity with MarkFailedAsync).
		var exists = await _database!.KeyExistsAsync(key).ConfigureAwait(false);
		if (!exists)
		{
			return;
		}

		var lastAttemptAt = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds().ToString();
		var nextAttemptAtMs = nextAttemptAt.ToUnixTimeMilliseconds().ToString();

		_ = await _database!.ScriptEvaluateAsync(
			MarkFailedWithBackoffLuaScript,
			[key, GetStagedIndexKey(), GetScheduledIndexKey(), GetLeasedIndexKey()],
			[messageId, errorMessage, retryCount.ToString(), lastAttemptAt, nextAttemptAtMs, ((int)OutboxStatus.Staged).ToString(), GetProcessorId()]).ConfigureAwait(false);

		LogMessageFailed(messageId, errorMessage, retryCount);
	}

	/// <inheritdoc/>
	public async ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetMessageKey(messageId);

		// Check if exists first - silent return when message does not exist
		var exists = await _database!.KeyExistsAsync(key).ConfigureAwait(false);
		if (!exists)
		{
			return;
		}

		// Use Lua script for atomic update: set terminal status + remove from every claimable index
		_ = await _database!.ScriptEvaluateAsync(
			MarkDeadLetteredLuaScript,
			[key, GetStagedIndexKey(), GetFailedIndexKey(), GetLeasedIndexKey()],
			[messageId, reason, ((int)OutboxStatus.DeadLettered).ToString()]).ConfigureAwait(false);

		_logger.LogWarning(
			"Marked message {MessageId} as dead-lettered: {Reason}",
			messageId,
			reason);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		// Get all failed message IDs
		var messageIds = await _database!.SortedSetRangeByRankAsync(
			GetFailedIndexKey(),
			0,
			-1).ConfigureAwait(false);

		var messages = new List<OutboundMessage>();

		foreach (var id in messageIds)
		{
			if (cancellationToken.IsCancellationRequested || messages.Count >= batchSize)
			{
				break;
			}

			var message = await GetMessageByIdAsync(id!).ConfigureAwait(false);
			if (message == null || message.Status != OutboxStatus.Failed)
			{
				continue;
			}

			if (maxRetries > 0 && message.RetryCount >= maxRetries)
			{
				continue;
			}

			if (olderThan.HasValue && message.LastAttemptAt >= olderThan)
			{
				continue;
			}

			messages.Add(message);
		}

		return messages;
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var maxScore = scheduledBefore.ToUnixTimeMilliseconds();

		var messageIds = await _database!.SortedSetRangeByScoreAsync(
			GetScheduledIndexKey(),
			double.NegativeInfinity,
			maxScore,
			take: batchSize).ConfigureAwait(false);

		var messages = new List<OutboundMessage>();

		foreach (var id in messageIds)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			var message = await GetMessageByIdAsync(id!).ConfigureAwait(false);
			if (message != null)
			{
				messages.Add(message);
			}
		}

		return messages;
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAllTenantsSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var maxScore = olderThan.ToUnixTimeMilliseconds();

		// Get sent messages older than the cutoff
		var messageIds = await _database!.SortedSetRangeByScoreAsync(
			GetSentIndexKey(),
			double.NegativeInfinity,
			maxScore,
			take: batchSize).ConfigureAwait(false);

		var count = 0;

		foreach (var id in messageIds)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			var key = GetMessageKey(id!);
			if (await _database!.KeyDeleteAsync(key).ConfigureAwait(false))
			{
				_ = await _database!.SortedSetRemoveAsync(GetSentIndexKey(), id).ConfigureAwait(false);
				count++;
			}
		}

		LogMessagesCleanedUp(count, olderThan);

		return count;
	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var now = _timeProvider.GetUtcNow();

		var stagedCount = (int)await _database!.SortedSetLengthAsync(GetStagedIndexKey()).ConfigureAwait(false);
		var sentCount = (int)await _database!.SortedSetLengthAsync(GetSentIndexKey()).ConfigureAwait(false);
		var failedCount = (int)await _database!.SortedSetLengthAsync(GetFailedIndexKey()).ConfigureAwait(false);
		var scheduledCount = (int)await _database!.SortedSetLengthAsync(GetScheduledIndexKey()).ConfigureAwait(false);

		// Leased (claimed, non-terminal) messages are the in-flight "sending" set — parity with the
		// SQL Server outbox's b64hci fix (SendingMessageCount = leased-but-not-terminal rows).
		var sendingCount = (int)await _database!.SortedSetLengthAsync(GetLeasedIndexKey()).ConfigureAwait(false);

		// Get oldest unsent (first in staged sorted set)
		TimeSpan? oldestUnsentAge = null;
		var oldestStaged = await _database!.SortedSetRangeByRankAsync(GetStagedIndexKey(), 0, 0).ConfigureAwait(false);
		if (oldestStaged.Length > 0)
		{
			var message = await GetMessageByIdAsync(oldestStaged[0]!).ConfigureAwait(false);
			if (message != null)
			{
				oldestUnsentAge = now - message.CreatedAt;
			}
		}

		// Get oldest failed
		TimeSpan? oldestFailedAge = null;
		var oldestFailed = await _database!.SortedSetRangeByRankAsync(GetFailedIndexKey(), 0, 0).ConfigureAwait(false);
		if (oldestFailed.Length > 0)
		{
			var message = await GetMessageByIdAsync(oldestFailed[0]!).ConfigureAwait(false);
			if (message != null)
			{
				oldestFailedAge = now - message.CreatedAt;
			}
		}

		return new OutboxStatistics
		{
			StagedMessageCount = stagedCount,
			SendingMessageCount = sendingCount,
			SentMessageCount = sentCount,
			FailedMessageCount = failedCount,
			ScheduledMessageCount = scheduledCount,
			OldestUnsentMessageAge = oldestUnsentAge,
			OldestFailedMessageAge = oldestFailedAge,
			CapturedAt = now
		};
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
		Justification = "Outbox headers are serialized from dynamic payloads.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox headers are serialized from dynamic payloads.")]
	private static HashEntry[] SerializeToHashEntries(OutboundMessage message)
	{
		var entries = new List<HashEntry>
		{
			new("MessageType", message.MessageType),
			new("Payload", message.Payload),
			new("Destination", message.Destination),
			new("CreatedAt", message.CreatedAt.ToUnixTimeMilliseconds()),
			new("Status", (int)message.Status),
			new("Priority", message.Priority),
			new("RetryCount", message.RetryCount)
		};

		if (!string.IsNullOrEmpty(message.CorrelationId))
		{
			entries.Add(new HashEntry("CorrelationId", message.CorrelationId));
		}

		if (!string.IsNullOrEmpty(message.CausationId))
		{
			entries.Add(new HashEntry("CausationId", message.CausationId));
		}

		if (!string.IsNullOrEmpty(message.TenantId))
		{
			entries.Add(new HashEntry("TenantId", message.TenantId));
		}

		// Consumer-supplied routing fields — persisted so they round-trip on reload (a dropped routing field
		// is silent consumer-data loss).
		if (!string.IsNullOrEmpty(message.PartitionKey))
		{
			entries.Add(new HashEntry("PartitionKey", message.PartitionKey));
		}

		if (!string.IsNullOrEmpty(message.GroupKey))
		{
			entries.Add(new HashEntry("GroupKey", message.GroupKey));
		}

		if (!string.IsNullOrEmpty(message.TargetTransports))
		{
			entries.Add(new HashEntry("TargetTransports", message.TargetTransports));
		}

		// Value type — always persist (0/1) so IsMultiTransport round-trips exactly, including false.
		entries.Add(new HashEntry("IsMultiTransport", message.IsMultiTransport ? 1 : 0));

		if (!string.IsNullOrEmpty(message.LastError))
		{
			entries.Add(new HashEntry("LastError", message.LastError));
		}

		if (message.ScheduledAt.HasValue)
		{
			entries.Add(new HashEntry("ScheduledAt", message.ScheduledAt.Value.ToUnixTimeMilliseconds()));
		}

		if (message.SentAt.HasValue)
		{
			entries.Add(new HashEntry("SentAt", message.SentAt.Value.ToUnixTimeMilliseconds()));
		}

		if (message.LastAttemptAt.HasValue)
		{
			entries.Add(new HashEntry("LastAttemptAt", message.LastAttemptAt.Value.ToUnixTimeMilliseconds()));
		}

		if (message.Headers.Count > 0)
		{
			entries.Add(new HashEntry("Headers", JsonSerializer.Serialize(message.Headers, EventSerializationDefaults.Canonical)));
		}

		return [.. entries];
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox headers are deserialized to dynamic payloads.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox headers are deserialized to dynamic payloads.")]
	private static OutboundMessage DeserializeFromHashEntries(string messageId, HashEntry[] entries)
	{
		var dict = entries.ToDictionary(
			e => e.Name.ToString(),
			e => e.Value,
			StringComparer.Ordinal);

		// Restore Headers from the JSON-serialized hash entry written in SerializeToHashEntries
		// (init-only on OutboundMessage → must be set in the object initializer below).
		var headers = dict.TryGetValue("Headers", out var headersValue) && !headersValue.IsNullOrEmpty
			? JsonSerializer.Deserialize<Dictionary<string, object>>((string)headersValue!, EventSerializationDefaults.Canonical) ?? new Dictionary<string, object>(StringComparer.Ordinal)
			: new Dictionary<string, object>(StringComparer.Ordinal);

		var message = new OutboundMessage
		{
			Id = messageId,
			MessageType = dict.GetValueOrDefault("MessageType", string.Empty)!,
			Payload = (byte[])dict.GetValueOrDefault("Payload", RedisValue.EmptyString)!,
			Destination = dict.GetValueOrDefault("Destination", string.Empty)!,
			CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds((long)dict.GetValueOrDefault("CreatedAt", 0)),
			Status = (OutboxStatus)(int)dict.GetValueOrDefault("Status", 0),
			Priority = (int)dict.GetValueOrDefault("Priority", 0),
			RetryCount = (int)dict.GetValueOrDefault("RetryCount", 0),
			IsMultiTransport = (int)dict.GetValueOrDefault("IsMultiTransport", 0) == 1,
			Headers = headers,
		};

		if (dict.TryGetValue("CorrelationId", out var correlationId) && !correlationId.IsNullOrEmpty)
		{
			message.CorrelationId = correlationId!;
		}

		if (dict.TryGetValue("CausationId", out var causationId) && !causationId.IsNullOrEmpty)
		{
			message.CausationId = causationId!;
		}

		if (dict.TryGetValue("TenantId", out var tenantId) && !tenantId.IsNullOrEmpty)
		{
			message.TenantId = tenantId!;
		}

		if (dict.TryGetValue("PartitionKey", out var partitionKey) && !partitionKey.IsNullOrEmpty)
		{
			message.PartitionKey = partitionKey!;
		}

		if (dict.TryGetValue("GroupKey", out var groupKey) && !groupKey.IsNullOrEmpty)
		{
			message.GroupKey = groupKey!;
		}

		if (dict.TryGetValue("TargetTransports", out var targetTransports) && !targetTransports.IsNullOrEmpty)
		{
			message.TargetTransports = targetTransports!;
		}

		if (dict.TryGetValue("LastError", out var lastError) && !lastError.IsNullOrEmpty)
		{
			message.LastError = lastError!;
		}

		if (dict.TryGetValue("ScheduledAt", out var scheduledAt) && scheduledAt != 0)
		{
			message.ScheduledAt = DateTimeOffset.FromUnixTimeMilliseconds((long)scheduledAt);
		}

		if (dict.TryGetValue("SentAt", out var sentAt) && sentAt != 0)
		{
			message.SentAt = DateTimeOffset.FromUnixTimeMilliseconds((long)sentAt);
		}

		if (dict.TryGetValue("LastAttemptAt", out var lastAttemptAt) && lastAttemptAt != 0)
		{
			message.LastAttemptAt = DateTimeOffset.FromUnixTimeMilliseconds((long)lastAttemptAt);
		}

		return message;
	}

	private string GetMessageKey(string messageId) => $"{_options.KeyPrefix}:msg:{messageId}";

	private string GetStagedIndexKey() => $"{_options.KeyPrefix}:idx:staged";

	private string GetSentIndexKey() => $"{_options.KeyPrefix}:idx:sent";

	private string GetFailedIndexKey() => $"{_options.KeyPrefix}:idx:failed";

	private string GetScheduledIndexKey() => $"{_options.KeyPrefix}:idx:scheduled";

	private string GetLeasedIndexKey() => $"{_options.KeyPrefix}:idx:leased";

	private string GetMessageKeyPrefix() => $"{_options.KeyPrefix}:msg:";

	// Resolves the lease-owner identifier once: the configured ProcessorId, or a stable generated id.
	// Disjoint claim does not depend on this value (the atomic claim script guarantees it); it is recorded
	// as LeasedBy purely for diagnostics / parity with the SQL Server outbox.
	private string GetProcessorId() =>
		_resolvedProcessorId ??= string.IsNullOrWhiteSpace(_options.ProcessorId)
			? $"{Environment.MachineName}:{Guid.NewGuid():N}"
			: _options.ProcessorId;

	private async Task EnsureConnectedAsync()
	{
		if (_database != null)
		{
			return;
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
	}

	private async Task MoveScheduledToStagedAsync(long nowMs)
	{
		// Get scheduled messages that are now due
		var dueMessages = await _database!.SortedSetRangeByScoreAsync(
			GetScheduledIndexKey(),
			double.NegativeInfinity,
			nowMs).ConfigureAwait(false);

		foreach (var id in dueMessages)
		{
			var message = await GetMessageByIdAsync(id!).ConfigureAwait(false);
			if (message == null)
			{
				continue;
			}

			// Move to staged index
			var score = ((double)message.Priority * 1_000_000_000_000) + message.CreatedAt.ToUnixTimeMilliseconds();
			_ = await _database!.SortedSetAddAsync(GetStagedIndexKey(), id, score).ConfigureAwait(false);
			_ = await _database!.SortedSetRemoveAsync(GetScheduledIndexKey(), id).ConfigureAwait(false);
		}
	}

	private async Task<OutboundMessage?> GetMessageByIdAsync(string messageId)
	{
		var key = GetMessageKey(messageId);
		var entries = await _database!.HashGetAllAsync(key).ConfigureAwait(false);

		if (entries.Length == 0)
		{
			return null;
		}

		return DeserializeFromHashEntries(messageId, entries);
	}

	[LoggerMessage(DataRedisEventId.OutboxMessageStaged, LogLevel.Debug,
		"Staged message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogMessageStaged(string messageId, string messageType, string destination);

	[LoggerMessage(DataRedisEventId.OutboxMessageEnqueued, LogLevel.Debug, "Enqueued message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataRedisEventId.OutboxMessageSent, LogLevel.Debug, "Marked message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataRedisEventId.OutboxMessageFailed, LogLevel.Warning,
		"Marked message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataRedisEventId.OutboxCleanedUp, LogLevel.Information, "Cleaned up {Count} sent messages older than {OlderThan}")]
	private partial void LogMessagesCleanedUp(int count, DateTimeOffset olderThan);
}
