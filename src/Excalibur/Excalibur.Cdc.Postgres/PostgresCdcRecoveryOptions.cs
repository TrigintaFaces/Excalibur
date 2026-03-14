// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Configuration options for Postgres CDC stale position recovery behavior.
/// </summary>
public sealed class PostgresCdcRecoveryOptions
{
	/// <summary>The default maximum number of recovery attempts.</summary>
	public const int DefaultMaxRecoveryAttempts = 3;

	/// <summary>The default delay between recovery attempts in seconds.</summary>
	public const int DefaultRecoveryAttemptDelaySeconds = 1;

	/// <summary>
	/// Gets or sets the strategy for handling stale position scenarios.
	/// </summary>
	public StalePositionRecoveryStrategy RecoveryStrategy { get; set; } =
		StalePositionRecoveryStrategy.FallbackToEarliest;

	/// <summary>
	/// Gets or sets the callback invoked when a stale position reset occurs.
	/// </summary>
	public CdcPositionResetHandler? OnPositionReset { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of recovery attempts before giving up.
	/// </summary>
	[Range(0, int.MaxValue)]
	public int MaxRecoveryAttempts { get; set; } = DefaultMaxRecoveryAttempts;

	/// <summary>
	/// Gets or sets the delay between recovery attempts.
	/// </summary>
	public TimeSpan RecoveryAttemptDelay { get; set; } = TimeSpan.FromSeconds(DefaultRecoveryAttemptDelaySeconds);

	/// <summary>
	/// Gets or sets a value indicating whether to log structured events for position resets.
	/// </summary>
	public bool EnableStructuredLogging { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically recreate the replication slot
	/// if it becomes invalid.
	/// </summary>
	public bool AutoRecreateSlotOnInvalidation { get; set; } = true;

	/// <summary>
	/// Validates the options and throws if the configuration is invalid.
	/// </summary>
	public void Validate()
	{
		if (RecoveryStrategy == StalePositionRecoveryStrategy.InvokeCallback && OnPositionReset == null)
		{
			throw new InvalidOperationException(
				$"The {nameof(OnPositionReset)} callback must be configured when using " +
				$"{nameof(StalePositionRecoveryStrategy)}.{nameof(StalePositionRecoveryStrategy.InvokeCallback)} strategy.");
		}

		if (MaxRecoveryAttempts < 0)
		{
			throw new InvalidOperationException($"{nameof(MaxRecoveryAttempts)} must be non-negative.");
		}

		if (RecoveryAttemptDelay < TimeSpan.Zero)
		{
			throw new InvalidOperationException($"{nameof(RecoveryAttemptDelay)} must be non-negative.");
		}
	}
}
