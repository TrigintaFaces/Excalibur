// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Provides default values for CDC recovery options.
/// </summary>
public static class CdcRecoveryOptionsDefaults
{
	/// <summary>
	/// Default recovery strategy for stale position scenarios.
	/// </summary>
	/// <remarks>
	/// <see cref="StalePositionRecoveryStrategy.FallbackToEarliest"/> is chosen as the default
	/// because it is the safest option - it may reprocess some events but ensures no data loss.
	/// </remarks>
	public const StalePositionRecoveryStrategy DefaultRecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest;

	/// <summary>
	/// Default maximum number of recovery attempts.
	/// </summary>
	public const int DefaultMaxRecoveryAttempts = 3;

	/// <summary>
	/// Default delay between recovery attempts in milliseconds.
	/// </summary>
	public const int DefaultRecoveryAttemptDelayMs = 1000;

	/// <summary>
	/// Default setting for structured logging of position resets.
	/// </summary>
	public const bool DefaultEnableStructuredLogging = true;
}
