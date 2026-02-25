// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Redis-based implementation of <see cref="IDistributedJobLock" />.
/// </summary>
internal sealed partial class RedisDistributedJobLock(
	IDatabase database,
	string lockKey,
	string jobKey,
	string instanceId,
	DateTimeOffset acquiredAt,
	DateTimeOffset expiresAt,
	ILogger logger)
	: IDistributedJobLock
{
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

		// Use TimeSpan overload instead of DateTime to avoid UTC-vs-local ambiguity
		var extended = await database.KeyExpireAsync(lockKey, additionalDuration).ConfigureAwait(false);

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
			_ = await database.KeyDeleteAsync(lockKey).ConfigureAwait(false);
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
		catch (OperationCanceledException)
		{
			// Disposal timed out — lock will expire naturally via Redis TTL
			logger.LogWarning("Timed out releasing distributed lock for job {JobKey} during disposal", JobKey);
		}
	}
}
