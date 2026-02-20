// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Cdc;

namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Configuration options for Firestore CDC stale position recovery behavior.
/// </summary>
/// <remarks>
/// <para>
/// These options control how the Firestore CDC processor handles scenarios where the
/// saved position is no longer valid. Common scenarios include:
/// <list type="bullet">
/// <item><description>Listener stream timeout (gRPC DEADLINE_EXCEEDED)</description></item>
/// <item><description>Collection or document deleted (gRPC NOT_FOUND)</description></item>
/// <item><description>Permission changes (gRPC PERMISSION_DENIED)</description></item>
/// <item><description>Service unavailability (gRPC UNAVAILABLE)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record FirestoreCdcRecoveryOptions
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
	/// Gets or sets whether to automatically reconnect when the listener stream is disconnected.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to automatically reconnect; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// When enabled, the processor will attempt to restart the listener from the
	/// last known position when the stream is unexpectedly closed.
	/// </remarks>
	public bool AutoReconnectOnDisconnect { get; init; } = true;

	/// <summary>
	/// Gets or sets whether to handle permission denied errors by waiting and retrying.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to retry on permission denied; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// Permission denied errors typically indicate a configuration issue or intentional
	/// access revocation. Enable this only if you expect transient permission issues
	/// (e.g., during security rule deployments).
	/// </para>
	/// </remarks>
	public bool RetryOnPermissionDenied { get; init; }

	/// <summary>
	/// Gets or sets the maximum number of recovery attempts before giving up.
	/// </summary>
	/// <value>
	/// The maximum number of attempts. Defaults to 3.
	/// </value>
	/// <remarks>
	/// If recovery fails after this many attempts, the processor will throw
	/// a <see cref="FirestoreStalePositionException"/>.
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
	/// Gets or sets the listener reconnection timeout.
	/// </summary>
	/// <value>
	/// The timeout for listener reconnection. Defaults to 30 seconds.
	/// </value>
	/// <remarks>
	/// This timeout applies when attempting to re-establish a listener connection
	/// after a disconnect.
	/// </remarks>
	public TimeSpan ReconnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum time to wait for unavailable service recovery.
	/// </summary>
	/// <value>
	/// The maximum wait time. Defaults to 5 minutes.
	/// </value>
	/// <remarks>
	/// When the Firestore service is unavailable, the processor will retry with
	/// exponential backoff up to this maximum duration.
	/// </remarks>
	public TimeSpan MaxUnavailableWaitTime { get; init; } = TimeSpan.FromMinutes(5);

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

		if (ReconnectionTimeout < TimeSpan.Zero)
		{
			throw new InvalidOperationException(
				$"{nameof(ReconnectionTimeout)} cannot be negative.");
		}

		if (MaxUnavailableWaitTime < TimeSpan.Zero)
		{
			throw new InvalidOperationException(
				$"{nameof(MaxUnavailableWaitTime)} cannot be negative.");
		}
	}
}
