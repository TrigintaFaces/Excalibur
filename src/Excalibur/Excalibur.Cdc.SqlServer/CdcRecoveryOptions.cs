// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Configuration options for CDC stale position recovery behavior.
/// </summary>
/// <remarks>
/// <para>
/// These options control how the CDC processor handles scenarios where the saved position (LSN)
/// is no longer valid in the database. This can occur due to CDC cleanup jobs, database restores,
/// or CDC being disabled and re-enabled.
/// </para>
/// <para>
/// The default configuration uses <see cref="StalePositionRecoveryStrategy.FallbackToEarliest"/>
/// which is the safest option, potentially reprocessing events but ensuring no data loss.
/// </para>
/// </remarks>
public sealed class CdcRecoveryOptions
{
	/// <summary>
	/// Gets or sets the strategy for handling stale position scenarios.
	/// </summary>
	/// <value>
	/// The recovery strategy to use. Default is <see cref="StalePositionRecoveryStrategy.FallbackToEarliest"/>.
	/// </value>
	/// <remarks>
	/// <list type="bullet">
	/// <item><description><see cref="StalePositionRecoveryStrategy.Throw"/> - Throws exception (legacy behavior)</description></item>
	/// <item><description><see cref="StalePositionRecoveryStrategy.FallbackToEarliest"/> - Resume from earliest (safe, may reprocess)</description></item>
	/// <item><description><see cref="StalePositionRecoveryStrategy.FallbackToLatest"/> - Skip to latest (data loss possible)</description></item>
	/// <item><description><see cref="StalePositionRecoveryStrategy.InvokeCallback"/> - Custom handling via callback</description></item>
	/// </list>
	/// </remarks>
	public StalePositionRecoveryStrategy RecoveryStrategy { get; set; } = StalePositionRecoveryStrategy.FallbackToEarliest;

	/// <summary>
	/// Gets or sets the callback invoked when a stale position reset occurs.
	/// </summary>
	/// <value>
	/// The callback delegate, or <see langword="null"/> if not configured.
	/// </value>
	/// <remarks>
	/// <para>
	/// This callback is required when <see cref="RecoveryStrategy"/> is set to
	/// <see cref="StalePositionRecoveryStrategy.InvokeCallback"/>.
	/// </para>
	/// <para>
	/// The callback is also invoked (if configured) for other strategies to allow logging
	/// or alerting, but the recovery action is determined by the <see cref="RecoveryStrategy"/>.
	/// </para>
	/// </remarks>
	public CdcPositionResetHandler? OnPositionReset { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of recovery attempts before giving up.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts. Default is 3.
	/// </value>
	/// <remarks>
	/// After exhausting all recovery attempts, the processor will throw a
	/// <see cref="SqlServerCdcStalePositionException"/> regardless of the configured strategy.
	/// </remarks>
	[Range(0, int.MaxValue)]
	public int MaxRecoveryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between recovery attempts.
	/// </summary>
	/// <value>
	/// The delay between attempts. Default is 1 second.
	/// </value>
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
	/// Creates a <see cref="CdcRecoveryOptions"/> from the recovery-related properties of <see cref="CdcOptions"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This factory eliminates the need to manually copy each recovery property when bridging
	/// from the user-facing <see cref="CdcOptions"/> (in <c>Excalibur.Cdc</c>) to the
	/// provider-specific <see cref="CdcRecoveryOptions"/> (in <c>Excalibur.Cdc.SqlServer</c>).
	/// If a new recovery property is added to <see cref="CdcOptions"/>, it only needs to be
	/// added here rather than at every bridge site.
	/// </para>
	/// </remarks>
	/// <param name="cdcOptions">The CDC options containing recovery configuration.</param>
	/// <returns>A new <see cref="CdcRecoveryOptions"/> with properties copied from the source.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="cdcOptions"/> is <c>null</c>.</exception>
	public static CdcRecoveryOptions FromCdcOptions(CdcOptions cdcOptions)
	{
		ArgumentNullException.ThrowIfNull(cdcOptions);

		return new CdcRecoveryOptions
		{
			RecoveryStrategy = cdcOptions.RecoveryStrategy,
			OnPositionReset = cdcOptions.OnPositionReset,
			MaxRecoveryAttempts = cdcOptions.MaxRecoveryAttempts,
			RecoveryAttemptDelay = cdcOptions.RecoveryAttemptDelay,
			EnableStructuredLogging = cdcOptions.EnableStructuredLogging,
		};
	}

	/// <summary>
	/// Validates the options and throws if the configuration is invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="RecoveryStrategy"/> is <see cref="StalePositionRecoveryStrategy.InvokeCallback"/>
	/// but <see cref="OnPositionReset"/> is not configured.
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
