// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Threading.Channels;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Volatile single-process schedule storage with atomic occurrence admission and detached snapshots.
/// </summary>
internal sealed class InMemoryScheduleStore(TimeProvider? timeProvider = null) : IScheduleExecutionStore, IScheduleStoreSignal
{
	// The store's own clock, which admission judges due-ness against. Optional and defaulted so the many
	// existing parameterless constructions keep working; injectable so a conformance arm can drive a
	// caller whose clock runs ahead of the store's, which is the case the guard exists for.
	private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

	private sealed record Entry(ScheduledMessage Message, Guid Generation,
		ScheduleExecutionLease? Owner = null, bool ProgressCommitted = false);
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
		lock (_gate)
		{
			cancellationToken.ThrowIfCancellationRequested();
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

			// Due-ness is judged against the STORE's clock, not the caller's, exactly as the relational
			// provider does. A single-process store cannot suffer clock skew between hosts, but the
			// contract binds every implementation and the conformance kit checks it here too -- a provider
			// that only honours the rule where skew is impossible teaches the wrong thing to the next one.
			if (!_store.TryGetValue(scheduleId, out var entry) || !entry.Message.Enabled
				|| entry.Generation != generation || entry.Message.NextExecutionUtc != occurrenceUtc
				|| occurrenceUtc > _clock.GetUtcNow() || entry.Owner is not null)
			{
				return Task.FromResult<ScheduleExecutionLease?>(null);
			}
			var lease = new ScheduleExecutionLease(_storeId, scheduleId, generation, occurrenceUtc, Guid.NewGuid());
			_store[scheduleId] = entry with { Owner = lease, ProgressCommitted = false };
			return Task.FromResult<ScheduleExecutionLease?>(lease);
		}
	}

	/// <inheritdoc />
	public Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lease);
		ArgumentNullException.ThrowIfNull(result);
		lock (_gate)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!Matches(lease, out var entry) || entry.ProgressCommitted)
			{
				return Task.FromResult(false);
			}
			var message = ScheduleExecutionSnapshot.Copy(entry.Message);
			message.LastExecutionUtc = result.LastExecutionUtc;
			message.NextExecutionUtc = result.NextExecutionUtc;
			if (result.Disable)
			{
				message.Enabled = false;
			}
			_store[lease.ScheduleId] = entry with { Message = message, ProgressCommitted = true };
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
			&& current.Generation == lease.Generation && current.Owner == lease)
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
