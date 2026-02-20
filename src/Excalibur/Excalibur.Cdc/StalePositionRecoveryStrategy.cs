// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Specifies the recovery strategy to use when the saved CDC position is no longer valid.
/// </summary>
/// <remarks>
/// <para>
/// A stale position scenario occurs when the saved position (e.g., LSN for SQL Server)
/// no longer exists in the CDC change tables. This can happen due to:
/// <list type="bullet">
/// <item><description>CDC cleanup job purging records past the saved position</description></item>
/// <item><description>Database restore from backup where the max position is lower than saved</description></item>
/// <item><description>CDC being disabled and re-enabled on the database</description></item>
/// <item><description>Lower environment database copy with different CDC history</description></item>
/// </list>
/// </para>
/// </remarks>
public enum StalePositionRecoveryStrategy
{
	/// <summary>
	/// Throw an exception when a stale position is detected.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the legacy behavior. The processor will stop and propagate the exception,
	/// requiring manual intervention to resolve the position mismatch.
	/// </para>
	/// </remarks>
	Throw = 0,

	/// <summary>
	/// Reset to the earliest available position in the CDC change tables.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the safest automatic recovery option. The processor will resume from the minimum
	/// available position, which may result in reprocessing events that were already handled.
	/// </para>
	/// <para>
	/// Consumers should implement idempotent handlers to handle potential duplicates.
	/// </para>
	/// </remarks>
	FallbackToEarliest = 1,

	/// <summary>
	/// Skip to the latest available position, potentially losing unprocessed changes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>Use with caution:</strong> This strategy may result in data loss if there are
	/// unprocessed changes between the stale position and the current maximum position.
	/// </para>
	/// <para>
	/// This option is appropriate only when missing changes can be tolerated or when the
	/// application has other mechanisms to detect and recover from gaps.
	/// </para>
	/// </remarks>
	FallbackToLatest = 2,

	/// <summary>
	/// Invoke the configured callback to let the consumer decide how to handle the stale position.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When this strategy is selected, the <see cref="CdcOptions.OnPositionReset"/> callback
	/// must be configured. The callback receives detailed information about the stale position
	/// and can implement custom recovery logic.
	/// </para>
	/// <para>
	/// If the callback is not configured and this strategy is selected, the processor will throw
	/// an <see cref="InvalidOperationException"/>.
	/// </para>
	/// </remarks>
	InvokeCallback = 3
}
