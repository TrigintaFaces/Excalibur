# wseau9 — Redis store R1/R2/R3 implementation spec (execution-ready)

**Author:** PlatformDeveloper · **Sprint:** S894 · **Bead:** wseau9 · **Store:** `Excalibur.Outbox.Redis` (last A1 full-lease core store; Mongo done+verified 7/7 GREEN).

**Design verified (run→read→cite):**
- `MoveScheduledToStagedAsync:1032` ZREMs `scheduledIdx` on promote (no double-claim); leaves `failedIdx` intact (reporting preserved through re-attempt — matches SqlServer Status=Failed-during-retry).
- Conformance base asserts plain `MarkFailedAsync → GetFailedMessagesAsync` returns it w/ RetryCount (`OutboxStoreConformanceTestBase.cs:536,570,608,647`; owned-path twin `:1369-1389` asserts BOTH recording AND reclaim-after-floor).
- **Mechanism:** reuse the proven `scheduledIdx` reclaim path (NO change to `ClaimMessagesLuaScript` / S1) + keep in `failedIdx` for reporting.

**Raw-string indentation (CRITICAL):** each Lua content line = `TAB` + 43 spaces; nested lines add one more `TAB`. C# 11 strips the common prefix. Edit carefully or read exact bytes first (`sed -n 'A,Bp' file | cat -A`).

---

## Edit 1 — `MarkFailedLuaScript` (currently ~:123-154): full rewrite

New KEYS: `[key, stagedIdx, failedIdx, leasedIdx, scheduledIdx]` · New ARGV: `[messageId, errorMessage, retryCount, lastAttemptAt, failedStatus, leasedBy, nextAttemptAt]`

```lua
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

-- R2 ownership guard IN the atomic write: only the current lease owner (or an already-released row).
local currentLease = redis.call('HGET', key, 'LeasedBy')
if currentLease and currentLease ~= false and currentLease ~= '' and currentLease ~= leasedBy then
	return {ok = 'NOT_OWNER'}
end

-- R3 monotonic: never lower RetryCount.
local newRetry = tonumber(retryCount)
local currentRetry = tonumber(redis.call('HGET', key, 'RetryCount')) or 0
if currentRetry > newRetry then newRetry = currentRetry end

redis.call('HMSET', key,
	'Status', failedStatus,
	'LastError', errorMessage,
	'RetryCount', newRetry,
	'LastAttemptAt', lastAttemptAt,
	'NextAttemptAt', nextAttemptAt)

-- Free the lease (single-write parity with SqlServer/Mongo) + drop in-flight index entries.
redis.call('HDEL', key, 'LeasedAt', 'LeasedBy')
redis.call('ZREM', stagedIdx, messageId)
redis.call('ZREM', leasedIdx, messageId)

-- R1: re-queue into scheduledIdx scored by NextAttemptAt (now+F) so MoveScheduledToStaged
-- re-surfaces it only after the floor (at-least-once; no zero-backoff hot-loop).
redis.call('ZADD', scheduledIdx, tonumber(nextAttemptAt), messageId)

-- Keep in failedIdx for GetFailedMessages reporting (retry-count-ordered).
local score = newRetry * 1000000000000 + tonumber(lastAttemptAt)
redis.call('ZADD', failedIdx, score, messageId)

return {ok = 'SUCCESS'}
```

## Edit 2 — `MarkFailedWithBackoffLuaScript` (~:163-193): add R2 + R3 + free-lease

Add ARGV[7]=`leasedBy`. After the EXISTS check insert the SAME R2 guard + R3 `newRetry` block as Edit 1. Change `'RetryCount', retryCount` → `'RetryCount', newRetry`. Add `redis.call('HDEL', key, 'LeasedAt', 'LeasedBy')` before the ZREMs. (Keeps Status=Staged + scheduledIdx; stays OUT of failedIdx — it is a scheduled retry, not a reported failure.)

## Edit 3 — `MarkSentLuaScript` (~:51-90): add failedIdx cleanup

Add KEYS[6]=`failedIdx`; add `redis.call('ZREM', failedIdx, messageId)` alongside the other ZREMs (:84-86). Else a reclaimed-then-sent message leaks into `GetFailedMessages`.

## Edit 4 — C# `MarkSentAsync` (:511-515)

Add `GetFailedIndexKey()` to the keys array (now 6 keys): `[key, GetStagedIndexKey(), GetSentIndexKey(), GetScheduledIndexKey(), GetLeasedIndexKey(), GetFailedIndexKey()]`.

## Edit 5 — C# `MarkFailedAsync` (:542-568)

Drop the `KeyExistsAsync` pre-check (rely on Lua NOT_FOUND = silent). Add the scheduled key + new ARGV:
```csharp
var nextAttemptAtMs = _timeProvider.GetUtcNow().AddSeconds(_options.FailureBackoffFloorSeconds)
	.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
_ = await _database!.ScriptEvaluateAsync(
	MarkFailedLuaScript,
	[key, GetStagedIndexKey(), GetFailedIndexKey(), GetLeasedIndexKey(), GetScheduledIndexKey()],
	[messageId, errorMessage, retryCount.ToString(), lastAttemptAt, ((int)OutboxStatus.Failed).ToString(),
		GetProcessorId(), nextAttemptAtMs]).ConfigureAwait(false);
```

## Edit 6 — C# `MarkFailedWithBackoffAsync` (:597-600)

Append `GetProcessorId()` to the ARGV array (ARGV[7]=leasedBy).

## Edit 7 — `RedisOutboxOptions.cs` (after `LeaseTimeoutSeconds`, before `ProcessorId`)

```csharp
/// <summary>
/// Gets or sets the failure-backoff floor F, in seconds: after a plain <c>MarkFailedAsync</c>, the
/// message becomes re-claimable only after F elapses. Bounds the plain-path retry cadence so it cannot
/// hot-loop the drain, while staying eventually re-claimable (at-least-once). Must exceed the drain
/// polling interval; the validator enforces that cross-options invariant.
/// </summary>
/// <value>The failure-backoff floor in seconds. Defaults to 30 (uniform across the outbox family).</value>
[Range(1, int.MaxValue)]
public int FailureBackoffFloorSeconds { get; set; } = 30;
```

## Edit 8 — `RedisOutboxOptionsValidator.cs`: add F > PollingInterval invariant (parity w/ Mongo)

Primary-ctor inject `IOptions<OutboxProcessingOptions>` + `IOptions<OutboxPartitionOptions>` (usings `Excalibur.Outbox.Outbox` + `Excalibur.Outbox.Partitioning`). Keep `options.Validate()` try/catch, then the same `effectivePollSeconds` check as `MongoDbOutboxOptionsValidator` / `PostgresOutboxStoreOptionsValidator`, message `RedisOutboxOptions.FailureBackoffFloorSeconds`. DI already registers by type (`OutboxBuilderRedisExtensions.cs:101`), so ctor injection resolves.

## Edit 9 — `PublicAPI.Unshipped.txt`

```
Excalibur.Outbox.Redis.RedisOutboxOptions.FailureBackoffFloorSeconds.get -> int
Excalibur.Outbox.Redis.RedisOutboxOptions.FailureBackoffFloorSeconds.set -> void
```

---

## Verify
`dotnet build src/Excalibur/Excalibur.Outbox.Redis/Excalibur.Outbox.Redis.csproj --no-incremental` → 0/0. Then TestsDeveloper authors the real-Redis conformance arm (property-bound, `c2qbfx` `RedisContainerFixture`; RED against pre-fix stranding). Do NOT flip ARCHITECTURE.md Redis row to ✅ until that arm is GREEN.
