// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Utility class for running detached background tasks.
/// </summary>
public static partial class BackgroundTaskRunner
{

	/// <summary>
	/// Runs a task in the background without waiting for completion.
	/// </summary>
	/// <param name="taskFactory"> Factory function that creates the task to run. </param>
	/// <param name="onError"> Optional error handler for exceptions. </param>
	/// <param name="logger"> Optional logger for unhandled exceptions. </param>
	/// <param name="cancellationToken"> Cancellation token for the background task. </param>
	/// <remarks>
	/// This method starts a task on the thread pool and does not wait for completion. Exceptions are handled according to the provided
	/// error handler or logged.
	/// </remarks>
	public static void RunDetachedInBackground(
		Func<CancellationToken, Task> taskFactory,
		CancellationToken cancellationToken,
		Func<Exception, Task>? onError = null,
		ILogger? logger = null) =>
		_ = Task.Factory.StartNew(
			async () =>
			{
				try
				{
					await taskFactory(cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					if (onError != null)
					{
						await onError(ex).ConfigureAwait(false);
					}
					else if (logger != null)
					{
						logger.LogUnhandledBackgroundException(ex);
					}
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).Unwrap();

	// Source-generated logging methods
	[LoggerMessage(CoreEventId.UnhandledBackgroundException, LogLevel.Error,
		"Unhandled exception in background task.")]
	private static partial void LogUnhandledBackgroundException(this ILogger logger, Exception ex);
}
