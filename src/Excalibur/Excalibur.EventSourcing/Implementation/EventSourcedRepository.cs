// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// Use Excalibur.EventSourcing as canonical source (AD-251-2)

namespace Excalibur.EventSourcing.Implementation;

/// <summary>
/// Event-sourced repository implementation with generic key support, optional automatic event upcasting, and outbox integration.
/// </summary>
/// <typeparam name="TAggregate"> The aggregate type. </typeparam>
/// <typeparam name="TKey"> The type of the aggregate identifier. </typeparam>
/// <remarks>
/// <para> This repository provides full event sourcing capabilities with strongly-typed keys:
/// <list type="bullet">
/// <item> Event loading and replay with version tracking </item>
/// <item> Automatic upcasting via <see cref="IUpcastingPipeline" /> when configured </item>
/// <item> Snapshot support via <see cref="ISnapshotManager" /> </item>
/// <item> ETag-based optimistic concurrency control </item>
/// <item> Outbox integration for reliable messaging of <see cref="IIntegrationEvent" /> instances </item>
/// <item> CQRS write-side only — query operations belong in a separate read model </item>
/// </list>
/// </para>
/// <para> <b> Usage: </b>
/// <code>
///services.AddMessageUpcasting(builder =&gt; builder
///.RegisterUpcaster&lt;OrderCreatedV1, OrderCreatedV2&gt;(new OrderCreatedV1ToV2())
///.EnableAutoUpcastOnReplay());
///
///services.AddExcalibur(x => x.AddEventSourcing(builder =&gt; builder
///.AddRepository&lt;OrderAggregate, Guid&gt;()));
/// </code>
/// </para>
/// </remarks>
public class EventSourcedRepository<TAggregate, TKey> : IEventSourcedRepository<TAggregate, TKey>
	where TAggregate : class, Domain.Model.IAggregateRoot<TKey>, Domain.Model.IAggregateSnapshotSupport
	where TKey : notnull
{
	private static readonly CompositeFormat SaveFailedFormat =
		CompositeFormat.Parse(Resources.EventSourcedRepository_SaveFailedFormat);

	private static readonly CompositeFormat DeleteRequiresTombstoneFormat =
		CompositeFormat.Parse(Resources.EventSourcedRepository_DeleteRequiresTombstoneFormat);

	private readonly IEventStore _eventStore;
	private readonly IUpcastingPipeline? _upcastingPipeline;
	private readonly ISnapshotManager? _snapshotManager;
	private readonly ISnapshotStrategy? _snapshotStrategy;
	private readonly IEventSerializer _eventSerializer;
	private readonly ITransactionalOutboxWriter? _transactionalOutboxWriter;
	private readonly IOutboxStore? _outboxStore;
	private readonly OutboxStagingStrategy _outboxStagingStrategy;
	private readonly SnapshotVersionManager? _snapshotVersionManager;
	private readonly bool _enableAutoUpcast;
	private readonly bool _enableAutoSnapshotUpgrade;
	private readonly int _targetSnapshotVersion;
	private readonly Func<TKey, TAggregate> _aggregateFactory;
	private readonly IOptionsMonitor<AutoSnapshotOptions>? _autoSnapshotOptions;
	private readonly IEventNotificationBroker? _eventNotificationBroker;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger? _logger;

	// Tracked per aggregate ID for auto-snapshot decision context (thread-safe for concurrent loads/saves)
	private readonly ConcurrentDictionary<string, SnapshotTrackingState> _snapshotTracking = new(StringComparer.Ordinal);

	private readonly record struct SnapshotTrackingState(long Version, DateTimeOffset Timestamp);

	/// <summary>
	/// Initializes a new instance of the <see cref="EventSourcedRepository{TAggregate, TKey}" /> class.
	/// </summary>
	/// <param name="eventStore"> The event store for persistence. </param>
	/// <param name="eventSerializer"> The event serializer for deserialization. </param>
	/// <param name="aggregateFactory"> Factory function to create aggregate instances from a key. </param>
	/// <param name="upcastingPipeline"> Optional upcasting pipeline for version transformation. </param>
	/// <param name="snapshotManager"> Optional snapshot manager. </param>
	/// <param name="snapshotStrategy"> Optional snapshot strategy. </param>
	/// <param name="upcastingOptions"> Optional upcasting configuration options. </param>
	/// <param name="transactionalOutboxWriter"> Optional transactional outbox writer for staging integration events atomically with event appends. </param>
	/// <param name="outboxStore"> Optional outbox store for eventually-consistent staging when transactional writer is unavailable. </param>
	/// <param name="snapshotVersionManager"> Optional snapshot version manager for automatic snapshot upgrading. </param>
	/// <param name="snapshotUpgradingOptions"> Optional snapshot upgrading configuration options. </param>
	/// <param name="logger"> Optional logger for diagnostics. </param>
	/// <param name="eventNotificationBroker"> Optional event notification broker for inline projections and post-commit handlers. </param>
	/// <param name="autoSnapshotOptions"> Optional auto-snapshot configuration for automatic snapshot creation after save. </param>
	/// <param name="timeProvider"> Optional time provider for deterministic testing. </param>
	/// <param name="outboxStagingStrategy"> The outbox staging strategy. Default is <see cref="OutboxStagingStrategy.Auto"/>. </param>
	public EventSourcedRepository(
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		Func<TKey, TAggregate> aggregateFactory,
		IOptions<UpcastingOptions>? upcastingOptions = null,
		IOptions<SnapshotUpgradingOptions>? snapshotUpgradingOptions = null,
		IOptionsMonitor<AutoSnapshotOptions>? autoSnapshotOptions = null,
		IUpcastingPipeline? upcastingPipeline = null,
		ISnapshotManager? snapshotManager = null,
		ISnapshotStrategy? snapshotStrategy = null,
		ITransactionalOutboxWriter? transactionalOutboxWriter = null,
		IOutboxStore? outboxStore = null,
		SnapshotVersionManager? snapshotVersionManager = null,
		IEventNotificationBroker? eventNotificationBroker = null,
		TimeProvider? timeProvider = null,
		OutboxStagingStrategy outboxStagingStrategy = OutboxStagingStrategy.Auto,
		ILogger<EventSourcedRepository<TAggregate, TKey>>? logger = null)
	{
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_aggregateFactory = aggregateFactory ?? throw new ArgumentNullException(nameof(aggregateFactory));
		_upcastingPipeline = upcastingPipeline;
		_snapshotManager = snapshotManager;
		_snapshotStrategy = snapshotStrategy;
		_transactionalOutboxWriter = transactionalOutboxWriter;
		_outboxStore = outboxStore;
		_outboxStagingStrategy = outboxStagingStrategy;
		_snapshotVersionManager = snapshotVersionManager;
		_enableAutoUpcast = upcastingOptions?.Value.EnableAutoUpcastOnReplay ?? false;
		_enableAutoSnapshotUpgrade = snapshotUpgradingOptions?.Value.EnableAutoUpgradeOnLoad ?? false;
		_targetSnapshotVersion = snapshotUpgradingOptions?.Value.CurrentSnapshotVersion ?? 1;
		_eventNotificationBroker = eventNotificationBroker;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_logger = logger;
		_autoSnapshotOptions = autoSnapshotOptions;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventSourcedRepository{TAggregate, TKey}" /> class
	/// using the Options pattern for configuration.
	/// </summary>
	/// <param name="eventStore"> The event store for persistence. </param>
	/// <param name="eventSerializer"> The event serializer for deserialization. </param>
	/// <param name="aggregateFactory"> Factory function to create aggregate instances from a key. </param>
	/// <param name="options"> Configuration options for the repository. </param>
	/// <param name="upcastingPipeline"> Optional upcasting pipeline for version transformation. </param>
	/// <param name="snapshotManager"> Optional snapshot manager. </param>
	/// <param name="snapshotStrategy"> Optional snapshot strategy. </param>
	/// <param name="transactionalOutboxWriter"> Optional transactional outbox writer for staging integration events atomically with event appends. </param>
	/// <param name="outboxStore"> Optional outbox store for eventually-consistent staging when transactional writer is unavailable. </param>
	/// <param name="snapshotVersionManager"> Optional snapshot version manager for automatic snapshot upgrading. </param>
	/// <param name="logger"> Optional logger for diagnostics. </param>
	/// <param name="eventNotificationBroker"> Optional event notification broker for inline projections and post-commit handlers. </param>
	/// <param name="autoSnapshotOptions"> Optional auto-snapshot configuration for automatic snapshot creation after save. </param>
	/// <param name="timeProvider"> Optional time provider for deterministic testing. </param>
	public EventSourcedRepository(
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		Func<TKey, TAggregate> aggregateFactory,
		IOptions<EventSourcedRepositoryOptions> options,
		IOptionsMonitor<AutoSnapshotOptions>? autoSnapshotOptions = null,
		IUpcastingPipeline? upcastingPipeline = null,
		ISnapshotManager? snapshotManager = null,
		ISnapshotStrategy? snapshotStrategy = null,
		ITransactionalOutboxWriter? transactionalOutboxWriter = null,
		IOutboxStore? outboxStore = null,
		SnapshotVersionManager? snapshotVersionManager = null,
		IEventNotificationBroker? eventNotificationBroker = null,
		TimeProvider? timeProvider = null,
		ILogger<EventSourcedRepository<TAggregate, TKey>>? logger = null)
	{
		ArgumentNullException.ThrowIfNull(eventStore);
		ArgumentNullException.ThrowIfNull(eventSerializer);
		ArgumentNullException.ThrowIfNull(aggregateFactory);
		ArgumentNullException.ThrowIfNull(options);

		_eventStore = eventStore;
		_eventSerializer = eventSerializer;
		_aggregateFactory = aggregateFactory;
		_upcastingPipeline = upcastingPipeline;
		_snapshotManager = snapshotManager;
		_snapshotStrategy = snapshotStrategy;
		_transactionalOutboxWriter = transactionalOutboxWriter;
		_outboxStore = outboxStore;
		_outboxStagingStrategy = options.Value.OutboxStagingStrategy;
		_snapshotVersionManager = snapshotVersionManager;
		_enableAutoUpcast = options.Value.EnableAutoUpcast;
		_enableAutoSnapshotUpgrade = options.Value.EnableAutoSnapshotUpgrade;
		_targetSnapshotVersion = options.Value.TargetSnapshotVersion;
		_eventNotificationBroker = eventNotificationBroker;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_logger = logger;
		_autoSnapshotOptions = autoSnapshotOptions;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification = "Optional snapshot upgrading and serialization use reflection; the repository's own rehydration is a delegate-factory + pattern-matched apply (no reflection). AOT consumers select an AOT-safe serializer and the interface contract stays clean.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Optional snapshot upgrading and serialization may use dynamic code; the repository's own rehydration does not.")]
	public async Task<TAggregate?> GetByIdAsync(TKey aggregateId, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregateId);

		var aggregate = _aggregateFactory(aggregateId);
		var aggregateType = aggregate.AggregateType;
		var stringId = aggregateId.ToString() ?? throw new InvalidOperationException(
			Resources.EventSourcedRepository_AggregateIdCannotConvertToNullString);

		// Try to load from snapshot first
		var snapshotVersion = -1L;
		if (_snapshotManager is not null)
		{
			var snapshot = await _snapshotManager.GetLatestSnapshotAsync(stringId, cancellationToken)
				.ConfigureAwait(false);
			if (snapshot is not null)
			{
				snapshot = TryUpgradeSnapshot(snapshot, aggregateType);
				aggregate.LoadFromSnapshot(snapshot);
				snapshotVersion = snapshot.Version;

				// Track snapshot state for auto-snapshot decisions in SaveAsync
				_snapshotTracking[stringId] = new SnapshotTrackingState(snapshot.Version, snapshot.CreatedAt);
			}
		}

		// Load events after snapshot (or all events if no snapshot)
		// Note: snapshot.Version is the aggregate's version COUNT (e.g., 3 means 3 events committed) Event store uses 0-indexed versions
		// (events 0, 1, 2 for 3 events) LoadAsync filters Version > fromVersion, so we pass snapshotVersion - 1 to load events with Version
		// > (snapshotVersion - 1), i.e., Version >= snapshotVersion (0-indexed)
		var fromVersion = snapshotVersion > 0 ? snapshotVersion - 1 : snapshotVersion;
		var storedEvents = await _eventStore.LoadAsync(stringId, aggregateType, fromVersion, cancellationToken)
			.ConfigureAwait(false);

		if (storedEvents.Count == 0 && snapshotVersion == -1)
		{
			// Aggregate doesn't exist
			return null;
		}

		// Detect version gaps when loading from snapshot (T.9: version gap detection)
		if (snapshotVersion > 0 && storedEvents.Count > 0 && storedEvents[0].Version > snapshotVersion)
		{
			_logger?.LogWarning(
				"Version gap detected for aggregate '{AggregateId}' ({AggregateType}): " +
				"snapshot at version {SnapshotVersion}, first event at version {FirstEventVersion}. " +
				"Events between these versions may have been retired or compacted.",
				stringId, aggregateType, snapshotVersion, storedEvents[0].Version);
		}

		// Deserialize, optionally upcast, and collect events for replay
		var eventsToApply = new List<IDomainEvent>(storedEvents.Count);
		foreach (var storedEvent in storedEvents)
		{
			// Do NOT silently skip a stored event during rehydration — skipping corrupts the
			// source-of-truth aggregate (it would replay an incomplete history into a wrong state).
			// Fail loud so the caller never receives a silently-truncated aggregate.
			var domainEvent = DeserializeEvent(storedEvent)
				?? throw new InvalidOperationException(
					$"Event '{storedEvent.EventId}' (type '{storedEvent.EventType}', version {storedEvent.Version}) " +
					$"deserialized to null while rehydrating aggregate '{stringId}' ({aggregateType}). Refusing to " +
					$"skip it and return a corrupt aggregate; ensure the event type is registered and resolvable.");

			// Upcast if enabled and event is versioned
			var eventToApply = TryUpcastEvent(domainEvent);
			eventsToApply.Add(eventToApply);
		}

		// Use LoadFromHistory to properly replay events with version tracking
		aggregate.LoadFromHistory(eventsToApply);

		return aggregate;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification = "Optional serialization uses reflection; the repository's own persistence path does not. AOT consumers select an AOT-safe serializer and the interface contract stays clean.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Optional serialization may use dynamic code; the repository's own persistence path does not.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
		Justification = "SaveAsync orchestrates event append, transactional outbox, fallback outbox, snapshots, and notifications -- coupling is inherent to the coordination role.")]
	public async Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		var rawEvents = aggregate.GetUncommittedEvents();
		if (rawEvents.Count == 0)
		{
			return;
		}

		// Snapshot the events before MarkEventsAsCommitted clears the aggregate's internal list.
		// The snapshot is used for event notification after commit.
		var uncommittedEvents = rawEvents.ToList();

		var stringId = aggregate.Id.ToString() ?? throw new InvalidOperationException(
			Resources.EventSourcedRepository_AggregateIdCannotConvertToNullString);

		// Verify all events have consistent AggregateId (set by RaiseEvent)
		Debug.Assert(
			uncommittedEvents.All(e => e.AggregateId == stringId),
			"All uncommitted events must have matching AggregateId. " +
			"Events should be raised via AggregateRoot.RaiseEvent which sets AggregateId automatically.");

		// Propagate CorrelationId/TenantId from current Activity into event metadata
		EnrichEventsWithActivityContext(uncommittedEvents);

		// Calculate expected version for optimistic concurrency: Version represents count of committed events (0 = none, 5 = five events
		// committed) Event store expects the max version of stored events (-1 = no events, 4 = five events with versions 0-4)
		// Formula: (committed event count) - 1 = max version
		var expectedVersion = aggregate.Version - 1;

		// Resolve effective staging strategy
		var strategy = ResolveEffectiveStagingStrategy();

		if (strategy == OutboxStagingStrategy.Transactional
			&& _transactionalOutboxWriter is not null
			&& _eventStore is ITransactionalEventStore txStore)
		{
			// Transactional: append events and stage outbox messages in a single atomic transaction.
			await SaveWithTransactionalOutboxAsync(
					txStore, stringId, aggregate, uncommittedEvents, expectedVersion, cancellationToken)
				.ConfigureAwait(false);
		}
		else
		{
			// Non-transactional path: append events first.
			var result = await _eventStore.AppendAsync(
				stringId,
				aggregate.AggregateType,
				uncommittedEvents,
				expectedVersion,
				cancellationToken).ConfigureAwait(false);

			ThrowIfAppendFailed(result, aggregate);

			// Eventually-consistent: stage integration events after successful append.
			if (strategy == OutboxStagingStrategy.EventuallyConsistent && _outboxStore is not null)
			{
				await StageIntegrationEventsAsync(
					stringId, aggregate, uncommittedEvents, cancellationToken).ConfigureAwait(false);
			}

			// Deferred: no staging here; events are picked up later by a background service.
		}

		aggregate.MarkEventsAsCommitted();

		// Update ETag to reflect the new version
		// Format: "{AggregateType}:{Id}:v{Version}" provides deterministic, version-linked ETags
		aggregate.ETag = $"{aggregate.AggregateType}:{aggregate.Id}:v{aggregate.Version}";

		// Notify inline projections and event notification handlers (R27.2, R27.3).
		// Events are already committed -- the broker handles failure per the configured policy.
		// When no broker is registered in DI, this is a no-op (zero behavioral change).
		if (_eventNotificationBroker is not null)
		{
			var context = new EventNotificationContext(
				uncommittedEvents[0].AggregateId,
				aggregate.AggregateType,
				aggregate.Version,
				_timeProvider.GetUtcNow());

			await _eventNotificationBroker.NotifyAsync(
					uncommittedEvents, context, cancellationToken)
				.ConfigureAwait(false);
		}

		// Check if we should create a snapshot (failure must not propagate as save failure)
		if (_snapshotManager is not null && _snapshotStrategy is not null && _snapshotStrategy.ShouldCreateSnapshot(aggregate))
		{
			try
			{
				var snapshot = await _snapshotManager.CreateSnapshotAsync(aggregate, cancellationToken)
					.ConfigureAwait(false);
				await _snapshotManager.SaveSnapshotAsync(stringId, snapshot, cancellationToken)
					.ConfigureAwait(false);

				// Update tracked state after successful snapshot
				_snapshotTracking[stringId] = new SnapshotTrackingState(aggregate.Version, _timeProvider.GetUtcNow());
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger?.LogWarning(
					ex,
					"Snapshot creation failed after successful save for aggregate '{AggregateId}'. " +
					"The aggregate was saved correctly; snapshot will be retried on next save.",
					stringId);
			}
		}

		// Auto-snapshot check: evaluate configured thresholds (best-effort, failure must not fail save)
		if (_autoSnapshotOptions is not null && _snapshotManager is not null)
		{
			var autoOptions = _autoSnapshotOptions.Get(aggregate.AggregateType);

			// Skip evaluation when no thresholds are configured (zero overhead)
			if (autoOptions.EventCountThreshold is not null
				|| autoOptions.TimeThreshold is not null
				|| autoOptions.VersionThreshold is not null
				|| autoOptions.CustomPolicy is not null)
			{
				var tracked = _snapshotTracking.GetValueOrDefault(stringId);
				var lastVersion = tracked.Version > 0 ? (long?)tracked.Version : null;
				var lastTimestamp = tracked.Timestamp != default ? (DateTimeOffset?)tracked.Timestamp : null;
				var eventsSinceSnapshot = (int)(aggregate.Version - (lastVersion ?? 0));
				var decisionContext = new SnapshotDecisionContext(
					stringId,
					aggregate.AggregateType,
					aggregate.Version,
					lastVersion,
					lastTimestamp,
					eventsSinceSnapshot);

				AutoSnapshotMetrics.Evaluated.Add(1);

				if (AutoSnapshotPolicy.ShouldSnapshot(autoOptions, decisionContext, _timeProvider))
				{
					try
					{
						var snapshot = await _snapshotManager.CreateSnapshotAsync(aggregate, cancellationToken)
							.ConfigureAwait(false);
						await _snapshotManager.SaveSnapshotAsync(stringId, snapshot, cancellationToken)
							.ConfigureAwait(false);

						// Update tracked state after successful auto-snapshot
						_snapshotTracking[stringId] = new SnapshotTrackingState(aggregate.Version, _timeProvider.GetUtcNow());

						AutoSnapshotMetrics.Created.Add(1);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						AutoSnapshotMetrics.Failed.Add(1);

						_logger?.LogWarning(
							ex,
							"Auto-snapshot failed for aggregate '{AggregateId}' at version {Version}. " +
							"The aggregate was saved correctly; snapshot will be retried on next save.",
							stringId,
							aggregate.Version);
					}
				}
			}
		}
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification = "Optional serialization uses reflection; the repository's own persistence path does not. AOT consumers select an AOT-safe serializer and the interface contract stays clean.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Optional serialization may use dynamic code; the repository's own persistence path does not.")]
	public async Task SaveAsync(TAggregate aggregate, string? expectedETag, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		// Validate ETag if provided
		if (expectedETag is not null && aggregate.ETag != expectedETag)
		{
			throw new ConcurrencyException(
				aggregate.Id.ToString() ?? string.Empty,
				typeof(TAggregate).Name,
				expectedETag,
				aggregate.ETag ?? string.Empty);
		}

		await SaveAsync(aggregate, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public virtual Task DeleteAsync(TAggregate aggregate, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		// Soft delete requires a tombstone event to be raised by the aggregate itself.
		throw new NotSupportedException(
			string.Format(
				CultureInfo.CurrentCulture,
				DeleteRequiresTombstoneFormat,
				typeof(TAggregate).Name));
	}

	/// <inheritdoc />
	public async Task<bool> ExistsAsync(TKey aggregateId, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregateId);

		var aggregate = _aggregateFactory(aggregateId);
		var stringId = aggregateId.ToString() ?? throw new InvalidOperationException(
			Resources.EventSourcedRepository_AggregateIdCannotConvertToNullString);
		var events = await _eventStore.LoadAsync(stringId, aggregate.AggregateType, cancellationToken)
			.ConfigureAwait(false);

		return events.Count > 0;
	}

	/// <summary>
	/// Enriches uncommitted events with CorrelationId and TenantId from the current Activity context.
	/// </summary>
	/// <remarks>
	/// Only sets metadata keys that are not already present, preserving explicitly set values.
	/// Keys follow transport header conventions: <c>correlation-id</c>, <c>tenant-id</c>.
	/// </remarks>
	private static void EnrichEventsWithActivityContext(IReadOnlyList<IDomainEvent> events)
	{
		var activity = Activity.Current;
		if (activity is null)
		{
			return;
		}

		var correlationId = activity.TraceId.ToString();
		string? tenantId = null;

		// Check Activity tags for tenant-id (set by middleware or upstream services)
		foreach (var tag in activity.Tags)
		{
			if (string.Equals(tag.Key, OutboxHeaderNames.TenantId, StringComparison.Ordinal)
				|| string.Equals(tag.Key, "tenant.id", StringComparison.Ordinal))
			{
				tenantId = tag.Value;
				break;
			}
		}

		foreach (var @event in events)
		{
			if (@event.Metadata is null)
			{
				continue;
			}

			if (!@event.Metadata.ContainsKey(OutboxHeaderNames.CorrelationId))
			{
				@event.Metadata[OutboxHeaderNames.CorrelationId] = correlationId;
			}

			if (tenantId is not null && !@event.Metadata.ContainsKey(OutboxHeaderNames.TenantId))
			{
				@event.Metadata[OutboxHeaderNames.TenantId] = tenantId;
			}
		}
	}

	/// <summary>
	/// Deserializes a stored event to a domain event.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[RequiresUnreferencedCode("Event deserialization may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Event deserialization may require dynamic code generation.")]
	private IDomainEvent? DeserializeEvent(StoredEvent storedEvent)
	{
		try
		{
			var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
			return _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType);
		}
		catch (Exception ex) when (ex is JsonException or TypeLoadException or InvalidOperationException)
		{
			// Rehydration must NOT swallow a deserialization failure into a skipped event — that
			// corrupts the source-of-truth aggregate. Surface the failure (preserving the cause) so
			// the load fails loud rather than returning a silently-incomplete aggregate.
			_logger?.LogError(
				ex,
				"Failed to deserialize event type '{EventType}' for aggregate '{AggregateId}' at version {Version} " +
				"during rehydration. Failing the load rather than skipping the event.",
				storedEvent.EventType,
				storedEvent.AggregateId,
				storedEvent.Version);

			throw new InvalidOperationException(
				$"Failed to deserialize event '{storedEvent.EventId}' (type '{storedEvent.EventType}', " +
				$"version {storedEvent.Version}) for aggregate '{storedEvent.AggregateId}' during rehydration. " +
				$"Refusing to skip it and return a corrupt aggregate.",
				ex);
		}
	}

	/// <summary>
	/// Attempts to upgrade snapshot data if auto-upgrading is enabled and the version differs.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[RequiresUnreferencedCode("Snapshot upgrading may reference types not preserved during trimming.")]
	[RequiresDynamicCode("Snapshot upgrading may require dynamic code generation for serialization.")]
	private Domain.Model.ISnapshot TryUpgradeSnapshot(Domain.Model.ISnapshot snapshot, string aggregateType)
	{
		if (!_enableAutoSnapshotUpgrade || _snapshotVersionManager is null)
		{
			return snapshot;
		}

		// Get schema version from metadata (default: 1)
		var currentSchemaVersion = 1;
		if (snapshot.Metadata?.TryGetValue("SnapshotSchemaVersion", out var versionObj) == true
			&& versionObj is int schemaVersion)
		{
			currentSchemaVersion = schemaVersion;
		}

		if (currentSchemaVersion == _targetSnapshotVersion)
		{
			return snapshot;
		}

		if (!_snapshotVersionManager.CanUpgrade(aggregateType, currentSchemaVersion, _targetSnapshotVersion))
		{
			return snapshot;
		}

		var upgradedData = _snapshotVersionManager.UpgradeSnapshot(
			aggregateType, snapshot.Data.ToArray(), currentSchemaVersion, _targetSnapshotVersion);

		return new UpgradedSnapshot(snapshot, upgradedData, _targetSnapshotVersion);
	}

	/// <summary>
	/// Attempts to upcast an event if auto-upcasting is enabled and the event is versioned.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private IDomainEvent TryUpcastEvent(IDomainEvent domainEvent)
	{
		// Fast path: upcasting not enabled or no pipeline
		if (!_enableAutoUpcast || _upcastingPipeline is null)
		{
			return domainEvent;
		}

		// Only upcast versioned messages
		if (domainEvent is not IVersionedMessage)
		{
			return domainEvent;
		}

		// Perform upcasting - pipeline handles multi-hop internally
		var upcasted = _upcastingPipeline.Upcast(domainEvent);
		return upcasted as IDomainEvent ?? domainEvent;
	}

	/// <summary>
	/// Appends events and stages outbox messages within a single database transaction.
	/// </summary>
	[RequiresUnreferencedCode("Aggregate persistence may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Aggregate persistence may require dynamic code generation.")]
	private async Task SaveWithTransactionalOutboxAsync(
		ITransactionalEventStore txStore,
		string aggregateId,
		TAggregate aggregate,
		IReadOnlyList<IDomainEvent> uncommittedEvents,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		// Store-owned atomic unit of work: the event store opens and owns a single connection/transaction,
		// appends the events, invokes this stageOutbox callback on that same transaction, then commits.
		// Append and outbox staging therefore share one transaction — neither can persist without the other.
		var result = await txStore.AppendWithOutboxStagingAsync(
			aggregateId,
			aggregate.AggregateType,
			uncommittedEvents,
			expectedVersion,
			async (transaction, ct) =>
			{
				foreach (var @event in uncommittedEvents)
				{
					if (@event is not IIntegrationEvent)
					{
						continue;
					}

					var outboundMessage = CreateOutboundMessage(@event, aggregateId, aggregate.AggregateType);
					await _transactionalOutboxWriter!.StageMessageAsync(outboundMessage, transaction, ct)
						.ConfigureAwait(false);
				}
			},
			cancellationToken).ConfigureAwait(false);

		ThrowIfAppendFailed(result, aggregate);
	}

	/// <summary>
	/// Resolves the effective outbox staging strategy based on configuration and available infrastructure.
	/// </summary>
	private OutboxStagingStrategy ResolveEffectiveStagingStrategy()
	{
		if (_outboxStagingStrategy != OutboxStagingStrategy.Auto)
		{
			return _outboxStagingStrategy;
		}

		// Auto: select best available strategy
		if (_transactionalOutboxWriter is not null && _eventStore is ITransactionalEventStore)
		{
			return OutboxStagingStrategy.Transactional;
		}

		if (_outboxStore is not null)
		{
			return OutboxStagingStrategy.EventuallyConsistent;
		}

		return OutboxStagingStrategy.Deferred;
	}

	/// <summary>
	/// Stages integration events to the outbox without transaction atomicity (eventually-consistent fallback).
	/// </summary>
	[RequiresUnreferencedCode("Aggregate persistence may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Aggregate persistence may require dynamic code generation.")]
	private async Task StageIntegrationEventsAsync(
		string aggregateId,
		TAggregate aggregate,
		IReadOnlyList<IDomainEvent> uncommittedEvents,
		CancellationToken cancellationToken)
	{
		foreach (var @event in uncommittedEvents)
		{
			if (@event is not IIntegrationEvent)
			{
				continue;
			}

			var outboundMessage = CreateOutboundMessage(@event, aggregateId, aggregate.AggregateType);
			await _outboxStore!.StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates an <see cref="OutboundMessage"/> from a domain event for outbox staging.
	/// </summary>
	[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation.")]
	private OutboundMessage CreateOutboundMessage(IDomainEvent @event, string aggregateId, string aggregateType)
	{
		var eventData = JsonSerializer.Serialize(@event, @event.GetType());
		var headers = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[OutboxHeaderNames.AggregateId] = aggregateId,
			[OutboxHeaderNames.AggregateType] = aggregateType,
		};

		if (@event.Metadata is { Count: > 0 })
		{
			foreach (var kvp in @event.Metadata)
			{
				headers.TryAdd(kvp.Key, kvp.Value);
			}
		}

		// Extract tenant and correlation IDs from event metadata for outbox routing
		var tenantId = @event.Metadata is not null && @event.Metadata.TryGetValue(OutboxHeaderNames.TenantId, out var tid)
			? tid?.ToString() : null;
		var correlationId = @event.Metadata is not null && @event.Metadata.TryGetValue(OutboxHeaderNames.CorrelationId, out var cid)
			? cid?.ToString() : null;
		var causationId = @event.Metadata is not null && @event.Metadata.TryGetValue(OutboxHeaderNames.CausationId, out var csid)
			? csid?.ToString() : null;

		return new OutboundMessage
		{
			Id = Guid.NewGuid().ToString(),
			MessageType = @event.EventType,
			Payload = Encoding.UTF8.GetBytes(eventData),
			Headers = headers,
			CreatedAt = _timeProvider.GetUtcNow(),
			TenantId = tenantId,
			CorrelationId = correlationId,
			CausationId = causationId,
			PartitionKey = tenantId ?? aggregateId,
		};
	}

	/// <summary>
	/// Throws appropriate exception if the append result indicates failure.
	/// </summary>
	private void ThrowIfAppendFailed(AppendResult result, TAggregate aggregate)
	{
		if (result.Success)
		{
			return;
		}

		if (result.IsConcurrencyConflict)
		{
			throw new ConcurrencyException(
				string.Format(
					CultureInfo.CurrentCulture,
					SaveFailedFormat,
					aggregate.Id,
					result.ErrorMessage));
		}

		throw new ResourceException(
			string.Format(
				CultureInfo.CurrentCulture,
				SaveFailedFormat,
				aggregate.Id,
				result.ErrorMessage));
	}

	/// <summary>
	/// Wraps an existing snapshot with upgraded data bytes.
	/// </summary>
	private sealed class UpgradedSnapshot : Domain.Model.ISnapshot
	{
		private readonly Domain.Model.ISnapshot _original;
		private readonly byte[] _upgradedData;
		private readonly int _schemaVersion;

		public UpgradedSnapshot(Domain.Model.ISnapshot original, byte[] upgradedData, int schemaVersion)
		{
			_original = original;
			_upgradedData = upgradedData;
			_schemaVersion = schemaVersion;
		}

		public string SnapshotId => _original.SnapshotId;
		public string AggregateId => _original.AggregateId;
		public long Version => _original.Version;
		public DateTimeOffset CreatedAt => _original.CreatedAt;
		public ReadOnlyMemory<byte> Data => _upgradedData;
		public string AggregateType => _original.AggregateType;

		public IDictionary<string, object>? Metadata
		{
			get
			{
				// Defensive copy to avoid mutating the original snapshot's metadata dictionary
				var metadata = _original.Metadata is not null
					? new Dictionary<string, object>(_original.Metadata)
					: new Dictionary<string, object>();
				metadata["SnapshotSchemaVersion"] = _schemaVersion;
				return metadata;
			}
		}
	}
}

/// <summary>
/// Event-sourced repository implementation for aggregates with string keys. This is a convenience class that wraps
/// <see cref="EventSourcedRepository{TAggregate, TKey}" /> with a string key type.
/// </summary>
/// <typeparam name="TAggregate"> The aggregate type. </typeparam>
/// <remarks>
/// <para>
/// Use this class when your aggregates use string identifiers. For other key types, use
/// <see cref="EventSourcedRepository{TAggregate, TKey}" /> directly.
/// </para>
/// </remarks>
public class EventSourcedRepository<TAggregate> : EventSourcedRepository<TAggregate, string>,
	IEventSourcedRepository<TAggregate>
	where TAggregate : class, Domain.Model.IAggregateRoot<string>, Domain.Model.IAggregateSnapshotSupport
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EventSourcedRepository{TAggregate}" /> class.
	/// </summary>
	/// <param name="eventStore"> The event store for persistence. </param>
	/// <param name="eventSerializer"> The event serializer for deserialization. </param>
	/// <param name="aggregateFactory"> Factory function to create aggregate instances from a string key. </param>
	/// <param name="upcastingPipeline"> Optional upcasting pipeline for version transformation. </param>
	/// <param name="snapshotManager"> Optional snapshot manager. </param>
	/// <param name="snapshotStrategy"> Optional snapshot strategy. </param>
	/// <param name="upcastingOptions"> Optional upcasting configuration options. </param>
	/// <param name="transactionalOutboxWriter"> Optional transactional outbox writer for staging integration events atomically with event appends. </param>
	/// <param name="outboxStore"> Optional outbox store for eventually-consistent staging when transactional writer is unavailable. </param>
	/// <param name="snapshotVersionManager"> Optional snapshot version manager for automatic snapshot upgrading. </param>
	/// <param name="snapshotUpgradingOptions"> Optional snapshot upgrading configuration options. </param>
	/// <param name="logger"> Optional logger for diagnostics. </param>
	/// <param name="eventNotificationBroker"> Optional event notification broker for inline projections and post-commit handlers. </param>
	/// <param name="autoSnapshotOptions"> Optional auto-snapshot configuration for automatic snapshot creation after save. </param>
	/// <param name="timeProvider"> Optional time provider for deterministic testing. </param>
	/// <param name="outboxStagingStrategy"> The outbox staging strategy. Default is <see cref="OutboxStagingStrategy.Auto"/>. </param>
	public EventSourcedRepository(
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		Func<string, TAggregate> aggregateFactory,
		IOptions<UpcastingOptions>? upcastingOptions = null,
		IOptions<SnapshotUpgradingOptions>? snapshotUpgradingOptions = null,
		IOptionsMonitor<AutoSnapshotOptions>? autoSnapshotOptions = null,
		IUpcastingPipeline? upcastingPipeline = null,
		ISnapshotManager? snapshotManager = null,
		ISnapshotStrategy? snapshotStrategy = null,
		ITransactionalOutboxWriter? transactionalOutboxWriter = null,
		IOutboxStore? outboxStore = null,
		SnapshotVersionManager? snapshotVersionManager = null,
		IEventNotificationBroker? eventNotificationBroker = null,
		TimeProvider? timeProvider = null,
		OutboxStagingStrategy outboxStagingStrategy = OutboxStagingStrategy.Auto,
		ILogger<EventSourcedRepository<TAggregate, string>>? logger = null)
		: base(eventStore, eventSerializer, aggregateFactory, upcastingOptions, snapshotUpgradingOptions,
			autoSnapshotOptions, upcastingPipeline, snapshotManager, snapshotStrategy,
			transactionalOutboxWriter, outboxStore, snapshotVersionManager, eventNotificationBroker,
			timeProvider, outboxStagingStrategy, logger)
	{
	}
}
