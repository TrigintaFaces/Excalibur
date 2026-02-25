// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Cdc;

namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Configuration options for DynamoDB Streams CDC stale position recovery behavior.
/// </summary>
/// <remarks>
/// <para>
/// These options control how the DynamoDB CDC processor handles scenarios where the
/// saved sequence number or shard iterator is no longer valid. Common scenarios include:
/// <list type="bullet">
/// <item><description>Iterator expiry (15-minute timeout without use)</description></item>
/// <item><description>Data trimmed beyond 24-hour retention window</description></item>
/// <item><description>Shard splits, merges, or closure</description></item>
/// <item><description>Stream disabled or table deleted</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record DynamoDbCdcRecoveryOptions
{
	/// <summary>
	/// Gets or sets the strategy to use when a stale position is detected.
	/// </summary>
	/// <value>
	/// The recovery strategy. Defaults to <see cref="StalePositionRecoveryStrategy.Throw"/>.
	/// </value>
	public StalePositionRecoveryStrategy RecoveryStrategy { get; init; } =
		StalePositionRecoveryStrategy.Throw;

	/// <summary>
	/// Gets or sets the callback to invoke when a position reset occurs.
	/// </summary>
	/// <value>
	/// The callback handler, or <see langword="null"/> if no callback is configured.
	/// Required when <see cref="RecoveryStrategy"/> is <see cref="StalePositionRecoveryStrategy.InvokeCallback"/>.
	/// </value>
	public CdcPositionResetHandler? OnPositionReset { get; init; }

	/// <summary>
	/// Gets or sets whether to automatically refresh expired shard iterators.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to automatically refresh iterators; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// When enabled and an iterator expires (15-minute timeout), the processor will
	/// automatically obtain a new iterator from the last known sequence number.
	/// </remarks>
	public bool AutoRefreshExpiredIterators { get; init; } = true;

	/// <summary>
	/// Gets or sets whether to automatically handle shard splits.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to handle shard splits automatically; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// When a shard closes due to a split, the processor will automatically discover
	/// and start processing the child shards.
	/// </remarks>
	public bool HandleShardSplitsGracefully { get; init; } = true;

	/// <summary>
	/// Gets or sets the maximum number of recovery attempts before giving up.
	/// </summary>
	/// <value>
	/// The maximum number of attempts. Defaults to 3.
	/// </value>
	/// <remarks>
	/// If recovery fails after this many attempts, the processor will throw
	/// a <see cref="DynamoDbStalePositionException"/>.
	/// </remarks>
	[Range(1, int.MaxValue)]
	public int MaxRecoveryAttempts { get; init; } = 3;

	/// <summary>
	/// Gets or sets the delay between recovery attempts.
	/// </summary>
	/// <value>
	/// The delay between attempts. Defaults to 1 second.
	/// </value>
	public TimeSpan RecoveryAttemptDelay { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets whether to invoke the <see cref="OnPositionReset"/> callback
	/// even when using automatic recovery strategies.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to always invoke the callback; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// This allows consumers to log or alert on position resets even when using
	/// <see cref="StalePositionRecoveryStrategy.FallbackToEarliest"/> or
	/// <see cref="StalePositionRecoveryStrategy.FallbackToLatest"/>.
	/// </remarks>
	public bool AlwaysInvokeCallbackOnReset { get; init; } = true;

	/// <summary>
	/// Gets or sets the iterator refresh interval to prevent expiry.
	/// </summary>
	/// <value>
	/// The interval at which to proactively refresh iterators.
	/// Defaults to 10 minutes (iterators expire at 15 minutes).
	/// </value>
	/// <remarks>
	/// Setting this lower than 15 minutes helps prevent iterator expiry during
	/// periods of low stream activity.
	/// </remarks>
	public TimeSpan IteratorRefreshInterval { get; init; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Validates the recovery options.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="RecoveryStrategy"/> is <see cref="StalePositionRecoveryStrategy.InvokeCallback"/>
	/// but <see cref="OnPositionReset"/> is not configured.
	/// </exception>
	public void Validate()
	{
		if (RecoveryStrategy == StalePositionRecoveryStrategy.InvokeCallback && OnPositionReset is null)
		{
			throw new InvalidOperationException(
				$"When using {nameof(StalePositionRecoveryStrategy)}.{nameof(StalePositionRecoveryStrategy.InvokeCallback)}, " +
				$"the {nameof(OnPositionReset)} callback must be configured.");
		}

		if (MaxRecoveryAttempts < 1)
		{
			throw new InvalidOperationException(
				$"{nameof(MaxRecoveryAttempts)} must be at least 1.");
		}

		if (RecoveryAttemptDelay < TimeSpan.Zero)
		{
			throw new InvalidOperationException(
				$"{nameof(RecoveryAttemptDelay)} cannot be negative.");
		}

		if (IteratorRefreshInterval < TimeSpan.Zero || IteratorRefreshInterval > TimeSpan.FromMinutes(14))
		{
			throw new InvalidOperationException(
				$"{nameof(IteratorRefreshInterval)} must be between 0 and 14 minutes " +
				"(iterators expire at 15 minutes).");
		}
	}
}
