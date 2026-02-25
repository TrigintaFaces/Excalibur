// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions;

/// <summary>
/// Provides predefined timeout durations (in seconds) for various database operations.
/// </summary>
/// <remarks>
/// This class centralizes timeout configurations to ensure consistency and readability when handling database operations that require
/// different levels of execution time.
/// </remarks>
public static class DbTimeouts
{
	/// <summary>
	/// The timeout duration (in seconds) for regular database operations.
	/// </summary>
	/// <remarks> Use this timeout for standard database queries or commands that are expected to complete quickly. </remarks>
	public const int RegularTimeoutSeconds = 60;

	/// <summary>
	/// The timeout duration (in seconds) for long-running database operations.
	/// </summary>
	/// <remarks>
	/// Use this timeout for operations that involve more intensive processing or larger datasets, such as batch queries or updates.
	/// </remarks>
	public const int LongRunningTimeoutSeconds = 600;

	/// <summary>
	/// The timeout duration (in seconds) for extra long-running database operations.
	/// </summary>
	/// <remarks>
	/// Use this timeout for highly resource-intensive operations or processes that are expected to take significantly longer to complete.
	/// </remarks>
	public const int ExtraLongRunningTimeoutSeconds = 1200;
}
