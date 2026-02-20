// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Simple in-memory implementation of <see cref="IScheduleStore" /> used for testing and small deployments.
/// </summary>
public sealed class InMemoryScheduleStore : IScheduleStore
{
	private readonly ConcurrentDictionary<Guid, IScheduledMessage> _store = new();

	/// <inheritdoc />
	public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) =>
		Task.FromResult<IEnumerable<IScheduledMessage>>(_store.Values.ToList());

	/// <inheritdoc />
	public async Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		_ = _store.AddOrUpdate(
			message.Id,
			static (_, state) => state,
			static (_, _, state) => state,
			message);
		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken)
	{
		if (_store.TryGetValue(scheduleId, out var msg))
		{
			msg.Enabled = false;
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Adds or updates a scheduled message in the store.
	/// </summary>
	/// <param name="message"> The scheduled message to add or update. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	public async Task AddOrUpdateAsync(IScheduledMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		_ = cancellationToken; // In-memory implementation doesn't need cancellation

		_ = _store.AddOrUpdate(
			message.Id,
			static (_, state) => state,
			static (_, _, state) => state,
			message);
		await Task.CompletedTask.ConfigureAwait(false);
	}
}
