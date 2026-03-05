// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Simple in-memory implementation of <see cref="IScheduleStore" /> used for testing and small deployments.
/// </summary>
public sealed class InMemoryScheduleStore : IScheduleStore, IScheduleStoreSignal
{
	private readonly ConcurrentDictionary<Guid, IScheduledMessage> _store = new();
	private readonly Channel<bool> _changeChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
	{
		SingleReader = false,
		SingleWriter = false,
		FullMode = BoundedChannelFullMode.DropOldest,
	});

	/// <inheritdoc />
	public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) =>
		Task.FromResult<IEnumerable<IScheduledMessage>>(CreateSnapshot());

	/// <inheritdoc />
	public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		_ = cancellationToken; // In-memory implementation doesn't need cancellation

		_ = _store.AddOrUpdate(
			message.Id,
			static (_, state) => state,
			static (_, _, state) => state,
			message);
		_changeChannel.Writer.TryWrite(true);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken)
	{
		_ = cancellationToken; // In-memory implementation doesn't need cancellation

		if (_store.TryGetValue(scheduleId, out var msg))
		{
			msg.Enabled = false;
			_changeChannel.Writer.TryWrite(true);
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Adds or updates a scheduled message in the store.
	/// </summary>
	/// <param name="message"> The scheduled message to add or update. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	public Task AddOrUpdateAsync(IScheduledMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		_ = cancellationToken; // In-memory implementation doesn't need cancellation

		_ = _store.AddOrUpdate(
			message.Id,
			static (_, state) => state,
			static (_, _, state) => state,
			message);
		_changeChannel.Writer.TryWrite(true);
		return Task.CompletedTask;
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

	private List<IScheduledMessage> CreateSnapshot() => new(_store.Values);

}
