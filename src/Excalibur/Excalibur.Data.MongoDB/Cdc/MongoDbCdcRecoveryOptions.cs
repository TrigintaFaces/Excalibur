// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Cdc;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Configuration options for MongoDB CDC stale position recovery behavior.
/// </summary>
/// <remarks>
/// <para>
/// These options control how the MongoDB CDC processor handles scenarios where the
/// saved resume token is no longer valid. Common scenarios include:
/// <list type="bullet">
/// <item><description>Oplog rollover before the position was consumed</description></item>
/// <item><description>Collection dropped or renamed</description></item>
/// <item><description>Shard migration in a sharded cluster</description></item>
/// <item><description>Explicit invalidate event from the change stream</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record MongoDbCdcRecoveryOptions
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
	/// Gets or sets whether to automatically recreate the change stream when the resume token is invalid.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to automatically recreate the stream; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// When enabled, the processor will attempt to restart the change stream from the
	/// position determined by the <see cref="RecoveryStrategy"/> instead of propagating
	/// the original exception.
	/// </remarks>
	public bool AutoRecreateStreamOnInvalidToken { get; init; } = true;

	/// <summary>
	/// Gets or sets whether to use the current cluster time when resume fails.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to use cluster time for recovery; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// When enabled and the recovery strategy is <see cref="StalePositionRecoveryStrategy.FallbackToLatest"/>,
	/// the processor will use <c>startAtOperationTime</c> with the current cluster time
	/// instead of attempting to use an invalid resume token.
	/// </para>
	/// <para>
	/// This is safer than using <c>startAfter</c> with an invalid token, as MongoDB will
	/// reject invalid resume tokens but accept valid cluster timestamps.
	/// </para>
	/// </remarks>
	public bool UseClusterTimeOnResumeFailure { get; init; } = true;

	/// <summary>
	/// Gets or sets the maximum number of recovery attempts before giving up.
	/// </summary>
	/// <value>
	/// The maximum number of attempts. Defaults to 3.
	/// </value>
	/// <remarks>
	/// If recovery fails after this many attempts, the processor will throw
	/// a <see cref="MongoDbStalePositionException"/>.
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
	}
}
