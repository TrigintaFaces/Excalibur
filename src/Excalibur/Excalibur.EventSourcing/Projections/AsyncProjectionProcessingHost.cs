// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Background service that processes all <see cref="ProjectionMode.Async"/> projections
/// by polling the global event stream and dispatching events to registered projection handlers.
/// </summary>
/// <remarks>
/// <para>
/// Cooperating instances of this host write persisted projections using positioned compare-and-set writes. It polls the global stream and applies
/// events in position order, which is what lets a projection row's position mean what it claims.
/// Projections were once also folded during <c>SaveAsync</c>; that second, uncoordinated writer could
/// lose a fold and is removed. A caller needing read-after-write passes the session token
/// <c>SaveAsync</c> returns to <c>IProjectionSessionReader</c>.
/// </para>
/// <para>
/// Register via <c>es.EnableProjectionProcessing()</c> on <see cref="DependencyInjection.IEventSourcingBuilder"/>.
/// Requires an <see cref="IGlobalStreamQuery"/> implementation (e.g., from <c>UseSqlServer()</c>).
/// </para>
/// </remarks>
internal sealed partial class AsyncProjectionProcessingHost : BackgroundService, IProjectionProcessor
{
	private readonly IProjectionRegistry _registry;
	private readonly IEventSerializer _eventSerializer;
	private readonly ISubscriptionCheckpointStore _checkpointStore;
	private readonly IOptions<GlobalStreamProjectionOptions> _options;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<AsyncProjectionProcessingHost> _logger;
	private readonly ProjectionObservability? _observability;
	private readonly ProjectionHealthState? _healthState;
	private readonly EventNotificationOptions _notificationOptions;
	private readonly TimeProvider _timeProvider;

	// One finite page per lease. FIFO acquisition lets request-driven work make progress
	// while the background reader remains live; the lease is never held during idle polling.
	private readonly ConcurrencyLimiter _pageGate = new(new ConcurrencyLimiterOptions
	{
		PermitLimit = 1,
		QueueLimit = int.MaxValue,
		QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
	});

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncProjectionProcessingHost"/> class.
	/// </summary>
	public AsyncProjectionProcessingHost(
		IProjectionRegistry registry,
		IEventSerializer eventSerializer,
		ISubscriptionCheckpointStore checkpointStore,
		IOptions<GlobalStreamProjectionOptions> options,
		IServiceProvider serviceProvider,
		ILogger<AsyncProjectionProcessingHost> logger,
		IOptions<EventNotificationOptions>? notificationOptions = null,
		TimeProvider? timeProvider = null)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_notificationOptions = notificationOptions?.Value ?? new EventNotificationOptions();
		_timeProvider = timeProvider ?? TimeProvider.System;

		_observability = serviceProvider.GetService(typeof(ProjectionObservability)) as ProjectionObservability;
		_healthState = serviceProvider.GetService(typeof(ProjectionHealthState)) as ProjectionHealthState;
	}

	/// <inheritdoc />
	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		// .NET 10 runs all ExecuteAsync code in the background. Definition mismatches
		// must prevent the application from serving traffic, so validate here explicitly.
		_ = _serviceProvider.GetRequiredService<IGlobalStreamQuery>();
		ValidateSubscriptionOptions(_options.Value);
		_ = await BindDefinitionAsync(_registry.GetByMode(ProjectionMode.Async), _options.Value, cancellationToken)
			.ConfigureAwait(false);
		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<ProjectionProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken)
	{
		long applied = 0;
		try
		{
			var query = _serviceProvider.GetRequiredService<IGlobalStreamQuery>();
			// A finite target: concurrent producers cannot keep a request draining forever.
			var target = await query.GetHeadPositionAsync(cancellationToken).ConfigureAwait(false);
			while (true)
			{
				var page = await ProcessPageAsync(query, cancellationToken).ConfigureAwait(false);
				applied += page.Applied;
				cancellationToken.ThrowIfCancellationRequested();
				if (page.Outcome == ProjectionProcessingOutcome.Halted)
				{
					return new ProjectionProcessingResult(applied, page.Outcome);
				}
				if (page.Position >= target)
				{
					return new ProjectionProcessingResult(applied, ProjectionProcessingOutcome.CaughtUp);
				}
				if (page.Applied == 0 && page.Outcome != ProjectionProcessingOutcome.Contested)
				{
					// A temporarily unreadable prefix is pending, never falsely caught up.
					return new ProjectionProcessingResult(applied, ProjectionProcessingOutcome.Halted);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new ProjectionProcessingResult(applied, ProjectionProcessingOutcome.Cancelled);
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var query = _serviceProvider.GetRequiredService<IGlobalStreamQuery>();
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var page = await ProcessPageAsync(query, stoppingToken).ConfigureAwait(false);
				if (page.Applied == 0 || page.Outcome == ProjectionProcessingOutcome.Halted)
				{
					await Task.Delay(_options.Value.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogAsyncProjectionHostError(ex);
				_healthState?.RecordHalt(_options.Value.ProjectionName);
				await Task.Delay(_options.Value.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
			}
		}
	}

	private async Task<PageResult> ProcessPageAsync(IGlobalStreamQuery query, CancellationToken cancellationToken)
	{
		using var lease = await _pageGate.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
		if (!lease.IsAcquired)
		{
			throw new InvalidOperationException("The projection page queue is full or disposed.");
		}

		var registrations = _registry.GetByMode(ProjectionMode.Async);
		var options = _options.Value;
		var name = options.ProjectionName;
		ValidateSubscriptionOptions(options);

		var destinations = await BindDefinitionAsync(registrations, options, cancellationToken).ConfigureAwait(false);

		var expected = await _checkpointStore.GetCheckpointAsync(name, cancellationToken).ConfigureAwait(false);
		var position = expected ?? 0;
		if (registrations.Count == 0)
		{
			return new PageResult(0, position, ProjectionProcessingOutcome.CaughtUp);
		}
		var events = await query.ReadAllAsync(
			new GlobalStreamPosition(position, DateTimeOffset.MinValue), options.BatchSize, cancellationToken)
			.ConfigureAwait(false);
		var batch = new List<ProjectionEvent>(events.Count);
		StoredEvent? last = null;
		var processed = 0;
		var poison = false;
		foreach (var stored in events)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (stored.GlobalPosition <= (last?.GlobalPosition ?? position))
			{
				throw new InvalidOperationException("The global stream returned an unordered or non-advancing page.");
			}
			if (!ErasedEventMarker.IsErased(stored.EventType))
			{
				try
				{
					batch.Add(new ProjectionEvent(DeserializeOrThrow(stored), stored.AggregateId, stored.GlobalPosition));
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					LogAsyncProjectionEventError(stored.EventId, ex);
					RecordPoison(name, ex);
					poison = true;
					break;
				}
			}
			last = stored;
			processed++;
		}
		if (batch.Count > 0)
		{
			var context = new EventNotificationContext(last!.AggregateId, last.AggregateType,
				last.Version, last.Timestamp, IsReplay: false, GlobalPosition: last.GlobalPosition);
			var faulted = await DispatchToProjectionsAsync(registrations, batch, context, destinations, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			if (faulted)
			{
				_healthState?.RecordHalt(name);
				return new PageResult(0, position, ProjectionProcessingOutcome.Halted);
			}
		}
		// A handler that returns after catching cancellation cannot acknowledge the page.
		// Every page starts from durable state; a crash or failed CAS simply redelivers it.
		cancellationToken.ThrowIfCancellationRequested();
		if (last is not null)
		{
			try
			{
				var result = await _checkpointStore.AdvanceCheckpointAsync(
					name, expected, last.GlobalPosition, cancellationToken).ConfigureAwait(false);
				if (result == CheckpointAdvanceOutcome.Superseded)
				{
					var winner = await _checkpointStore.GetCheckpointAsync(name, cancellationToken).ConfigureAwait(false);
					return new PageResult(processed, winner ?? position, ProjectionProcessingOutcome.Contested);
				}
				position = last.GlobalPosition;
				_healthState?.RecordApplied(name, position);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				LogAsyncProjectionCheckpointWriteFailed(ex);
				_healthState?.RecordHalt(name);
				return new PageResult(0, position, ProjectionProcessingOutcome.Halted);
			}
		}
		if (poison)
		{
			_healthState?.RecordHalt(name);
		}
		return new PageResult(processed, position,
			poison ? ProjectionProcessingOutcome.Halted : ProjectionProcessingOutcome.CaughtUp);
	}

	private static void ValidateSubscriptionOptions(GlobalStreamProjectionOptions options)
	{
		// SECOND LINE, NOT THE FIRST. GlobalStreamProjectionOptionsValidator refuses these at startup
		// through ValidateOnStart, which is where a consumer can act on them. This check remains for
		// options constructed directly rather than resolved from the container -- a test, or a host
		// driving IProjectionProcessor by hand -- and it names WHICH option is blank, because naming
		// both sends the reader to check one that was never wrong.
		if (string.IsNullOrWhiteSpace(options.SubscriptionScope))
		{
			throw new InvalidOperationException(
				$"Projection processing requires {nameof(GlobalStreamProjectionOptions.SubscriptionScope)}, "
				+ "which has no default. Set it to a stable name identifying this event source and "
				+ "projection destination together; every cooperating process must use the same value.");
		}

		if (string.IsNullOrWhiteSpace(options.SubscriptionRevision))
		{
			throw new InvalidOperationException(
				$"Projection processing requires a non-blank "
				+ $"{nameof(GlobalStreamProjectionOptions.SubscriptionRevision)}. It defaults to \"1\", so a "
				+ "blank value means something explicitly set it.");
		}

	}

	private async Task<IReadOnlyDictionary<Type, string?>> BindDefinitionAsync(
		IReadOnlyList<ProjectionRegistration> registrations,
		GlobalStreamProjectionOptions options, CancellationToken cancellationToken)
	{
		if (registrations.Any(r => r.InlineApply is null))
		{
			throw new InvalidOperationException("Every asynchronous projection must have a bound apply delegate.");
		}
		var destinations = new Dictionary<Type, string?>();
		await using (var scope = _serviceProvider.CreateAsyncScope())
		{
			var types = registrations.Select(r => r.ProjectionType).ToHashSet();
			foreach (var probe in scope.ServiceProvider.GetServices<IPositionedProjectionProbe>())
			{
				if (!types.Contains(probe.ProjectionType))
				{
					continue;
				}
				if (!probe.IsPositioned(scope.ServiceProvider))
				{
					throw new InvalidOperationException($"Asynchronous projection '{probe.ProjectionTypeName}' requires positioned writes.");
				}
				destinations[probe.ProjectionType] = probe.GetDestinationIdentity(scope.ServiceProvider);
			}
		}

		// Immutable first-writer binding. A rolling deployment with a different workset must
		// fail BEFORE effects, rather than acknowledge work the other deployment never performed.
		var descriptor = new StringBuilder();
		AppendDescriptorPart(descriptor, options.SubscriptionScope);
		AppendDescriptorPart(descriptor, options.SubscriptionRevision);
		foreach (var registration in registrations.OrderBy(
			r => r.ProjectionType.Assembly.GetName().Name, StringComparer.Ordinal)
			.ThenBy(r => r.ProjectionType.FullName, StringComparer.Ordinal))
		{
			AppendDescriptorPart(descriptor, registration.ProjectionType.Assembly.GetName().Name ?? string.Empty);
			AppendDescriptorPart(descriptor, registration.ProjectionType.FullName ?? registration.ProjectionType.Name);
			AppendDescriptorPart(descriptor, destinations.GetValueOrDefault(registration.ProjectionType) ?? string.Empty);
		}

		var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(descriptor.ToString())));
		await _checkpointStore.BindSubscriptionAsync(options.ProjectionName, hash, cancellationToken).ConfigureAwait(false);
		return destinations;
	}

	private static void AppendDescriptorPart(StringBuilder builder, string part) =>
		builder.Append(part.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(part);

	private readonly record struct PageResult(long Applied, long Position, ProjectionProcessingOutcome Outcome);

	/// <summary>
	/// Resolves and deserializes a stored event to a domain event, throwing on a null result rather
	/// than silently dropping it (a null deserialization is a poison event, not an empty batch).
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; projection host requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	private IDomainEvent DeserializeOrThrow(StoredEvent storedEvent)
	{
		var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
		return _eventSerializer.DeserializeEvent(StoredEventPayload.Require(storedEvent), eventType)
			?? throw new InvalidOperationException(
				$"Event '{storedEvent.EventId}' (type '{storedEvent.EventType}') deserialized to null; refusing to skip it.");
	}

	/// <summary>
	/// Records a poison event against observability and host health, swallowing telemetry failures so
	/// they never affect the projection pipeline.
	/// </summary>
	private void RecordPoison(string projectionName, Exception ex)
	{
		try
		{
			_observability?.RecordError(projectionName, ex.GetType().Name);
		}
		catch
		{
			// Swallow -- metrics must not affect the projection pipeline.
		}

		try
		{
			_healthState?.RecordInlineError(projectionName);
		}
		catch
		{
			// Swallow -- health recording must not affect the projection pipeline.
		}
	}

	/// <summary>
	/// Dispatches domain events to all async projection registrations concurrently.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if at least one projection's apply faulted (each projection is still
	/// attempted — fault-independence); otherwise <see langword="false"/>. The caller MUST NOT advance the
	/// checkpoint past a batch that reports a fault, so a failed apply is reprocessed rather than silently
	/// skipped (read-model integrity; parity with the deserialize-poison halt and GlobalStreamProjectionHost).
	/// </returns>
	private async Task<bool> DispatchToProjectionsAsync(
		IReadOnlyList<ProjectionRegistration> registrations,
		List<ProjectionEvent> domainEvents,
		EventNotificationContext context,
		IReadOnlyDictionary<Type, string?> destinations,
		CancellationToken cancellationToken)
	{
		var tasks = new Task[registrations.Count];

		for (var i = 0; i < registrations.Count; i++)
		{
			var registration = registrations[i];

			if (registration.InlineApply is not null)
			{
				// IProjectionStore<T> is scoped; this host is a singleton BackgroundService, so the
				// apply delegate must receive a provider from a created scope, not the captured root
				// provider (which throws under DI scope validation). A scope per projection also
				// isolates scoped state across the concurrently-applied projections.
				tasks[i] = ApplyInScopeAsync(registration, domainEvents, context, destinations.GetValueOrDefault(registration.ProjectionType), cancellationToken);
			}
			else
			{
				tasks[i] = Task.CompletedTask;
			}
		}

		var anyFaulted = false;
		for (var j = 0; j < tasks.Length; j++)
		{
			try
			{
				await tasks[j].ConfigureAwait(false);
			}
#pragma warning disable CA1031 // Catch general exceptions -- partial failure; log, record, and report to halt the batch
			catch (Exception ex)
#pragma warning restore CA1031
			{
				// Fault-independence: still await every projection so an independent one is not abandoned.
				// But REPORT the fault so the caller does not advance the checkpoint past it (halt-at-failure),
				// preventing silent read-model drift from the event log.
				anyFaulted = true;
				var projectionName = registrations[j].ProjectionType.Name;
				LogAsyncProjectionDispatchError(projectionName, ex);

				try
				{
					_healthState?.RecordInlineError(projectionName);
					_observability?.RecordError(projectionName, ex.GetType().Name);
				}
				catch
				{
					// Swallow -- metrics must not affect projection pipeline
				}
			}
		}

		return anyFaulted;
	}

	/// <remarks>
	/// Timed because a slow apply is the earliest observable cause of a degraded read: a
	/// session-consistent read waits on this projection reaching the caller's token, so an apply that
	/// is merely slow surfaces to the consumer as a read whose bound expired. The per-projection
	/// threshold wins over the global one when both are set.
	/// </remarks>
	private async Task ApplyInScopeAsync(
		ProjectionRegistration registration,
		List<ProjectionEvent> domainEvents,
		EventNotificationContext context,
		string? expectedDestination,
		CancellationToken cancellationToken)
	{
		var started = _timeProvider.GetTimestamp();

		await using (var scope = _serviceProvider.CreateAsyncScope())
		{
			foreach (var probe in scope.ServiceProvider.GetServices<IPositionedProjectionProbe>())
			{
				if (probe.ProjectionType == registration.ProjectionType
					&& !string.Equals(expectedDestination, probe.GetDestinationIdentity(scope.ServiceProvider), StringComparison.Ordinal))
				{
					throw new InvalidOperationException($"Projection '{probe.ProjectionTypeName}' changed destination between binding and applying the page.");
				}
			}
			await registration.InlineApply!(domainEvents, context, scope.ServiceProvider, cancellationToken)
				.ConfigureAwait(false);
			// Even a fold skipped because another process already wrote must establish
			// SEARCH visibility before this process acknowledges the page.
			foreach (var probe in scope.ServiceProvider.GetServices<IPositionedProjectionProbe>())
			{
				if (probe.ProjectionType == registration.ProjectionType)
				{
					await probe.EnsureVisibleAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
				}
			}

		}

		var elapsed = _timeProvider.GetElapsedTime(started);
		var threshold = registration.Options?.WarningThreshold
			?? _notificationOptions.ProjectionApplyWarningThreshold;

		if (elapsed > threshold)
		{
			LogProjectionApplySlow(
				registration.ProjectionType.Name,
				domainEvents.Count,
				elapsed.TotalMilliseconds,
				threshold.TotalMilliseconds);
		}
	}

	/// <inheritdoc />
	public override void Dispose()
	{
		_pageGate.Dispose();
		base.Dispose();
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostStarted, LogLevel.Information,
		"Async projection processing host started with {ProjectionCount} async projection(s).")]
	private partial void LogAsyncProjectionHostStarted(int projectionCount);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostStopped, LogLevel.Information,
		"Async projection processing host stopped.")]
	private partial void LogAsyncProjectionHostStopped();

	[LoggerMessage(EventSourcingEventId.AsyncProjectionBatchProcessed, LogLevel.Debug,
		"Async projections processed batch of {EventCount} events, position now at {Position}.")]
	private partial void LogAsyncProjectionBatchProcessed(int eventCount, long position);

	[LoggerMessage(EventSourcingEventId.ErasedEventSkipped, LogLevel.Debug,
		"Skipping erased (tombstoned) event {EventId} at global position {GlobalPosition}; advancing past it")]
	private partial void LogErasedEventSkipped(string eventId, long globalPosition);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionCheckpointSaved, LogLevel.Debug,
		"Async projection checkpoint saved at position {Position}.")]
	private partial void LogAsyncProjectionCheckpointSaved(long position);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionEventError, LogLevel.Error,
		"Poison event {EventId} halted the async projection host; checkpoint not advanced past it.")]
	private partial void LogAsyncProjectionEventError(string eventId, Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionApplySlow, LogLevel.Warning,
		"Applying {EventCount} event(s) to projection {ProjectionName} took {ElapsedMs}ms, " +
		"over the {ThresholdMs}ms warning threshold; session-consistent reads of it may time out.")]
	private partial void LogProjectionApplySlow(
		string projectionName, int eventCount, double elapsedMs, double thresholdMs);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionDispatchError, LogLevel.Error,
		"Error dispatching events to async projection {ProjectionName}.")]
	private partial void LogAsyncProjectionDispatchError(string projectionName, Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostError, LogLevel.Error,
		"Async projection processing host encountered an error.")]
	private partial void LogAsyncProjectionHostError(Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionCheckpointWriteFailed, LogLevel.Error,
		"The projection checkpoint write failed. The host rewound to its last durable mark rather than "
		+ "continuing to apply events whose progress nothing durable records; the span since that mark "
		+ "will be re-applied once the checkpoint store accepts writes again.")]
	private partial void LogAsyncProjectionCheckpointWriteFailed(Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionNoGlobalStreamQuery, LogLevel.Warning,
		"No IGlobalStreamQuery registered. Async projection processing cannot start. " +
		"Ensure your event store provider (e.g., UseSqlServer) registers IGlobalStreamQuery.")]
	private partial void LogNoGlobalStreamQuery();

	#endregion Logging
}
