// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.EventSourced;

/// <summary>
/// In-memory implementation of <see cref="IEventSourcedSagaStore"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores saga events in memory using concurrent collections.
/// It is NOT suitable for production use where durability is required.
/// Data is lost when the application restarts.
/// </para>
/// <para>
/// Saga state is rehydrated by replaying all events for a saga instance in order.
/// The store applies events using pattern matching against the known saga event types:
/// <see cref="SagaStateTransitioned"/>, <see cref="SagaStepCompleted"/>, and
/// <see cref="SagaStepFailed"/>.
/// </para>
/// <para>
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/> with lock-based
/// append to ensure event ordering within a saga stream.
/// </para>
/// </remarks>
public sealed partial class InMemoryEventSourcedSagaStore : IEventSourcedSagaStore
{
	private readonly ConcurrentDictionary<string, List<ISagaEvent>> _eventStreams = new(StringComparer.Ordinal);
	private readonly IOptions<EventSourcedSagaOptions> _options;
	private readonly ILogger<InMemoryEventSourcedSagaStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryEventSourcedSagaStore"/> class.
	/// </summary>
	/// <param name="options">The event-sourced saga configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public InMemoryEventSourcedSagaStore(
		IOptions<EventSourcedSagaOptions> options,
		ILogger<InMemoryEventSourcedSagaStore> logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task AppendEventAsync(string sagaId, ISagaEvent sagaEvent, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);
		ArgumentNullException.ThrowIfNull(sagaEvent);

		var streamName = GetStreamName(sagaId);
		var events = _eventStreams.GetOrAdd(streamName, static _ => []);

		// Lock per-stream to preserve event ordering
		lock (events)
		{
			events.Add(sagaEvent);
		}

		Log.EventAppended(_logger, sagaId, sagaEvent.EventType, events.Count);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<SagaState?> RehydrateAsync(string sagaId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);

		var streamName = GetStreamName(sagaId);

		if (!_eventStreams.TryGetValue(streamName, out var events))
		{
			return Task.FromResult<SagaState?>(null);
		}

		ISagaEvent[] snapshot;
		lock (events)
		{
			if (events.Count == 0)
			{
				return Task.FromResult<SagaState?>(null);
			}

			snapshot = [.. events];
		}

		var state = new SagaState
		{
			SagaId = sagaId,
			Status = SagaStatus.Created,
			StartedAt = snapshot[0].OccurredAt.UtcDateTime,
			LastUpdatedAt = snapshot[^1].OccurredAt.UtcDateTime,
		};

		foreach (var sagaEvent in snapshot)
		{
			ApplyEvent(state, sagaEvent);
		}

		Log.StateRehydrated(_logger, sagaId, snapshot.Length);

		return Task.FromResult<SagaState?>(state);
	}

	/// <summary>
	/// Gets the total number of saga event streams.
	/// </summary>
	public int StreamCount => _eventStreams.Count;

	/// <summary>
	/// Gets the total number of events across all streams.
	/// </summary>
	public int TotalEventCount
	{
		get
		{
			var count = 0;
			foreach (var kvp in _eventStreams)
			{
				lock (kvp.Value)
				{
					count += kvp.Value.Count;
				}
			}

			return count;
		}
	}

	/// <summary>
	/// Clears all event streams from the store.
	/// </summary>
	public void Clear() => _eventStreams.Clear();

	private string GetStreamName(string sagaId)
	{
		var prefix = _options.Value.StreamPrefix;
		return $"{prefix}{sagaId}";
	}

	private static void ApplyEvent(SagaState state, ISagaEvent sagaEvent)
	{
		state.LastUpdatedAt = sagaEvent.OccurredAt.UtcDateTime;

		switch (sagaEvent)
		{
			case SagaStateTransitioned transitioned:
				state.Status = transitioned.ToStatus;
				if (transitioned.ToStatus is SagaStatus.Completed
					or SagaStatus.Failed
					or SagaStatus.Compensated
					or SagaStatus.Cancelled)
				{
					state.CompletedAt = transitioned.OccurredAt.UtcDateTime;
				}

				if (transitioned.Reason is not null)
				{
					state.ErrorMessage = transitioned.Reason;
				}

				break;

			case SagaStepCompleted stepCompleted:
				state.CurrentStepIndex = stepCompleted.StepIndex + 1;
				state.StepHistory.Add(new StepExecutionRecord
				{
					StepName = stepCompleted.StepName,
					StepIndex = stepCompleted.StepIndex,
					StartedAt = (stepCompleted.OccurredAt - stepCompleted.Duration).UtcDateTime,
					CompletedAt = stepCompleted.OccurredAt.UtcDateTime,
					IsSuccess = true,
				});
				break;

			case SagaStepFailed stepFailed:
				state.StepHistory.Add(new StepExecutionRecord
				{
					StepName = stepFailed.StepName,
					StepIndex = stepFailed.StepIndex,
					StartedAt = stepFailed.OccurredAt.UtcDateTime,
					CompletedAt = stepFailed.OccurredAt.UtcDateTime,
					IsSuccess = false,
					ErrorMessage = stepFailed.ErrorMessage,
					RetryCount = stepFailed.RetryCount,
				});
				state.ErrorMessage = stepFailed.ErrorMessage;
				break;
		}
	}

	private static partial class Log
	{
		[LoggerMessage(
			EventId = 3920,
			Level = LogLevel.Debug,
			Message = "Appended event '{EventType}' to saga '{SagaId}' (stream length: {StreamLength})")]
		public static partial void EventAppended(
			ILogger logger,
			string sagaId,
			string eventType,
			int streamLength);

		[LoggerMessage(
			EventId = 3921,
			Level = LogLevel.Debug,
			Message = "Rehydrated saga '{SagaId}' from {EventCount} event(s)")]
		public static partial void StateRehydrated(
			ILogger logger,
			string sagaId,
			int eventCount);
	}
}
