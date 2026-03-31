// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.ParallelCatchUp;

/// <summary>
/// Processes a single range of the global event stream during parallel catch-up.
/// </summary>
/// <remarks>
/// <para>
/// Each worker processes events within its assigned <see cref="StreamRange"/>,
/// checkpointing periodically. On failure, the host retries from the last
/// checkpoint (not from range start) with exponential backoff.
/// </para>
/// </remarks>
internal sealed class ParallelCatchUpWorker
{
	private readonly int _workerId;
	private readonly StreamRange _range;
	private readonly IRangeQueryableEventStore _eventStore;
	private readonly IParallelCheckpointStore _checkpointStore;
	private readonly string _projectionName;
	private readonly int _batchSize;
	private readonly int _checkpointInterval;
	private readonly ILogger _logger;

	internal ParallelCatchUpWorker(
		int workerId,
		StreamRange range,
		IRangeQueryableEventStore eventStore,
		IParallelCheckpointStore checkpointStore,
		string projectionName,
		int batchSize,
		int checkpointInterval,
		ILogger logger)
	{
		_workerId = workerId;
		_range = range;
		_eventStore = eventStore;
		_checkpointStore = checkpointStore;
		_projectionName = projectionName;
		_batchSize = batchSize;
		_checkpointInterval = checkpointInterval;
		_logger = logger;
	}

	/// <summary>
	/// Processes events in the assigned range, applying them to projections.
	/// </summary>
	/// <param name="applyEvent">Delegate to apply a single event to projections.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of events processed.</returns>
	internal async Task<long> ProcessAsync(
		Func<StoredEvent, CancellationToken, Task> applyEvent,
		CancellationToken cancellationToken)
	{
		_logger.WorkerProcessingRange(_workerId, _range.StartPosition, _range.EndPosition);

		var eventsProcessed = 0L;
		var lastCheckpointedPosition = _range.StartPosition;

		await foreach (var storedEvent in _eventStore.ReadRangeAsync(
			_range.StartPosition, _range.EndPosition, _batchSize, cancellationToken))
		{
			// Idempotency: skip events at or before last checkpoint (P3 decision)
			if (storedEvent.Version <= lastCheckpointedPosition)
			{
				continue;
			}

			await applyEvent(storedEvent, cancellationToken).ConfigureAwait(false);
			eventsProcessed++;

			// Periodic checkpoint
			if (eventsProcessed % _checkpointInterval == 0)
			{
				await _checkpointStore.SaveWorkerCheckpointAsync(
					_projectionName, _workerId, storedEvent.Version, cancellationToken)
					.ConfigureAwait(false);
				lastCheckpointedPosition = storedEvent.Version;
			}
		}

		// Final checkpoint
		if (eventsProcessed > 0)
		{
			await _checkpointStore.SaveWorkerCheckpointAsync(
				_projectionName, _workerId, _range.EndPosition, cancellationToken)
				.ConfigureAwait(false);
		}

		_logger.WorkerCompletedRange(_workerId, _range.StartPosition, _range.EndPosition, (int)eventsProcessed);

		return eventsProcessed;
	}
}
