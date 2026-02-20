// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Migration;

/// <summary>
/// Default implementation of <see cref="IEventBatchMigrator"/> that reads events from
/// an <see cref="IEventStore"/> source, applies optional filters and transforms, and
/// writes to a target stream.
/// </summary>
/// <remarks>
/// <para>
/// This migrator processes events in configurable batches and supports dry-run mode
/// for validation without writing. It uses the same <see cref="IEventStore"/> for
/// both source and target by default.
/// </para>
/// </remarks>
public sealed partial class EventBatchMigrator : IEventBatchMigrator
{
	private readonly IEventStore _eventStore;
	private readonly IOptions<MigrationOptions> _options;
	private readonly ILogger<EventBatchMigrator> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventBatchMigrator"/> class.
	/// </summary>
	/// <param name="eventStore">The event store for reading and writing events.</param>
	/// <param name="options">The migration options.</param>
	/// <param name="logger">The logger.</param>
	public EventBatchMigrator(
		IEventStore eventStore,
		IOptions<MigrationOptions> options,
		ILogger<EventBatchMigrator> logger)
	{
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<EventMigrationResult> MigrateAsync(MigrationPlan plan, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(plan);

		var opts = _options.Value;
		var eventsMigrated = 0L;
		var eventsSkipped = 0L;
		var errors = new List<string>();

		LogMigrationStarted(plan.SourceStream, plan.TargetStream, opts.DryRun);

		try
		{
			// Load all events from the source stream
			var sourceEvents = await _eventStore.LoadAsync(
				plan.SourceStream, plan.SourceStream, cancellationToken).ConfigureAwait(false);

			var eventsToProcess = new List<StoredEvent>(opts.BatchSize);

			foreach (var storedEvent in sourceEvents)
			{
				cancellationToken.ThrowIfCancellationRequested();

				// Apply max events limit (count includes events queued in the current batch)
				if (opts.MaxEvents > 0 && (eventsMigrated + eventsToProcess.Count) >= opts.MaxEvents)
				{
					break;
				}

				// Apply event filter
				if (plan.EventFilter is not null && !plan.EventFilter(storedEvent))
				{
					eventsSkipped++;
					continue;
				}

				// Apply transform
				var targetEvent = plan.TransformFunc is not null
					? plan.TransformFunc(storedEvent)
					: storedEvent;

				eventsToProcess.Add(targetEvent);

				// Process batch
				if (eventsToProcess.Count >= opts.BatchSize)
				{
					if (!opts.DryRun)
					{
						await WriteBatchAsync(plan.TargetStream, eventsToProcess, errors, cancellationToken)
							.ConfigureAwait(false);
					}

					eventsMigrated += eventsToProcess.Count;
					LogBatchProcessed(eventsToProcess.Count, eventsMigrated);
					eventsToProcess.Clear();
				}
			}

			// Process remaining events
			if (eventsToProcess.Count > 0)
			{
				if (!opts.DryRun)
				{
					await WriteBatchAsync(plan.TargetStream, eventsToProcess, errors, cancellationToken)
						.ConfigureAwait(false);
				}

				eventsMigrated += eventsToProcess.Count;
				LogBatchProcessed(eventsToProcess.Count, eventsMigrated);
			}

			LogMigrationCompleted(eventsMigrated, eventsSkipped);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogMigrationFailed(plan.SourceStream, ex);

			if (!opts.ContinueOnError)
			{
				throw;
			}

			errors.Add(ex.Message);
		}

		return new EventMigrationResult(
			eventsMigrated,
			eventsSkipped,
			StreamsMigrated: 1,
			opts.DryRun,
			errors);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<MigrationPlan>> CreatePlanAsync(
		MigrationOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Create a single plan based on the options.
		// In a more advanced implementation, this would discover matching streams
		// based on the SourceStreamPattern.
		var plans = new List<MigrationPlan>();

		if (!string.IsNullOrEmpty(options.SourceStreamPattern))
		{
			var targetStream = !string.IsNullOrEmpty(options.TargetStreamPrefix)
				? $"{options.TargetStreamPrefix}{options.SourceStreamPattern}"
				: options.SourceStreamPattern;

			plans.Add(new MigrationPlan(
				SourceStream: options.SourceStreamPattern,
				TargetStream: targetStream));
		}

		return Task.FromResult<IReadOnlyList<MigrationPlan>>(plans);
	}

	private async Task WriteBatchAsync(
		string targetStream,
		List<StoredEvent> events,
		List<string> errors,
		CancellationToken cancellationToken)
	{
		try
		{
			// Convert StoredEvents to domain events for AppendAsync
			// Since we're migrating raw stored events, we create wrapper domain events
			var domainEvents = new List<IDomainEvent>(events.Count);
			foreach (var storedEvent in events)
			{
				domainEvents.Add(new MigrationDomainEvent(storedEvent));
			}

			var result = await _eventStore.AppendAsync(
				targetStream,
				targetStream,
				domainEvents,
				expectedVersion: -1,
				cancellationToken).ConfigureAwait(false);

			if (!result.Success)
			{
				var errorMessage = $"Failed to write batch to {targetStream}: {result.ErrorMessage}";
				errors.Add(errorMessage);
				LogWriteBatchFailed(targetStream, result.ErrorMessage ?? "Unknown error");
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			errors.Add($"Exception writing to {targetStream}: {ex.Message}");
			LogWriteBatchFailed(targetStream, ex.Message);

			if (!_options.Value.ContinueOnError)
			{
				throw;
			}
		}
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.EventMigrationStarted, LogLevel.Information,
		"Event batch migration started: Source={SourceStream}, Target={TargetStream}, DryRun={DryRun}")]
	private partial void LogMigrationStarted(string sourceStream, string targetStream, bool dryRun);

	[LoggerMessage(EventSourcingEventId.EventMigrationCompleted, LogLevel.Information,
		"Event batch migration completed: {EventsMigrated} migrated, {EventsSkipped} skipped")]
	private partial void LogMigrationCompleted(long eventsMigrated, long eventsSkipped);

	[LoggerMessage(EventSourcingEventId.EventMigrationFailed, LogLevel.Error,
		"Event batch migration failed for stream {SourceStream}")]
	private partial void LogMigrationFailed(string sourceStream, Exception ex);

	[LoggerMessage(EventSourcingEventId.EventMigrated, LogLevel.Debug,
		"Batch processed: {BatchCount} events, {TotalMigrated} total")]
	private partial void LogBatchProcessed(int batchCount, long totalMigrated);

	[LoggerMessage(EventSourcingEventId.SchemaVersionUpdated, LogLevel.Warning,
		"Failed to write batch to target stream {TargetStream}: {Error}")]
	private partial void LogWriteBatchFailed(string targetStream, string error);

	#endregion

	/// <summary>
	/// Internal domain event wrapper for stored events during migration.
	/// </summary>
	private sealed class MigrationDomainEvent : IDomainEvent
	{
		private readonly StoredEvent _storedEvent;

		public MigrationDomainEvent(StoredEvent storedEvent) => _storedEvent = storedEvent;

		public string EventId => _storedEvent.EventId;
		public string AggregateId => _storedEvent.AggregateId;
		public long Version => _storedEvent.Version;
		public DateTimeOffset OccurredAt => _storedEvent.Timestamp;
		public string EventType => _storedEvent.EventType;
		public IDictionary<string, object>? Metadata => null;
	}
}
