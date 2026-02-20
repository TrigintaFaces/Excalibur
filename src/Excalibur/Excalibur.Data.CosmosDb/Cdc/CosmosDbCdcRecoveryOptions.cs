// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Text;

using Excalibur.Cdc;
using Excalibur.Data.CosmosDb.Resources;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Configuration options for CosmosDB CDC stale position recovery behavior.
/// </summary>
/// <remarks>
/// <para>
/// These options control how the CosmosDB CDC processor handles scenarios where the
/// saved continuation token is no longer valid. Common scenarios include:
/// <list type="bullet">
/// <item><description>Continuation token expiry (7-day retention window)</description></item>
/// <item><description>Container deleted or recreated</description></item>
/// <item><description>Partition splits or merges</description></item>
/// <item><description>Throughput changes causing repartitioning</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record CosmosDbCdcRecoveryOptions
{
	private static readonly CompositeFormat PropertyMustBeAtLeastOneFormat =
		CompositeFormat.Parse(ErrorMessages.PropertyMustBeAtLeastOne);

	private static readonly CompositeFormat PropertyCannotBeNegativeFormat =
		CompositeFormat.Parse(ErrorMessages.PropertyCannotBeNegative);

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
	/// Gets or sets whether to automatically recreate the Change Feed processor when the continuation token is invalid.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to automatically recreate the processor; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// When enabled, the processor will attempt to restart the Change Feed from the
	/// position determined by the <see cref="RecoveryStrategy"/> instead of propagating
	/// the original exception.
	/// </remarks>
	public bool AutoRecreateProcessorOnInvalidToken { get; init; } = true;

	/// <summary>
	/// Gets or sets whether to use the current timestamp when resume fails.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to use current timestamp for recovery; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// When enabled and the recovery strategy is <see cref="StalePositionRecoveryStrategy.FallbackToLatest"/>,
	/// the processor will use <c>StartTime</c> with the current UTC time
	/// instead of attempting to use an invalid continuation token.
	/// </para>
	/// <para>
	/// This is safer than using an invalid continuation token, as CosmosDB will
	/// reject invalid tokens but accept valid timestamps.
	/// </para>
	/// </remarks>
	public bool UseCurrentTimeOnResumeFailure { get; init; } = true;

	/// <summary>
	/// Gets or sets the maximum number of recovery attempts before giving up.
	/// </summary>
	/// <value>
	/// The maximum number of attempts. Defaults to 3.
	/// </value>
	/// <remarks>
	/// If recovery fails after this many attempts, the processor will throw
	/// a <see cref="CosmosDbStalePositionException"/>.
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
	/// Gets or sets whether to handle partition splits gracefully.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to handle partition splits automatically; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// When a partition split occurs, the Change Feed processor needs to handle the new
	/// partition key ranges. When enabled, the processor will automatically detect and
	/// adapt to partition splits.
	/// </remarks>
	public bool HandlePartitionSplitsGracefully { get; init; } = true;

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
				ErrorMessages.OnPositionResetCallbackRequired);
		}

		if (MaxRecoveryAttempts < 1)
		{
			throw new InvalidOperationException(
				string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyMustBeAtLeastOneFormat, nameof(MaxRecoveryAttempts)));
		}

		if (RecoveryAttemptDelay < TimeSpan.Zero)
		{
			throw new InvalidOperationException(
				string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyCannotBeNegativeFormat, nameof(RecoveryAttemptDelay)));
		}
	}
}
