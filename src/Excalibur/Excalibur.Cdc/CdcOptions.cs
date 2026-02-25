// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc;

/// <summary>
/// Configuration options for CDC processing.
/// </summary>
public sealed class CdcOptions
{
	/// <summary>
	/// Gets the table tracking configurations.
	/// </summary>
	public List<CdcTableTrackingOptions> TrackedTables { get; } = [];

	/// <summary>
	/// Gets or sets the recovery strategy for stale positions.
	/// </summary>
	/// <value>
	/// The recovery strategy. Default is <see cref="StalePositionRecoveryStrategy.FallbackToEarliest"/>.
	/// </value>
	public StalePositionRecoveryStrategy RecoveryStrategy { get; set; } = StalePositionRecoveryStrategy.FallbackToEarliest;

	/// <summary>
	/// Gets or sets the callback invoked when a stale position reset occurs.
	/// </summary>
	public CdcPositionResetHandler? OnPositionReset { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of recovery attempts before giving up.
	/// </summary>
	/// <value>The maximum retry count. Default is 3.</value>
	[Range(0, int.MaxValue)]
	public int MaxRecoveryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between recovery attempts.
	/// </summary>
	/// <value>The delay between attempts. Default is 1 second.</value>
	public TimeSpan RecoveryAttemptDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets a value indicating whether to log structured events for position resets.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable structured logging; otherwise, <see langword="false"/>.
	/// Default is <see langword="true"/>.
	/// </value>
	public bool EnableStructuredLogging { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether background processing is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if background processing is enabled; otherwise, <see langword="false"/>.
	/// Default is <see langword="false"/>.
	/// </value>
	public bool EnableBackgroundProcessing { get; set; }

	/// <summary>
	/// Validates the options and throws if the configuration is invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when configuration is invalid.
	/// </exception>
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

/// <summary>
/// Configuration options for tracking a specific table.
/// </summary>
public sealed class CdcTableTrackingOptions
{
	/// <summary>
	/// Gets or sets the fully qualified table name.
	/// </summary>
	[Required]
	public string TableName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the capture instance name (SQL Server specific).
	/// </summary>
	public string? CaptureInstance { get; set; }

	/// <summary>
	/// Gets the event type mappings for different change types.
	/// </summary>
	public Dictionary<CdcChangeType, Type> EventMappings { get; } = [];

	/// <summary>
	/// Gets or sets the filter predicate for changes.
	/// </summary>
	public Func<CdcDataChange, bool>? Filter { get; set; }
}
