// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Contains extension methods for structured logging in the application.
/// </summary>
/// <remarks>
/// Uses source-generated logging for high performance.
/// </remarks>
internal static partial class LogExtensions
{
	/// <summary>
	/// Logs an error when activity groups cannot be retrieved.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="reason"> The reason for the failure. </param>
	/// <param name="exception"> The associated exception, if any. </param>
	[LoggerMessage(A3EventId.ActivityGroupsError, LogLevel.Error,
		"Failed to retrieve activity groups because {Reason}")]
	public static partial void LogErrorActivityGroups(this ILogger logger, string reason, Exception? exception);

	/// <summary>
	/// Logs an error when activity group grants cannot be retrieved.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="reason"> The reason for the failure. </param>
	/// <param name="exception"> The associated exception, if any. </param>
	[LoggerMessage(A3EventId.ActivityGrantsError, LogLevel.Error,
		"Failed to retrieve activity group grants because {Reason}")]
	public static partial void LogErrorActivityGrants(this ILogger logger, string reason, Exception? exception);
}
