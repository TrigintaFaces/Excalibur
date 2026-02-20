// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// Polling-based implementation of <see cref="IEventSubscription"/> that periodically
/// checks the event store for new events and delivers them to the registered handler.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses a configurable polling interval to detect new events.
/// It tracks the last processed position per subscription and is thread-safe
/// via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </para>
/// <para>
/// For production systems with high throughput, consider provider-specific
/// implementations that use native change notification mechanisms (e.g.,
/// SQL Server Change Tracking, Cosmos DB Change Feed).
/// </para>
/// </remarks>
public sealed partial class EventStoreLiveSubscription : IEventSubscription, IAsyncDisposable
{
	private readonly IEventStore _eventStore;
	private readonly IEventSerializer _eventSerializer;
	private readonly EventSubscriptionOptions _options;
	private readonly ILogger<EventStoreLiveSubscription> _logger;
	private readonly ConcurrentDictionary<string, long> _positions = new();

	private volatile bool _disposed;
	private CancellationTokenSource? _pollingCts;
	private Task? _pollingTask;
	private string? _subscribedStreamId;
	private Func<IReadOnlyList<IDomainEvent>, Task>? _handler;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStoreLiveSubscription"/> class.
	/// </summary>
	/// <param name="eventStore">The event store to poll for new events.</param>
	/// <param name="eventSerializer">The event serializer for deserializing stored events.</param>
	/// <param name="options">The subscription options.</param>
	/// <param name="logger">The logger.</param>
	public EventStoreLiveSubscription(
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		EventSubscriptionOptions options,
		ILogger<EventStoreLiveSubscription> logger)
	{
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task SubscribeAsync(
		string streamId,
		Func<IReadOnlyList<IDomainEvent>, Task> handler,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(streamId);
		ArgumentNullException.ThrowIfNull(handler);
		ObjectDisposedException.ThrowIf(_disposed, this);

		_subscribedStreamId = streamId;
		_handler = handler;

		// Determine starting position
		var startPosition = _options.StartPosition switch
		{
			SubscriptionStartPosition.Beginning => -1L,
			SubscriptionStartPosition.Position => _options.StartPositionValue,
			_ => long.MaxValue // End - will only get new events
		};

		_positions[streamId] = startPosition;

		LogSubscriptionStarted(streamId, _options.StartPosition.ToString());

		// Start the polling loop
		_pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_pollingTask = PollForEventsAsync(_pollingCts.Token);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task UnsubscribeAsync(CancellationToken cancellationToken)
	{
		if (_pollingCts is not null)
		{
			await _pollingCts.CancelAsync().ConfigureAwait(false);
		}

		if (_pollingTask is not null)
		{
			try
			{
				await _pollingTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected during unsubscribe
			}
		}

		if (_subscribedStreamId is not null)
		{
			LogSubscriptionStopped(_subscribedStreamId);
		}

		_subscribedStreamId = null;
		_handler = null;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_pollingCts is not null)
		{
			await _pollingCts.CancelAsync().ConfigureAwait(false);
			_pollingCts.Dispose();
		}

		if (_pollingTask is not null)
		{
			try
			{
				await _pollingTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected during disposal
			}
		}
	}

	private async Task PollForEventsAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_options.PollingInterval, cancellationToken).ConfigureAwait(false);

				if (_subscribedStreamId is null || _handler is null)
				{
					continue;
				}

				var lastPosition = _positions.GetOrAdd(_subscribedStreamId, -1L);

				var events = await _eventStore.LoadAsync(
					_subscribedStreamId,
					_subscribedStreamId,
					lastPosition,
					cancellationToken).ConfigureAwait(false);

				if (events.Count == 0)
				{
					continue;
				}

				// Batch events according to MaxBatchSize
				var domainEvents = DeserializeEvents(events);

				if (domainEvents.Count == 0)
				{
					continue;
				}

				// Deliver in batches
				for (var i = 0; i < domainEvents.Count; i += _options.MaxBatchSize)
				{
					var batchSize = Math.Min(_options.MaxBatchSize, domainEvents.Count - i);
					var batch = domainEvents.GetRange(i, batchSize);

					await _handler(batch).ConfigureAwait(false);
				}

				// Update position to the last processed event
				_positions[_subscribedStreamId] = events[events.Count - 1].Version;

				LogEventsDelivered(_subscribedStreamId, domainEvents.Count);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogPollingError(_subscribedStreamId ?? "unknown", ex);
				// Continue polling after transient errors
			}
		}
	}

	private List<IDomainEvent> DeserializeEvents(IReadOnlyList<StoredEvent> storedEvents)
	{
		var results = new List<IDomainEvent>(storedEvents.Count);

		foreach (var storedEvent in storedEvents)
		{
			try
			{
				var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
				var domainEvent = _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType);

				if (domainEvent is not null)
				{
					results.Add(domainEvent);
				}
			}
			catch (Exception ex)
			{
				LogDeserializationError(storedEvent.EventId, ex);
				// Skip events that cannot be deserialized
			}
		}

		return results;
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.ProjectionStarted, LogLevel.Information,
		"Live subscription started for stream {StreamId} at position {StartPosition}")]
	private partial void LogSubscriptionStarted(string streamId, string startPosition);

	[LoggerMessage(EventSourcingEventId.ProjectionStopped, LogLevel.Information,
		"Live subscription stopped for stream {StreamId}")]
	private partial void LogSubscriptionStopped(string streamId);

	[LoggerMessage(EventSourcingEventId.ProjectionEventProcessed, LogLevel.Debug,
		"Delivered {EventCount} events for stream {StreamId}")]
	private partial void LogEventsDelivered(string streamId, int eventCount);

	[LoggerMessage(EventSourcingEventId.ProjectionError, LogLevel.Error,
		"Error polling for events on stream {StreamId}")]
	private partial void LogPollingError(string streamId, Exception ex);

	[LoggerMessage(EventSourcingEventId.EventSerializationFailed, LogLevel.Warning,
		"Failed to deserialize event {EventId}")]
	private partial void LogDeserializationError(string eventId, Exception ex);

	#endregion
}
