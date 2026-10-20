// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Maintains one occurrence's renewable authority using conservative local elapsed time.</summary>
internal sealed class ScheduleExecutionRenewal : IAsyncDisposable
{
	private static readonly TimeSpan MaximumTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
	private static readonly TimeSpan MinimumTimerDelay = TimeSpan.FromMilliseconds(1);
	private readonly System.Threading.Lock _gate = new();
	private readonly IScheduleExecutionLeaseRenewal _store;
	private readonly ScheduleExecutionLease _lease;
	private readonly TimeProvider _clock;
	private readonly TimeSpan _duration;
	private readonly TimeSpan _interval;
	private readonly Action<Exception> _logError;
	private readonly CancellationTokenSource _lost = new();
	private readonly CancellationTokenSource _stop;
	private readonly CancellationTokenSource _occurrence;
	private readonly CancellationTokenSource _maintenanceCancellation;
	private readonly ITimer _deadline;
	private readonly Task _worker;
	private readonly CancellationTokenRegistration _hostRegistration;
	private Task _lostCancellation = Task.CompletedTask;
	private Task _occurrenceCancellation = Task.CompletedTask;
	private Task _stopCancellation = Task.CompletedTask;
	private Task _lateRequestObservation = Task.CompletedTask;
	private long _grantStarted;
	private bool _ownershipLost;
	private bool _stopped;

	internal ScheduleExecutionRenewal(IScheduleExecutionLeaseRenewal store, ScheduleExecutionLease lease,
		TimeSpan duration, long acquisitionStarted, TimeProvider clock, Action<Exception> logError, CancellationToken hostToken)
	{
		_store = store;
		_lease = lease;
		_clock = clock;
		_duration = duration;
		_interval = ClampTimerDelay(TimeSpan.FromTicks(duration.Ticks / 3));
		_grantStarted = acquisitionStarted;
		_logError = logError;
		_stop = new CancellationTokenSource();
		_occurrence = new CancellationTokenSource();
		_maintenanceCancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, _lost.Token);
		_deadline = clock.CreateTimer(static state => ((ScheduleExecutionRenewal)state!).CheckDeadline(), this,
			Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		var remaining = duration - clock.GetElapsedTime(acquisitionStarted);
		if (remaining <= TimeSpan.Zero) { LoseOwnership(); }
		else { _deadline.Change(ClampTimerDelay(remaining), Timeout.InfiniteTimeSpan); }
		_hostRegistration = hostToken.Register(static state => ((ScheduleExecutionRenewal)state!).RequestHostCancellation(), this);
		_worker = RenewAsync();
	}

	internal CancellationToken Token => _occurrence.Token;

	internal static TimeSpan ValidateDuration(IScheduleExecutionLeaseRenewal store)
	{
		var duration = store.LeaseDuration;
		// System timers truncate sub-millisecond delays. Timer cadence is clamped independently of
		// the provider's full positive grant. Longer grants use bounded timer slices,
		// retaining their full elapsed-time ownership window without restricting provider durations.
		if (duration <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("The schedule store lease duration must be finite and positive.");
		}
		return duration;
	}

	internal void ThrowIfOwnershipLost()
	{
		CheckDeadline();
		lock (_gate)
		{
			// Cancellation callbacks run outside the lock. The latch itself is authoritative even
			// if a concurrent timer callback has not yet delivered cancellation to the handler.
			if (_ownershipLost) { throw new OperationCanceledException("Schedule execution ownership was lost.", Token); }
		}
		Token.ThrowIfCancellationRequested();
	}

	private void CheckDeadline()
	{
		bool expired;
		lock (_gate)
		{
			if (_stopped || _ownershipLost) { return; }
			var remaining = _duration - _clock.GetElapsedTime(_grantStarted);
			expired = remaining <= TimeSpan.Zero;
			if (expired) { RequestLostOwnershipLocked(); }
			if (!expired) { _deadline.Change(ClampTimerDelay(remaining), Timeout.InfiniteTimeSpan); }
		}
	}

	private void LoseOwnership()
	{
		lock (_gate)
		{
			if (_ownershipLost || _stopped) { return; }
			RequestLostOwnershipLocked();
		}
	}

	private void RequestLostOwnershipLocked()
	{
		_ownershipLost = true;
		_lostCancellation = _lost.CancelAsync();
		// Do not propagate consumer callbacks synchronously through the deadline timer or a
		// linked-token callback. Each cancellation request is tracked until its callbacks finish.
		if (!_occurrence.IsCancellationRequested) { _occurrenceCancellation = _occurrence.CancelAsync(); }
	}

	private void RequestHostCancellation()
	{
		lock (_gate)
		{
			if (_stopped) { return; }
			if (!_stop.IsCancellationRequested) { _stopCancellation = _stop.CancelAsync(); }
			if (!_occurrence.IsCancellationRequested) { _occurrenceCancellation = _occurrence.CancelAsync(); }
		}
	}

	private async Task RenewAsync()
	{
		var token = _maintenanceCancellation.Token;
		try
		{
			while (!token.IsCancellationRequested)
			{
				TimeSpan delay;
				lock (_gate)
				{
					if (_stopped || _ownershipLost || _stop.IsCancellationRequested) { return; }
					delay = _interval - _clock.GetElapsedTime(_grantStarted);
				}
				if (delay > TimeSpan.Zero) { await Task.Delay(delay, _clock, token).ConfigureAwait(false); }
				var started = _clock.GetTimestamp();
				lock (_gate)
				{
					// Reserve maintenance before leaving the guard. A continuation queued before
					// stop cannot initiate a new request after stop; only already-reserved work may finish.
					if (_stopped || _ownershipLost || _stop.IsCancellationRequested || token.IsCancellationRequested) { return; }
					if (_clock.GetElapsedTime(_grantStarted) >= _duration)
					{
						RequestLostOwnershipLocked();
						return;
					}
				}
				var request = _store.TryRenewExecutionAsync(_lease, token);
				bool renewed;
				// The independent deadline watchdog cancels this wait at the conservative grant expiry,
				// even when the provider ignores cancellation or the grant spans multiple timer slices.
				try { renewed = await request.WaitAsync(token).ConfigureAwait(false); }
				catch (OperationCanceledException) when (token.IsCancellationRequested)
				{
					// WaitAsync bounds an uncooperative provider. Observe its eventual fault without
					// allowing a late successful completion to restore this occurrence's authority.
					var observation = ObserveLateRequestAsync(request);
					lock (_gate) { _lateRequestObservation = observation; }
					throw;
				}
				bool valid;
				lock (_gate)
				{
					valid = renewed && !_ownershipLost && !_stopped
						&& _clock.GetElapsedTime(_grantStarted) < _duration
						&& _clock.GetElapsedTime(started) < _duration;
					if (valid)
					{
						// Grant expiry is authoritative at the provider; RPC start gives a conservative
						// local lower bound without comparing clocks between machines.
						_grantStarted = started;
						_deadline.Change(ClampTimerDelay(_duration - _clock.GetElapsedTime(started)), Timeout.InfiniteTimeSpan);
					}
				}
				if (!valid) { LoseOwnership(); return; }
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { }
		catch (Exception ex) { _logError(ex); LoseOwnership(); }
	}

	private async Task ObserveLateRequestAsync(Task<bool> request)
	{
		try { _ = await request.ConfigureAwait(false); }
		catch (OperationCanceledException) { }
		catch (Exception ex) { _logError(ex); }
	}

	private static TimeSpan ClampTimerDelay(TimeSpan delay) => delay > MaximumTimerDelay ? MaximumTimerDelay
		: delay < MinimumTimerDelay ? MinimumTimerDelay : delay;

	public async ValueTask DisposeAsync()
	{
		Task callbacks;
		lock (_gate)
		{
			_stopped = true;
			if (!_stop.IsCancellationRequested) { _stopCancellation = _stop.CancelAsync(); }
			callbacks = Task.WhenAll(_lostCancellation, _occurrenceCancellation, _stopCancellation);
		}
		var cleanup = Task.WhenAll(_deadline.DisposeAsync().AsTask(), _hostRegistration.DisposeAsync().AsTask(),
			CompleteMaintenanceAsync(), callbacks);
		try { await cleanup.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
		catch (TimeoutException ex)
		{
			_logError(ex);
			// Arbitrarily blocking consumer cancellation callbacks cannot be terminated. Retain
			// their sources until callbacks finish, observe late faults, and allow authority release.
			_ = CompleteCleanupAsync(cleanup);
			return;
		}
		catch (Exception ex) { _logError(ex); }
		DisposeSources();
	}

	private async Task CompleteCleanupAsync(Task cleanup)
	{
		try { await cleanup.ConfigureAwait(false); }
		catch (Exception ex) { _logError(ex); }
		finally { DisposeSources(); }
	}

	private async Task CompleteMaintenanceAsync()
	{
		await _worker.ConfigureAwait(false);
		Task observation;
		// The worker can publish its abandoned request after DisposeAsync starts. Capture it
		// only after the worker settles, so cleanup never disposes tokens beneath the provider.
		lock (_gate) { observation = _lateRequestObservation; }
		await observation.ConfigureAwait(false);
	}

	private void DisposeSources()
	{
		_maintenanceCancellation.Dispose();
		_occurrence.Dispose();
		_stop.Dispose();
		_lost.Dispose();
	}
}
