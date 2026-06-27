// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Coordination;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Excalibur.Jobs.Redis.Coordination;

/// <summary>
/// Redis-based implementation of <see cref="IDistributedJobLock" />.
/// </summary>
internal sealed partial class RedisDistributedJobLock(
	IDatabase database,
	string lockKey,
	string jobKey,
	string instanceId,
	string ownerToken,
	DateTimeOffset acquiredAt,
	DateTimeOffset expiresAt,
	ILogger logger)
	: IDistributedJobLock
{
	// Atomic owner-checked release: DEL the key ONLY if its stored value still equals
	// this acquisition's per-acquisition owner token. A stale handle (lock expired and
	// re-acquired by another holder) will not match and the script is a no-op. [bd-jqlqc8]
	private const string OwnerCheckedReleaseScript =
		"if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

	// Atomic owner-checked extend: PEXPIRE (milliseconds) ONLY if the stored value still
	// equals this acquisition's owner token. Milliseconds keep parity with the PX acquire. [bd-jqlqc8]
	private const string OwnerCheckedExtendScript =
		"if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('pexpire', KEYS[1], ARGV[2]) else return 0 end";

	private volatile bool _disposed;

	/// <inheritdoc />
	public string JobKey { get; } = jobKey;

	/// <inheritdoc />
	public string InstanceId { get; } = instanceId;

	/// <inheritdoc />
	public DateTimeOffset AcquiredAt { get; } = acquiredAt;

	/// <inheritdoc />
	public DateTimeOffset ExpiresAt { get; private set; } = expiresAt;

	/// <inheritdoc />
	public bool IsValid => !_disposed && DateTimeOffset.UtcNow < ExpiresAt;

	/// <inheritdoc />
	public async Task<bool> ExtendAsync(TimeSpan additionalDuration, CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return false;
		}

		// Owner-checked extend: only PEXPIRE when THIS acquisition still owns the lock
		// (stored value still equals our token). If the lock expired and was re-acquired
		// by another holder, the token no longer matches and the script is a no-op
		// (returns 0) — we never extend someone else's lock. [bd-jqlqc8]
		var result = await database.ScriptEvaluateAsync(
			OwnerCheckedExtendScript,
			[lockKey],
			[ownerToken, (long)additionalDuration.TotalMilliseconds]).ConfigureAwait(false);

		var extended = (long)result == 1;

		if (extended)
		{
			ExpiresAt = DateTimeOffset.UtcNow.Add(additionalDuration);
		}

		return extended;
	}

	/// <inheritdoc />
	public async Task ReleaseAsync(CancellationToken cancellationToken)
	{
		if (!_disposed)
		{
			// Owner-checked release: only DEL when THIS acquisition still owns the lock.
			// A stale handle whose lock has expired and been re-acquired by another holder
			// will NOT delete the new holder's lock (token mismatch -> no-op). We still mark
			// ourselves disposed: this handle is finished regardless of the Redis outcome. [bd-jqlqc8]
			_ = await database.ScriptEvaluateAsync(
				OwnerCheckedReleaseScript,
				[lockKey],
				[ownerToken]).ConfigureAwait(false);
			_disposed = true;
			logger.LogDebug("Released distributed lock for job {JobKey}", JobKey);
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		try
		{
			await ReleaseAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cts.IsCancellationRequested)
		{
			// Disposal timed out — lock will expire naturally via Redis TTL
			logger.LogWarning("Timed out releasing distributed lock for job {JobKey} during disposal", JobKey);
		}
	}
}
