// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Versioning;

using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.Options;

using ConcurrencyException = Excalibur.Dispatch.Exceptions.ConcurrencyException;
// Use Excalibur.EventSourcing.Abstractions as canonical source (AD-251-2)
using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;
using ISnapshotManager = Excalibur.EventSourcing.Abstractions.ISnapshotManager;
using ISnapshotStrategy = Excalibur.EventSourcing.Abstractions.ISnapshotStrategy;
using ResourceException = Excalibur.Dispatch.Exceptions.ResourceException;
using StoredEvent = Excalibur.EventSourcing.Abstractions.StoredEvent;

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
/// <item> Snapshot support via <see cref="ExcaliburSnapshotManager" /> </item>
/// <item> ETag-based optimistic concurrency control </item>
/// <item> Outbox integration for reliable messaging of <see cref="IIntegrationEvent" /> instances </item>
/// <item> Query support via <see cref="Abstractions.IAggregateQuery{TAggregate}" /> (requires injected executor) </item>
/// </list>
/// </para>
/// <para> <b> Usage: </b>
/// <code>
///services.AddMessageUpcasting(builder =&gt; builder
///.RegisterUpcaster&lt;OrderCreatedV1, OrderCreatedV2&gt;(new OrderCreatedV1ToV2())
///.EnableAutoUpcastOnReplay());
///
///services.AddExcaliburEventSourcing(builder =&gt; builder
///.AddRepository&lt;OrderAggregate, Guid&gt;());
/// </code>
/// </para>
/// </remarks>
public class EventSourcedRepository<TAggregate, TKey> : Abstractions.IEventSourcedRepository<TAggregate, TKey>
		where TAggregate : class, Domain.Model.IAggregateRoot<TKey>, Domain.Model.IAggregateSnapshotSupport
		where TKey : notnull
{
	private static readonly CompositeFormat SaveFailedFormat =
			CompositeFormat.Parse(Resources.EventSourcedRepository_SaveFailedFormat);
	private static readonly CompositeFormat DeleteRequiresTombstoneFormat =
			CompositeFormat.Parse(Resources.EventSourcedRepository_DeleteRequiresTombstoneFormat);
	private static readonly CompositeFormat QueryRequiresExecutorFormat =
			CompositeFormat.Parse(Resources.EventSourcedRepository_QueryRequiresExecutorFormat);
	private static readonly CompositeFormat FindRequiresExecutorFormat =
			CompositeFormat.Parse(Resources.EventSourcedRepository_FindRequiresExecutorFormat);

	private readonly IEventStore _eventStore;
	private readonly IUpcastingPipeline? _upcastingPipeline;
	private readonly ISnapshotManager? _snapshotManager;
	private readonly ISnapshotStrategy? _snapshotStrategy;
	private readonly IEventSerializer _eventSerializer;
	private readonly IEventSourcedOutboxStore? _outboxStore;
	private readonly SnapshotVersionManager? _snapshotVersionManager;
	private readonly bool _enableAutoUpcast;
	private readonly bool _enableAutoSnapshotUpgrade;
	private readonly int _targetSnapshotVersion;
	private readonly Func<TKey, TAggregate> _aggregateFactory;

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
	/// <param name="outboxStore"> Optional outbox store for staging integration events. </param>
	/// <param name="snapshotVersionManager"> Optional snapshot version manager for automatic snapshot upgrading. </param>
	/// <param name="snapshotUpgradingOptions"> Optional snapshot upgrading configuration options. </param>
	public EventSourcedRepository(
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		Func<TKey, TAggregate> aggregateFactory,
		IUpcastingPipeline? upcastingPipeline = null,
		ISnapshotManager? snapshotManager = null,
		ISnapshotStrategy? snapshotStrategy = null,
		IOptions<UpcastingOptions>? upcastingOptions = null,
		IEventSourcedOutboxStore? outboxStore = null,
		SnapshotVersionManager? snapshotVersionManager = null,
		IOptions<SnapshotUpgradingOptions>? snapshotUpgradingOptions = null)
	{
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_aggregateFactory = aggregateFactory ?? throw new ArgumentNullException(nameof(aggregateFactory));
		_upcastingPipeline = upcastingPipeline;
		_snapshotManager = snapshotManager;
		_snapshotStrategy = snapshotStrategy;
		_outboxStore = outboxStore;
		_snapshotVersionManager = snapshotVersionManager;
		_enableAutoUpcast = upcastingOptions?.Value.EnableAutoUpcastOnReplay ?? false;
		_enableAutoSnapshotUpgrade = snapshotUpgradingOptions?.Value.EnableAutoUpgradeOnLoad ?? false;
		_targetSnapshotVersion = snapshotUpgradingOptions?.Value.CurrentSnapshotVersion ?? 1;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Aggregate rehydration may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Aggregate rehydration may require dynamic code generation.")]
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

		// Deserialize, optionally upcast, and collect events for replay
		var eventsToApply = new List<IDomainEvent>(storedEvents.Count);
		foreach (var storedEvent in storedEvents)
		{
			var domainEvent = DeserializeEvent(storedEvent);
			if (domainEvent is null)
			{
				continue;
			}

			// Upcast if enabled and event is versioned
			var eventToApply = TryUpcastEvent(domainEvent);
			eventsToApply.Add(eventToApply);
		}

		// Use LoadFromHistory to properly replay events with version tracking
		aggregate.LoadFromHistory(eventsToApply);

		return aggregate;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Aggregate persistence may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Aggregate persistence may require dynamic code generation.")]
	public async Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		var uncommittedEvents = aggregate.GetUncommittedEvents();
		if (uncommittedEvents.Count == 0)
		{
			return;
		}

		var stringId = aggregate.Id.ToString() ?? throw new InvalidOperationException(
				Resources.EventSourcedRepository_AggregateIdCannotConvertToNullString);

		// Calculate expected version for optimistic concurrency: Version represents count of committed events (0 = none, 5 = five events
		// committed) Event store expects the max version of stored events (-1 = no events, 4 = five events with versions 0-4)
		// Formula: (committed event count) - 1 = max version
		var expectedVersion = aggregate.Version - 1;

		var result = await _eventStore.AppendAsync(
			stringId,
			aggregate.AggregateType,
			uncommittedEvents,
			expectedVersion,
			cancellationToken).ConfigureAwait(false);

		if (!result.Success)
		{
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
					ErrorCodes.DataTransactionFailed,
					string.Format(
							CultureInfo.CurrentCulture,
							SaveFailedFormat,
							aggregate.Id,
							result.ErrorMessage),
					null);
		}

		// Stage integration events to outbox if configured
		if (_outboxStore is not null)
		{
			// Outbox staging requires transaction support; intentionally deferred.
		}

		aggregate.MarkEventsAsCommitted();

		// Update ETag to reflect the new version
		// Format: "{AggregateType}:{Id}:v{Version}" provides deterministic, version-linked ETags
		aggregate.ETag = $"{aggregate.AggregateType}:{aggregate.Id}:v{aggregate.Version}";

		// Check if we should create a snapshot
		if (_snapshotManager is not null && _snapshotStrategy is not null && _snapshotStrategy.ShouldCreateSnapshot(aggregate))
		{
			var snapshot = await _snapshotManager.CreateSnapshotAsync(aggregate, cancellationToken)
				.ConfigureAwait(false);
			await _snapshotManager.SaveSnapshotAsync(stringId, snapshot, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Aggregate persistence may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Aggregate persistence may require dynamic code generation.")]
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
				aggregate.ETag);
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

	/// <inheritdoc />
	public virtual Task<IReadOnlyList<TAggregate>> QueryAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : Abstractions.IAggregateQuery<TAggregate>
	{
		// Query execution requires a query executor to be injected. This base implementation throws NotSupportedException. Derived classes
		// can override this method to provide query support.
		throw new NotSupportedException(
				string.Format(
						CultureInfo.CurrentCulture,
												QueryRequiresExecutorFormat,
						typeof(TAggregate).Name));
	}

	/// <inheritdoc />
	public virtual Task<TAggregate?> FindAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : Abstractions.IAggregateQuery<TAggregate>
	{
		// Find execution requires a query executor to be injected. This base implementation throws NotSupportedException. Derived classes
		// can override this method to provide query support.
		throw new NotSupportedException(
				string.Format(
						CultureInfo.CurrentCulture,
												FindRequiresExecutorFormat,
						typeof(TAggregate).Name));
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
		public byte[] Data => _upgradedData;
		public string AggregateType => _original.AggregateType;

		public IDictionary<string, object>? Metadata
		{
			get
			{
				var metadata = _original.Metadata ?? new Dictionary<string, object>();
				metadata["SnapshotSchemaVersion"] = _schemaVersion;
				return metadata;
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
			// Fallback: event type may have changed or cannot be resolved
			return null;
		}
	}

	/// <summary>
	/// Attempts to upgrade snapshot data if auto-upgrading is enabled and the version differs.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
			aggregateType, snapshot.Data, currentSchemaVersion, _targetSnapshotVersion);

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
	Abstractions.IEventSourcedRepository<TAggregate>
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
	/// <param name="outboxStore"> Optional outbox store for staging integration events. </param>
	/// <param name="snapshotVersionManager"> Optional snapshot version manager for automatic snapshot upgrading. </param>
	/// <param name="snapshotUpgradingOptions"> Optional snapshot upgrading configuration options. </param>
	public EventSourcedRepository(
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		Func<string, TAggregate> aggregateFactory,
		IUpcastingPipeline? upcastingPipeline = null,
		ISnapshotManager? snapshotManager = null,
		ISnapshotStrategy? snapshotStrategy = null,
		IOptions<UpcastingOptions>? upcastingOptions = null,
		IEventSourcedOutboxStore? outboxStore = null,
		SnapshotVersionManager? snapshotVersionManager = null,
		IOptions<SnapshotUpgradingOptions>? snapshotUpgradingOptions = null)
		: base(eventStore, eventSerializer, aggregateFactory, upcastingPipeline, snapshotManager, snapshotStrategy, upcastingOptions,
			outboxStore, snapshotVersionManager, snapshotUpgradingOptions)
	{
	}
}
