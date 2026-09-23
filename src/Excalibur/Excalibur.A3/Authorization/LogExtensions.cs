// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

	/// <summary>
	/// Warns, once at start-up, that the configured grant store cannot replace grants atomically and the host
	/// has accepted the partial-state window that follows.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="storeType"> The configured grant store. </param>
	[LoggerMessage(A3EventId.ActivityGroupGrantSyncNotAtomic, LogLevel.Warning,
		"Activity-group grant synchronization is not atomic: {StoreType} cannot replace a set of grants in one "
		+ "step, and GrantSyncAtomicity is BestEffort. Between the delete and the last insert a reader observes "
		+ "a partial set of grants and is denied access the snapshot confers, and a sync that fails part-way "
		+ "leaves the set partial until the next one succeeds.")]
	public static partial void LogGrantSyncNotAtomic(this ILogger logger, string storeType);
}
