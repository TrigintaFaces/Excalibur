// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Redis-based implementation of <see cref="ILeadershipToken" />.
/// </summary>
internal sealed partial class RedisLeadershipToken(
	IDatabase database,
	string leaderKey,
	string leaderInstanceId,
	DateTimeOffset acquiredAt,
	DateTimeOffset expiresAt,
	ILogger logger)
	: ILeadershipToken
{
	private volatile bool _disposed;

	/// <inheritdoc />
	public string LeaderInstanceId { get; } = leaderInstanceId;

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
		var extended = await database.KeyExpireAsync(leaderKey, additionalDuration).ConfigureAwait(false);

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
			_ = await database.KeyDeleteAsync(leaderKey).ConfigureAwait(false);
			_disposed = true;
			logger.LogInformation("Released leadership for instance {InstanceId}", LeaderInstanceId);
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
			// Disposal timed out — leadership will expire naturally via Redis TTL
			logger.LogWarning("Timed out releasing leadership for instance {InstanceId} during disposal", LeaderInstanceId);
		}
	}
}
