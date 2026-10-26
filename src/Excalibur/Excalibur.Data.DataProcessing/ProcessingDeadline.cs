// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Data.DataProcessing;

/// <summary>Why a claimed task's processing was cancelled by its own clocks.</summary>
internal enum ProcessingDeadlineOutcome
{
	/// <summary>Neither clock fired. Any failure came from the work itself.</summary>
	None = 0,

	/// <summary>The task made no progress for the stall budget.</summary>
	Stalled = 1,

	/// <summary>The task exceeded its absolute cap, whether or not it was progressing.</summary>
	TotalBudgetExceeded = 2,
}

/// <summary>
/// The two clocks bounding one claimed task, and the only thing that can tell which of them fired.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stall clock measures SILENCE, not duration.</b> It is restarted by
/// <see cref="RecordProgress"/> every time the task durably accounts for a record, so a task that is
/// working is never cancelled for working slowly. A single total deadline cannot distinguish a wedged
/// task from a healthy one with a lot to do: under one, there is always a record count above which the
/// task fails regardless of per-record speed, and making each record faster only moves that count.
/// </para>
/// <para>
/// <b>The two clocks are separate sources on purpose.</b> A stall and an exhausted absolute cap are
/// different faults with different fixes, and a single token could only ever report one of them —
/// which is exactly what the previous shape did, sending operators after per-record cost when the
/// actual fault was a budget unsatisfiable at any speed.
/// </para>
/// <para>
/// Extracted from the orchestration manager so this policy has a seam a test can reach: the manager's
/// own entry point takes a concrete claim and cannot be driven without real infrastructure, so the
/// behaviour would otherwise be bindable only by an integration test.
/// </para>
/// </remarks>
internal sealed class ProcessingDeadline : IDisposable
{
	private readonly CancellationTokenSource _stall;
	private readonly CancellationTokenSource _total;
	private readonly CancellationTokenSource _processing;
	private readonly ITimer _stallTimer;
	private readonly ITimer? _totalTimer;
	private readonly TimeSpan _stallTimeout;
	private bool _disposed;

	/// <summary>Arms both clocks for one claimed task.</summary>
	/// <param name="linkedTo">Tokens the work must also observe — the caller's, and the staleness source.</param>
	/// <param name="stallTimeout">How long the task may make no progress.</param>
	/// <param name="totalTimeout">An optional absolute cap, or <see langword="null"/> for none.</param>
	/// <param name="onCallbackError">
	/// Reports a consumer cancellation registration that threw. Cancellation itself still succeeded; this
	/// exists so the throw reaches the caller instead of escaping unobserved on a timer thread.
	/// </param>
	/// <param name="clock">The time source for both clocks. Defaults to <see cref="TimeProvider.System"/>.</param>
	public ProcessingDeadline(
		TimeSpan stallTimeout,
		TimeSpan? totalTimeout,
		Action<Exception> onCallbackError,
		TimeProvider? clock = null,
		params CancellationToken[] linkedTo)
	{
		ArgumentNullException.ThrowIfNull(onCallbackError);
		clock ??= TimeProvider.System;
		_stallTimeout = stallTimeout;

		// EACH CLOCK OWNS ITS OWN SOURCE, and neither is the linked one. Arming the stall clock ON the
		// linked source would make a caller's shutdown indistinguishable from a stall -- the linked token
		// trips for every reason, so asking it WHY would answer "stalled" for an operator pressing Ctrl+C.
		_stall = new CancellationTokenSource();
		_total = new CancellationTokenSource();

		var sources = new CancellationToken[(linkedTo?.Length ?? 0) + 2];
		sources[0] = _stall.Token;
		sources[1] = _total.Token;
		for (var i = 0; i < (linkedTo?.Length ?? 0); i++)
		{
			sources[i + 2] = linkedTo![i];
		}

		_processing = CancellationTokenSource.CreateLinkedTokenSource(sources);

		// CANCEL FROM A TIMER CALLBACK WE OWN, never via CancelAfter.
		//
		// CancelAfter cancels on a TIMER THREAD, and cancellation callbacks run on whichever thread
		// cancels. A consumer's registration is arbitrary code that may throw, and a throw on a timer
		// thread is unobserved: it escapes as an unhandled background exception rather than reaching the
		// caller that can log it and keep the task for retry. Cancelling inside a callback we own puts
		// the try/catch back on the path that invokes the consumer's code.
		_stallTimer = clock.CreateTimer(
			_ => CancelObserving(_stall, onCallbackError), null, stallTimeout, Timeout.InfiniteTimeSpan);

		_totalTimer = totalTimeout is { } cap
			? clock.CreateTimer(_ => CancelObserving(_total, onCallbackError), null, cap, Timeout.InfiniteTimeSpan)
			: null;
	}

	private static void CancelObserving(CancellationTokenSource source, Action<Exception> onCallbackError)
	{
		try
		{
			source.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// The deadline was disposed between this timer firing and here. Nothing to report.
		}
		catch (AggregateException error)
		{
			// One or more CONSUMER registrations threw. The cancellation itself succeeded -- the token is
			// cancelled -- so this is reported, never rethrown onto the thread pool.
			onCallbackError(error);
		}
	}

	/// <summary>Gets the token the work must observe.</summary>
	public CancellationToken Token => _processing.Token;

	/// <summary>Restarts the stall clock, because the task durably accounted for a record.</summary>
	/// <remarks>
	/// A later call REPLACES the pending deadline rather than adding to it, so repeated progress holds the
	/// task open indefinitely. Harmless once cancellation has already been requested.
	/// </remarks>
	public void RecordProgress()
	{
		if (!_disposed)
		{
			_ = _stallTimer.Change(_stallTimeout, Timeout.InfiniteTimeSpan);
		}
	}

	/// <summary>Cancels the pending stall deadline without cancelling the work.</summary>
	/// <remarks>
	/// Called when the processor returns, so a timer firing between that return and the caller's final
	/// check cannot report a task that FINISHED as one that stalled.
	/// </remarks>
	public void Disarm()
	{
		if (!_disposed)
		{
			_ = _stallTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		}
	}

	/// <summary>Says which clock fired, if either.</summary>
	/// <returns>The outcome attributable to these clocks.</returns>
	/// <remarks>
	/// Reads each clock's OWN source, never the linked one. The linked token trips for every reason —
	/// a caller's shutdown, a staleness signal, either clock — so classifying from it would attribute
	/// causes that did not occur. A cancellation from outside is correctly reported as
	/// <see cref="ProcessingDeadlineOutcome.None"/>: it is not a timeout and must not be named as one.
	/// </remarks>
	public ProcessingDeadlineOutcome Classify() =>
		_total.IsCancellationRequested ? ProcessingDeadlineOutcome.TotalBudgetExceeded
		: _stall.IsCancellationRequested ? ProcessingDeadlineOutcome.Stalled
		: ProcessingDeadlineOutcome.None;

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_stallTimer.Dispose();
		_totalTimer?.Dispose();
		_processing.Dispose();
		_total.Dispose();
		_stall.Dispose();
	}
}
