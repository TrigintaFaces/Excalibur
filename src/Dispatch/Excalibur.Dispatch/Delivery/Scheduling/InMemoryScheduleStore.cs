// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Threading.Channels;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Volatile single-process schedule storage with atomic occurrence admission and detached snapshots.
/// </summary>
internal sealed partial class InMemoryScheduleStore(TimeProvider? timeProvider = null) : IScheduleExecutionFailureStore, IScheduleStoreSignal
{
	// The store's own clock, which admission judges due-ness against. Optional and defaulted so the many
	// existing parameterless constructions keep working; injectable so a conformance arm can drive a
	// caller whose clock runs ahead of the store's, which is the case the guard exists for.
	private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

	private sealed record Entry(ScheduledMessage Message, Guid Generation,
		ScheduleExecutionLease? Owner = null, bool ProgressCommitted = false, int AdmissionAttempts = 0, int? MaxAdmissionAttempts = null, Guid? FailureOwner = null, ScheduleOccurrenceFailure? FailureRequest = null, bool RecoveryLineage = false);
	private readonly System.Threading.Lock _gate = new();
	private readonly Guid _storeId = Guid.NewGuid();
	private readonly Dictionary<Guid, Entry> _store = [];
	private readonly Channel<bool> _changeChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
	{
		SingleReader = false,
		SingleWriter = false,
		FullMode = BoundedChannelFullMode.DropOldest,
	});

	/// <inheritdoc />
	public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult<IEnumerable<IScheduledMessage>>(
				_store.Values.Select(e => (IScheduledMessage)ScheduleExecutionSnapshot.Copy(e.Message)).ToArray());
		}
	}

	/// <inheritdoc />
	public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		cancellationToken.ThrowIfCancellationRequested();
		var owned = ScheduleExecutionSnapshot.Copy(message);
		_ = CronRecurrenceValidation.Validate(owned);
		lock (_gate)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (_store.TryGetValue(owned.Id, out var current) && (current.MaxAdmissionAttempts is not null || HasUnresolvedFailure(owned.Id, current)))
			{
				throw new InvalidOperationException("A captured occurrence is unsettled; retain its work and resolve it before replacing this schedule.");
			}
			_store[owned.Id] = new Entry(owned, Guid.NewGuid());
		}
		_changeChannel.Writer.TryWrite(true);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (_store.TryGetValue(scheduleId, out var entry))
			{
				if (entry.MaxAdmissionAttempts is not null || HasUnresolvedFailure(scheduleId, entry))
				{
					QuarantineForAdministrativeCompletion(scheduleId, entry);
					return Task.CompletedTask;
				}
				var message = ScheduleExecutionSnapshot.Copy(entry.Message);
				message.Enabled = false;
				_store[scheduleId] = new Entry(message, Guid.NewGuid());
				_changeChannel.Writer.TryWrite(true);
			}
		}
		return Task.CompletedTask;
	}

	/// <summary>Adds or administratively replaces a schedule with a fresh generation.</summary>
	/// <param name="message">Message to copy.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The storage operation.</returns>
	public Task AddOrUpdateAsync(IScheduledMessage message, CancellationToken cancellationToken) => StoreAsync(message, cancellationToken);

	/// <inheritdoc />
	public Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult<IReadOnlyList<ScheduleExecutionSnapshot>>(
				_store.Values.Select(e => new ScheduleExecutionSnapshot(e.Generation, e.Message)).ToArray());
		}
	}

	/// <inheritdoc />
	public Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid scheduleId, Guid generation,
		DateTimeOffset occurrenceUtc, CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(AcquireUnderLock(scheduleId, generation, occurrenceUtc));
		}
	}

	private ScheduleExecutionLease? AcquireUnderLock(Guid scheduleId, Guid generation, DateTimeOffset occurrenceUtc)
	{
		if (!CanAcquire(scheduleId, generation, occurrenceUtc, out var entry)) { return null; }
		if (entry.MaxAdmissionAttempts is null) { _ = CronRecurrenceValidation.Validate(entry.Message); }
		return GrantUnderLock(scheduleId, generation, occurrenceUtc, entry);
	}

	private ScheduleExecutionLease GrantUnderLock(Guid scheduleId, Guid generation, DateTimeOffset occurrenceUtc, Entry entry)
	{
		var attempt = checked(entry.AdmissionAttempts + 1);
		var lease = new ScheduleExecutionLease(_storeId, scheduleId, generation, occurrenceUtc, Guid.NewGuid())
		{
			AdmissionAttempt = attempt,
		};
		_store[scheduleId] = entry with { Owner = lease, ProgressCommitted = false, AdmissionAttempts = attempt };
		return lease;
	}

	private bool CanAcquire(Guid scheduleId, Guid generation, DateTimeOffset occurrenceUtc, out Entry entry)
	{
		if (_store.TryGetValue(scheduleId, out var current) && current.Message.Enabled && current.Generation == generation
			&& current.Message.NextExecutionUtc == occurrenceUtc && occurrenceUtc <= _clock.GetUtcNow() && current.Owner is null)
		{
			entry = current;
			return true;
		}
		entry = null!;
		return false;
	}

	/// <inheritdoc />
	public Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result,
		CancellationToken cancellationToken) => UpdateProgressAsync(lease, result, disposition: null, cancellationToken);

	private Task<bool> UpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result,
		ScheduleExecutionCompletionDisposition? disposition, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lease);
		ArgumentNullException.ThrowIfNull(result);
		lock (_gate)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!Matches(lease, out var entry) || entry.ProgressCommitted
				|| (entry.MaxAdmissionAttempts is not null && disposition is null))
			{
				return Task.FromResult(false);
			}
			if (disposition is { } completion)
			{
				if (!Enum.IsDefined(completion) || entry.MaxAdmissionAttempts is not { } budget
					|| entry.AdmissionAttempts > budget
					|| (!result.Disable && (result.NextExecutionUtc is null || result.NextExecutionUtc <= lease.OccurrenceUtc)))
				{
					return Task.FromResult(false);
				}
				if (completion != ScheduleExecutionCompletionDisposition.Fulfilled
					&& (!PolicyDispositionAllowed(lease.ScheduleId, entry, lease.OccurrenceUtc)
						|| result.LastExecutionUtc != entry.Message.LastExecutionUtc
						|| (completion == ScheduleExecutionCompletionDisposition.PolicyDisabled && !result.Disable)
						|| (completion == ScheduleExecutionCompletionDisposition.PolicySkipped && result.Disable)))
				{
					return Task.FromResult(false);
				}
			}
			var message = ScheduleExecutionSnapshot.Copy(entry.Message);
			message.LastExecutionUtc = result.LastExecutionUtc;
			message.NextExecutionUtc = result.NextExecutionUtc;
			if (result.Disable)
			{
				message.Enabled = false;
			}
			if (disposition == ScheduleExecutionCompletionDisposition.Fulfilled)
			{
				var key = (lease.ScheduleId, lease.Generation, lease.OccurrenceUtc);
				if (_failures.TryGetValue(key, out var failure) && failure.Disposition == ScheduleFailureDisposition.Quarantined)
				{
					return Task.FromResult(false);
				}
				_failures.Remove(key);
			}
			_store[lease.ScheduleId] = entry with { Message = message, ProgressCommitted = true, AdmissionAttempts = 0,
				MaxAdmissionAttempts = null, FailureOwner = null, FailureRequest = null, RecoveryLineage = false };
			_changeChannel.Writer.TryWrite(true);
			return Task.FromResult(true);
		}
	}

	/// <inheritdoc />
	public Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lease);
		lock (_gate)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!Matches(lease, out var entry))
			{
				return Task.FromResult(false);
			}
			_store[lease.ScheduleId] = entry with { Owner = null };
			_changeChannel.Writer.TryWrite(true);
			return Task.FromResult(true);
		}
	}

	private bool Matches(ScheduleExecutionLease lease, out Entry entry)
	{
		if (lease.StoreId == _storeId && _store.TryGetValue(lease.ScheduleId, out var current)
			&& current.Generation == lease.Generation && current.Owner is { } owner
			&& owner.StoreId == lease.StoreId && owner.ScheduleId == lease.ScheduleId
			&& owner.Generation == lease.Generation && owner.OccurrenceUtc == lease.OccurrenceUtc
			&& owner.OwnerToken == lease.OwnerToken)
		{
			entry = current;
			return true;
		}
		entry = null!;
		return false;
	}

	/// <inheritdoc />
	public async ValueTask WaitForChangeAsync(TimeSpan timeout, CancellationToken cancellationToken)
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(timeout);

		try
		{
			_ = await _changeChannel.Reader.ReadAsync(timeoutCts.Token).ConfigureAwait(false);
			while (_changeChannel.Reader.TryRead(out _))
			{
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			// Timed out waiting for a signal; caller falls back to polling.
		}
	}


}
