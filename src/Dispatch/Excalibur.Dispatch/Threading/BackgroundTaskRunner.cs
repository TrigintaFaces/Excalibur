// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Utility class for running detached background tasks.
/// </summary>
public static partial class BackgroundTaskRunner
{
	/// <summary>
	/// The work this runner has started and not yet observed finishing, so a graceful shutdown can await it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>STATIC BECAUSE THE ENTRY POINT IS.</b> <see cref="RunDetachedInBackground"/> is a shipped public
	/// static with no access to the container, so an injected tracker could not see work a consumer starts
	/// by calling it directly — and that caller is exactly the one with no other way to have their work
	/// awaited. Keeping the registry beside the method covers every caller rather than only the framework's.
	/// </para>
	/// <para>
	/// Each entry carries the cancellation source the drain signals for that work, when the runner owns it.
	/// </para>
	/// </remarks>
	private static readonly ConcurrentDictionary<long, InFlightWork> InFlight = new();

	private static long _nextId;

	/// <summary>
	/// Runs a task in the background without waiting for completion.
	/// </summary>
	/// <param name="taskFactory"> Factory function that creates the task to run. </param>
	/// <param name="onError"> Optional error handler for exceptions. </param>
	/// <param name="logger"> Optional logger for unhandled exceptions. </param>
	/// <param name="cancellationToken"> Cancellation token for the background task. </param>
	/// <remarks>
	/// <para>
	/// This method starts a task on the thread pool and does not wait for completion. Exceptions are handled according to the provided
	/// error handler or logged.
	/// </para>
	/// <para>
	/// <b>DECOUPLED IS NOT DISCARDED.</b> The work is registered so a graceful host shutdown drains it
	/// within the host's own shutdown budget. That makes it best-effort WITHIN THE PROCESS — it does not
	/// make it durable: an abrupt termination, an out-of-memory kill or a node eviction still loses it,
	/// which is inherent to running in-process rather than a gap left open. Work that must survive a crash
	/// belongs in the outbox.
	/// </para>
	/// </remarks>
	public static void RunDetachedInBackground(
		Func<CancellationToken, Task> taskFactory,
		CancellationToken cancellationToken,
		Func<Exception, Task>? onError = null,
		ILogger? logger = null) =>
		Start(taskFactory, ownsShutdownToken: false, onError, logger, cancellationToken);

	/// <summary>
	/// Runs work in the background on a token owned by the shutdown drain rather than by the caller.
	/// </summary>
	/// <param name="taskFactory"> Factory function that creates the task to run. </param>
	/// <param name="onError"> Optional error handler for exceptions. </param>
	/// <param name="logger"> Optional logger for unhandled exceptions. </param>
	/// <remarks>
	/// Used for work the caller has already been told was ACCEPTED. A caller's token is frequently request-scoped
	/// and fires when the response carrying that acceptance completes, which would cancel the very work the
	/// caller was just told was accepted. So accepted work stops following the caller and follows the drain: it is
	/// cancelled only when the host's shutdown budget elapses with it still running.
	/// </remarks>
	internal static void RunDetachedUntilShutdown(
		Func<CancellationToken, Task> taskFactory,
		Func<Exception, Task>? onError,
		ILogger? logger) =>
		Start(taskFactory, ownsShutdownToken: true, onError, logger, CancellationToken.None);

	private static void Start(
		Func<CancellationToken, Task> taskFactory,
		bool ownsShutdownToken,
		Func<Exception, Task>? onError,
		ILogger? logger,
		CancellationToken callerToken)
	{
		var id = Interlocked.Increment(ref _nextId);

		// When the runner owns the token, the drain signals it. Ownership of the source passes to the in-flight
		// entry, and the completion continuation below disposes it -- a hand-off CA2000 cannot follow.
#pragma warning disable CA2000 // Disposed by the completion continuation that removes the in-flight entry.
		var shutdown = ownsShutdownToken ? new CancellationTokenSource() : null;
#pragma warning restore CA2000
		var workToken = shutdown?.Token ?? callerToken;

		var task = Task.Factory.StartNew(
			async () =>
			{
				try
				{
					await taskFactory(workToken).ConfigureAwait(false);
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
			},
			// NOT the work's token. Handed to StartNew, an already-cancelled token cancels the thread-pool task
			// before the delegate runs, so the work reaches neither the error handler nor the logger and vanishes
			// with no record. The token still flows to the work itself, which observes it and reports.
			CancellationToken.None,
			TaskCreationOptions.None,
			TaskScheduler.Default).Unwrap();

		InFlight[id] = new InFlightWork(task, shutdown);

		// Registered BEFORE the removal is attached, and the removal runs even if the task already finished
		// — ContinueWith on a completed task is scheduled immediately — so the entry cannot be stranded by
		// work that outran its own registration.
		// Captures the id rather than boxing it through the state overload: one small closure per background
		// task, which is nothing beside the Task it is tracking, and it avoids an unboxing cast on a hot-ish
		// path where a wrong cast would strand the entry silently.
		_ = task.ContinueWith(
			_ =>
			{
				if (InFlight.TryRemove(id, out var finished))
				{
					finished.Shutdown?.Dispose();
				}
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	/// <summary>
	/// Awaits the work started through this runner that has not yet finished.
	/// </summary>
	/// <param name="cancellationToken">
	/// The drain budget. A graceful shutdown passes the host's own shutdown token, so the wait is bounded by
	/// <c>HostOptions.ShutdownTimeout</c> rather than by a second timeout this type would have to invent.
	/// </param>
	/// <returns>
	/// How many tasks the drain waited for, and how many were still running when the budget elapsed. The
	/// second is work that is lost; it is returned so the caller can report it rather than drop it silently.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Work accepted WHILE the drain waits — a background handler that starts more background work — is awaited
	/// too: the outstanding set is re-read until it is empty, not snapshotted once.
	/// </para>
	/// <para>
	/// Faults are deliberately not propagated: each task already routes its own exception to the error
	/// handler or logger supplied at start, and a drain that threw on one item would abandon the rest —
	/// re-creating, at shutdown, the abandonment it exists to prevent.
	/// </para>
	/// </remarks>
	internal static async Task<(int Waited, int Abandoned)> DrainAsync(CancellationToken cancellationToken)
	{
		var waited = new HashSet<Task>();

		while (true)
		{
			var pending = Pending();

			if (pending.Length == 0)
			{
				return (waited.Count, 0);
			}

			waited.UnionWith(pending);

			try
			{
				await Task.WhenAll(pending).WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// The budget elapsed. Work still running is lost — the documented limit of in-process background
				// execution — and is returned so the caller reports it instead of dropping it silently. Work that
				// runs on a drain-owned token is told so, so cooperative work can stop cleanly rather than be cut off.
				var abandoned = PendingWork();
				foreach (var work in abandoned)
				{
					SignalShutdown(work.Shutdown);
				}

				waited.UnionWith(abandoned.Select(static work => work.Task));
				return (waited.Count, abandoned.Length);
			}
			catch (Exception)
			{
				// A faulted or cancelled task is already reported at its own start site; keep draining the rest.
			}
		}

		static InFlightWork[] PendingWork() => InFlight.Values.Where(static work => !work.Task.IsCompleted).ToArray();

		static Task[] Pending() => PendingWork().Select(static work => work.Task).ToArray();
	}

	private static void SignalShutdown(CancellationTokenSource? shutdown)
	{
		if (shutdown is null)
		{
			return;
		}

		try
		{
			shutdown.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// The work finished between being read as pending and being signalled; there is nothing to cancel.
		}
		catch (AggregateException)
		{
			// A cancellation callback registered by the work threw. The work reports its own failure; the drain
			// must still signal the rest.
		}
	}

	private sealed record InFlightWork(Task Task, CancellationTokenSource? Shutdown);

	// Source-generated logging methods
	[LoggerMessage(CoreEventId.UnhandledBackgroundException, LogLevel.Error,
		"Unhandled exception in background task.")]
	private static partial void LogUnhandledBackgroundException(this ILogger logger, Exception ex);
}
