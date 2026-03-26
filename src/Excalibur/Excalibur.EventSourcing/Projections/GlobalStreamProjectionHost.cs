// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Background service that hosts a <see cref="IGlobalStreamProjection{TState}"/>,
/// continuously reading from the global event stream via <see cref="IGlobalStreamQuery"/>
/// and applying events to the projection state.
/// </summary>
/// <typeparam name="TState">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// This host manages the lifecycle of a global stream projection, including:
/// <list type="bullet">
/// <item>Reading events from the global stream in configurable batches</item>
/// <item>Applying events to the projection via <see cref="IGlobalStreamProjection{TState}.ApplyAsync"/></item>
/// <item>Tracking processing position for checkpoint/resume</item>
/// <item>Graceful shutdown with cancellation support</item>
/// </list>
/// </para>
/// <para>
/// Events from the event store are <see cref="StoredEvent"/> records and must be
/// deserialized to <see cref="IDomainEvent"/> before applying to the projection.
/// This host uses <see cref="IEventSerializer"/> for deserialization.
/// </para>
/// </remarks>
public sealed partial class GlobalStreamProjectionHost<TState> : BackgroundService
	where TState : class, new()
{
	private readonly IGlobalStreamQuery _globalStreamQuery;
	private readonly IGlobalStreamProjection<TState> _projection;
	private readonly IEventSerializer _eventSerializer;
	private readonly ISubscriptionCheckpointStore _checkpointStore;
	private readonly ICursorMapStore? _cursorMapStore;
	private readonly IOptions<GlobalStreamProjectionOptions> _options;
	private readonly ILogger<GlobalStreamProjectionHost<TState>> _logger;
	private readonly Diagnostics.ProjectionObservability? _observability;
	private readonly ProjectionHealthState? _healthState;

	private GlobalStreamPosition _currentPosition = GlobalStreamPosition.Start;
	private long _eventsSinceCheckpoint;
	private readonly Dictionary<string, long> _pendingCursorUpdates = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="GlobalStreamProjectionHost{TState}"/> class.
	/// </summary>
	/// <param name="globalStreamQuery">The global stream query for reading events.</param>
	/// <param name="projection">The projection to apply events to.</param>
	/// <param name="eventSerializer">The event serializer for deserializing stored events.</param>
	/// <param name="checkpointStore">The checkpoint store for persisting and restoring position.</param>
	/// <param name="options">The projection host options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="serviceProvider">The service provider for resolving internal observability services.</param>
	/// <param name="cursorMapStore">
	/// Optional cursor map store for multi-stream projections. When provided,
	/// per-stream positions are tracked in addition to the single checkpoint.
	/// Null means single-stream checkpoint mode (unchanged behavior).
	/// </param>
	public GlobalStreamProjectionHost(
		IGlobalStreamQuery globalStreamQuery,
		IGlobalStreamProjection<TState> projection,
		IEventSerializer eventSerializer,
		ISubscriptionCheckpointStore checkpointStore,
		IOptions<GlobalStreamProjectionOptions> options,
		ILogger<GlobalStreamProjectionHost<TState>> logger,
		IServiceProvider serviceProvider,
		ICursorMapStore? cursorMapStore = null)
	{
		_globalStreamQuery = globalStreamQuery ?? throw new ArgumentNullException(nameof(globalStreamQuery));
		_projection = projection ?? throw new ArgumentNullException(nameof(projection));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_cursorMapStore = cursorMapStore;

		// Resolve internal observability types from DI (optional, never fails)
		ArgumentNullException.ThrowIfNull(serviceProvider);
		_observability = serviceProvider.GetService(typeof(Diagnostics.ProjectionObservability))
			as Diagnostics.ProjectionObservability;
		_healthState = serviceProvider.GetService(typeof(ProjectionHealthState))
			as ProjectionHealthState;
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var opts = _options.Value;
		var state = new TState();

		// Restore checkpoint position from last run
		var lastCheckpoint = await _checkpointStore.GetCheckpointAsync(opts.ProjectionName, stoppingToken)
			.ConfigureAwait(false);
		if (lastCheckpoint.HasValue)
		{
			_currentPosition = new GlobalStreamPosition(lastCheckpoint.Value, DateTimeOffset.MinValue);
		}

		LogProjectionHostStarted(opts.ProjectionName);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var events = await _globalStreamQuery.ReadAllAsync(
					_currentPosition,
					opts.BatchSize,
					stoppingToken).ConfigureAwait(false);

				if (events.Count == 0)
				{
					// No new events; wait before polling again
					await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
					continue;
				}

				foreach (var storedEvent in events)
				{
					stoppingToken.ThrowIfCancellationRequested();

					try
					{
						var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
						var domainEvent = _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType);

						if (domainEvent is not null)
						{
							await _projection.ApplyAsync(domainEvent, state, stoppingToken)
								.ConfigureAwait(false);
						}
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						LogEventProcessingError(opts.ProjectionName, storedEvent.EventId, ex);

						// Fire-and-forget observability
						try { _observability?.RecordError(typeof(TState).Name, ex.GetType().Name); } catch { /* swallow */ }

						// Continue processing other events
					}

					// Track per-stream position for cursor map (R27.55)
					if (_cursorMapStore is not null)
					{
						var streamKey = $"{storedEvent.AggregateType}:{storedEvent.AggregateId}";
						_pendingCursorUpdates[streamKey] = storedEvent.Version;
					}

					_eventsSinceCheckpoint++;
				}

				// Advance position
				var lastEvent = events[events.Count - 1];
				_currentPosition = new GlobalStreamPosition(
					lastEvent.Version + 1,
					lastEvent.Timestamp);

				// Checkpoint if needed -- persist position so restarts resume here
				if (_eventsSinceCheckpoint >= opts.CheckpointInterval)
				{
					await _checkpointStore.StoreCheckpointAsync(
						opts.ProjectionName, _currentPosition.Position, stoppingToken)
						.ConfigureAwait(false);

					// Save cursor map after projection apply (phase ordering, R27.55)
					if (_cursorMapStore is not null && _pendingCursorUpdates.Count > 0)
					{
						await _cursorMapStore.SaveCursorMapAsync(
							opts.ProjectionName, _pendingCursorUpdates, stoppingToken)
							.ConfigureAwait(false);
						_pendingCursorUpdates.Clear();
					}

					LogCheckpointSaved(opts.ProjectionName, _currentPosition.Position);
					_eventsSinceCheckpoint = 0;
				}

				// Report lag as current position (consumers compute actual lag externally)
				try { _healthState?.AsyncLag = _currentPosition.Position; } catch { /* swallow */ }

				LogBatchProcessed(opts.ProjectionName, events.Count, _currentPosition.Position);
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogProjectionHostError(opts.ProjectionName, ex);

				// Wait before retrying to avoid tight error loops
				await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
			}
		}

		// Persist final checkpoint position on graceful shutdown
		if (_eventsSinceCheckpoint > 0)
		{
			try
			{
				await _checkpointStore.StoreCheckpointAsync(
					opts.ProjectionName, _currentPosition.Position, CancellationToken.None)
					.ConfigureAwait(false);

				// Save remaining cursor map entries on shutdown
				if (_cursorMapStore is not null && _pendingCursorUpdates.Count > 0)
				{
					await _cursorMapStore.SaveCursorMapAsync(
						opts.ProjectionName, _pendingCursorUpdates, CancellationToken.None)
						.ConfigureAwait(false);
				}

				LogCheckpointSaved(opts.ProjectionName, _currentPosition.Position);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				LogProjectionHostError(opts.ProjectionName, ex);
			}
		}

		LogProjectionHostStopped(opts.ProjectionName);
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.ProjectionStarted, LogLevel.Information,
		"Global stream projection host started for {ProjectionName}")]
	private partial void LogProjectionHostStarted(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionStopped, LogLevel.Information,
		"Global stream projection host stopped for {ProjectionName}")]
	private partial void LogProjectionHostStopped(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionCheckpointSaved, LogLevel.Debug,
		"Checkpoint saved for {ProjectionName} at position {Position}")]
	private partial void LogCheckpointSaved(string projectionName, long position);

	[LoggerMessage(EventSourcingEventId.ProjectionBatchProcessed, LogLevel.Debug,
		"Batch of {EventCount} events processed for {ProjectionName}, position now at {Position}")]
	private partial void LogBatchProcessed(string projectionName, int eventCount, long position);

	[LoggerMessage(EventSourcingEventId.ProjectionError, LogLevel.Error,
		"Error processing event {EventId} in projection {ProjectionName}")]
	private partial void LogEventProcessingError(string projectionName, string eventId, Exception ex);

	[LoggerMessage(EventSourcingEventId.ProjectionBehind, LogLevel.Error,
		"Global stream projection host error for {ProjectionName}")]
	private partial void LogProjectionHostError(string projectionName, Exception ex);

	#endregion
}
