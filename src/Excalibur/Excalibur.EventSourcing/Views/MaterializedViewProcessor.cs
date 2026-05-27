// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Views;

/// <summary>
/// Default implementation of <see cref="IMaterializedViewProcessor"/> that routes domain events
/// to registered <see cref="IMaterializedViewBuilder{TView}"/> instances and persists updated
/// views via <see cref="IMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The processor builds an event-type-to-builder routing map from
/// <see cref="MaterializedViewBuilderRegistration"/> entries at construction time. When an event
/// is processed, only builders whose <see cref="IMaterializedViewBuilder{TView}.HandledEventTypes"/>
/// include the event type are invoked.
/// </para>
/// <para>
/// For catch-up and rebuild scenarios, the processor reads the global event stream via
/// <see cref="IGlobalStreamQuery"/> and replays events through the builder pipeline.
/// Position tracking uses <see cref="IMaterializedViewStore.GetPositionAsync"/> and
/// <see cref="IMaterializedViewStore.SavePositionAsync"/>.
/// </para>
/// </remarks>
internal sealed partial class MaterializedViewProcessor : IMaterializedViewProcessor
{
    private readonly IMaterializedViewStore _viewStore;
    private readonly IGlobalStreamQuery _globalStreamQuery;
    private readonly IEventSerializer _eventSerializer;
    private readonly IOptions<MaterializedViewOptions> _options;
    private readonly ILogger<MaterializedViewProcessor> _logger;

    /// <summary>
    /// Maps event type -> list of (viewName, builderRegistration) for routing.
    /// </summary>
    private readonly Dictionary<Type, List<BuilderRoute>> _eventTypeRoutes;

    /// <summary>
    /// Maps viewName -> list of builderRegistrations for catch-up/rebuild by view.
    /// </summary>
    private readonly Dictionary<string, List<MaterializedViewBuilderRegistration>> _viewNameRoutes;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterializedViewProcessor"/> class.
    /// </summary>
    /// <param name="viewStore">The materialized view store for persistence and position tracking.</param>
    /// <param name="globalStreamQuery">The global stream query for reading events during catch-up/rebuild.</param>
    /// <param name="eventSerializer">The event serializer for deserializing stored events.</param>
    /// <param name="registrations">The registered materialized view builders.</param>
    /// <param name="options">The materialized view options.</param>
    /// <param name="logger">The logger.</param>
    public MaterializedViewProcessor(
        IMaterializedViewStore viewStore,
        IGlobalStreamQuery globalStreamQuery,
        IEventSerializer eventSerializer,
        IEnumerable<MaterializedViewBuilderRegistration> registrations,
        IOptions<MaterializedViewOptions> options,
        ILogger<MaterializedViewProcessor> logger)
    {
        _viewStore = viewStore ?? throw new ArgumentNullException(nameof(viewStore));
        _globalStreamQuery = globalStreamQuery ?? throw new ArgumentNullException(nameof(globalStreamQuery));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(registrations);

        _eventTypeRoutes = new Dictionary<Type, List<BuilderRoute>>();
        _viewNameRoutes = new Dictionary<string, List<MaterializedViewBuilderRegistration>>(StringComparer.Ordinal);

        BuildRoutingMaps(registrations);
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
    public async Task ProcessEventAsync(IDomainEvent @event, long position, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var eventType = @event.GetType();

        if (!_eventTypeRoutes.TryGetValue(eventType, out var routes))
        {
            // No builders handle this event type — skip silently
            return;
        }

        foreach (var route in routes)
        {
            await ApplyEventToBuilderAsync(route.Registration, @event, cancellationToken).ConfigureAwait(false);
        }

        // Save position for each affected view after processing
        foreach (var route in routes)
        {
            var viewName = GetViewName(route.Registration);
            await _viewStore.SavePositionAsync(viewName, position, cancellationToken).ConfigureAwait(false);
        }

        LogEventProcessed(@event.EventType, position);
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
    public async Task ProcessEventsAsync(
        IEnumerable<(IDomainEvent Event, long Position)> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        long lastPosition = -1;
        var affectedViews = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (domainEvent, position) in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var eventType = domainEvent.GetType();

            if (!_eventTypeRoutes.TryGetValue(eventType, out var routes))
            {
                continue;
            }

            foreach (var route in routes)
            {
                await ApplyEventToBuilderAsync(route.Registration, domainEvent, cancellationToken)
                    .ConfigureAwait(false);

                affectedViews.Add(GetViewName(route.Registration));
            }

            lastPosition = position;
        }

        // Save position for all affected views after the entire batch
        if (lastPosition >= 0)
        {
            foreach (var viewName in affectedViews)
            {
                await _viewStore.SavePositionAsync(viewName, lastPosition, cancellationToken)
                    .ConfigureAwait(false);
            }

            LogBatchProcessed(affectedViews.Count, lastPosition);
        }
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        LogRebuildStarting();

        // Reset positions for all registered views to replay from the beginning
        foreach (var (viewName, _) in _viewNameRoutes)
        {
            await _viewStore.SavePositionAsync(viewName, 0, cancellationToken).ConfigureAwait(false);
        }

        // Replay the entire global stream through all builders
        await ReplayGlobalStreamAsync(
            GlobalStreamPosition.Start,
            allBuilders: true,
            viewNameFilter: null,
            cancellationToken).ConfigureAwait(false);

        LogRebuildCompleted();
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
    public async Task CatchUpAsync(string viewName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewName);

        if (!_viewNameRoutes.ContainsKey(viewName))
        {
            LogViewNotFound(viewName);
            return;
        }

        var lastPosition = await _viewStore.GetPositionAsync(viewName, cancellationToken)
            .ConfigureAwait(false);

        var startPosition = lastPosition.HasValue
            ? new GlobalStreamPosition(lastPosition.Value + 1, DateTimeOffset.MinValue)
            : GlobalStreamPosition.Start;

        LogCatchUpStarting(viewName, startPosition.Position);

        await ReplayGlobalStreamAsync(
            startPosition,
            allBuilders: false,
            viewNameFilter: viewName,
            cancellationToken).ConfigureAwait(false);

        LogCatchUpCompleted(viewName);
    }

    /// <summary>
    /// Replays events from the global stream through the builder pipeline.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
    private async Task ReplayGlobalStreamAsync(
        GlobalStreamPosition startPosition,
        bool allBuilders,
        string? viewNameFilter,
        CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        var currentPosition = startPosition;

        while (!cancellationToken.IsCancellationRequested)
        {
            var storedEvents = await _globalStreamQuery.ReadAllAsync(
                currentPosition,
                opts.BatchSize,
                cancellationToken).ConfigureAwait(false);

            if (storedEvents.Count == 0)
            {
                break; // Caught up — no more events
            }

            foreach (var storedEvent in storedEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
                    var domainEvent = _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType);

                    if (domainEvent is null)
                    {
                        continue;
                    }

                    if (allBuilders)
                    {
                        // Rebuild mode: route to all matching builders
                        if (_eventTypeRoutes.TryGetValue(eventType, out var routes))
                        {
                            foreach (var route in routes)
                            {
                                await ApplyEventToBuilderAsync(route.Registration, domainEvent, cancellationToken)
                                    .ConfigureAwait(false);
                            }
                        }
                    }
                    else if (viewNameFilter is not null)
                    {
                        // Catch-up mode: route only to builders for the specified view
                        if (_eventTypeRoutes.TryGetValue(eventType, out var routes))
                        {
                            foreach (var route in routes)
                            {
                                if (string.Equals(GetViewName(route.Registration), viewNameFilter, StringComparison.Ordinal))
                                {
                                    await ApplyEventToBuilderAsync(route.Registration, domainEvent, cancellationToken)
                                        .ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogEventProcessingError(storedEvent.EventId, storedEvent.EventType, ex);
                    // Continue processing remaining events
                }
            }

            // Advance position past the last event in the batch
            var lastEvent = storedEvents[storedEvents.Count - 1];
            var newPosition = lastEvent.Version + 1;
            currentPosition = new GlobalStreamPosition(newPosition, lastEvent.Timestamp);

            // Save position checkpoint after each batch
            if (allBuilders)
            {
                foreach (var (viewName, _) in _viewNameRoutes)
                {
                    await _viewStore.SavePositionAsync(viewName, lastEvent.Version, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else if (viewNameFilter is not null)
            {
                await _viewStore.SavePositionAsync(viewNameFilter, lastEvent.Version, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Brief delay between batches to avoid overwhelming the store
            if (opts.BatchDelay > TimeSpan.Zero)
            {
                await Task.Delay(opts.BatchDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Applies a domain event to a builder, loading/creating the view and saving the result.
    /// </summary>
    private async Task ApplyEventToBuilderAsync(
        MaterializedViewBuilderRegistration registration,
        IDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var viewName = GetViewName(registration);
        var viewId = GetViewId(registration, domainEvent);

        if (viewId is null)
        {
            // Builder says this event should not update any view
            return;
        }

        // Use reflection to call the generic GetAsync/SaveAsync on the store.
        // The view type is known at registration time but not at compile time here.
        var viewType = registration.ViewType;

        var existingView = await GetViewFromStoreAsync(viewType, viewName, viewId, cancellationToken)
            .ConfigureAwait(false);

        var view = existingView ?? CreateNewView(registration);

        var updatedView = ApplyEvent(registration, view, domainEvent);

        await SaveViewToStoreAsync(viewType, viewName, viewId, updatedView, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a view from the store using reflection to call the generic method.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "View types are registered at startup; MakeGenericMethod is safe for known types.")]
    [UnconditionalSuppressMessage("Trimming", "IL2060",
        Justification = "View types are registered at startup and preserved by consumer DI configuration.")]
    private async ValueTask<object?> GetViewFromStoreAsync(
        Type viewType,
        string viewName,
        string viewId,
        CancellationToken cancellationToken)
    {
        // IMaterializedViewStore.GetAsync<TView> requires a generic call.
        // We use the generic method pattern from the store interface.
        var method = typeof(IMaterializedViewStore)
            .GetMethod(nameof(IMaterializedViewStore.GetAsync))!
            .MakeGenericMethod(viewType);

        var valueTask = method.Invoke(_viewStore, [viewName, viewId, cancellationToken]);

        // ValueTask<TView?> — we need to await it dynamically
        var awaiter = valueTask!.GetType().GetMethod("GetAwaiter")!.Invoke(valueTask, null);
        var getResult = awaiter!.GetType().GetMethod("GetResult")!;

        // Check if completed synchronously
        var isCompleted = (bool)awaiter.GetType().GetProperty("IsCompleted")!.GetValue(awaiter)!;
        if (isCompleted)
        {
            return getResult.Invoke(awaiter, null);
        }

        // Need to await — convert to Task
        var asTask = valueTask.GetType().GetMethod("AsTask")!.Invoke(valueTask, null);
        await ((Task)asTask!).ConfigureAwait(false);

        // Re-get result after await
        var awaiter2 = valueTask.GetType().GetMethod("GetAwaiter")!.Invoke(valueTask, null);
        return awaiter2!.GetType().GetMethod("GetResult")!.Invoke(awaiter2, null);
    }

    /// <summary>
    /// Saves a view to the store using reflection to call the generic method.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "View types are registered at startup; MakeGenericMethod is safe for known types.")]
    [UnconditionalSuppressMessage("Trimming", "IL2060",
        Justification = "View types are registered at startup and preserved by consumer DI configuration.")]
    private async ValueTask SaveViewToStoreAsync(
        Type viewType,
        string viewName,
        string viewId,
        object view,
        CancellationToken cancellationToken)
    {
        var method = typeof(IMaterializedViewStore)
            .GetMethod(nameof(IMaterializedViewStore.SaveAsync))!
            .MakeGenericMethod(viewType);

        var valueTask = method.Invoke(_viewStore, [viewName, viewId, view, cancellationToken]);

        var awaiter = valueTask!.GetType().GetMethod("GetAwaiter")!.Invoke(valueTask, null);
        var isCompleted = (bool)awaiter!.GetType().GetProperty("IsCompleted")!.GetValue(awaiter)!;

        if (!isCompleted)
        {
            var asTask = valueTask.GetType().GetMethod("AsTask")!.Invoke(valueTask, null);
            await ((Task)asTask!).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the view name from a builder registration by invoking its ViewName property.
    /// </summary>
    private static string GetViewName(MaterializedViewBuilderRegistration registration)
    {
        // The BuilderInstance implements IMaterializedViewBuilder<TView> which has ViewName
        var viewNameProp = registration.BuilderType
            .GetProperty(nameof(IMaterializedViewBuilder<>.ViewName));

        return (string)viewNameProp!.GetValue(registration.BuilderInstance)!;
    }

    /// <summary>
    /// Gets the view ID for an event from a builder registration.
    /// </summary>
    private static string? GetViewId(MaterializedViewBuilderRegistration registration, IDomainEvent @event)
    {
        var getViewIdMethod = registration.BuilderType
            .GetMethod(nameof(IMaterializedViewBuilder<>.GetViewId));

        return (string?)getViewIdMethod!.Invoke(registration.BuilderInstance, [@event]);
    }

    /// <summary>
    /// Creates a new view instance via the builder's CreateNew method.
    /// </summary>
    private static object CreateNewView(MaterializedViewBuilderRegistration registration)
    {
        var createNewMethod = registration.BuilderType
            .GetMethod(nameof(IMaterializedViewBuilder<>.CreateNew));

        return createNewMethod!.Invoke(registration.BuilderInstance, null)!;
    }

    /// <summary>
    /// Applies an event to a view via the builder's Apply method.
    /// </summary>
    private static object ApplyEvent(
        MaterializedViewBuilderRegistration registration,
        object view,
        IDomainEvent @event)
    {
        var applyMethod = registration.BuilderType
            .GetMethod(nameof(IMaterializedViewBuilder<>.Apply));

        return applyMethod!.Invoke(registration.BuilderInstance, [view, @event])!;
    }

    /// <summary>
    /// Builds the event-type and view-name routing maps from builder registrations.
    /// </summary>
    private void BuildRoutingMaps(IEnumerable<MaterializedViewBuilderRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            var viewName = GetViewName(registration);

            // Build view-name route
            if (!_viewNameRoutes.TryGetValue(viewName, out var viewRegistrations))
            {
                viewRegistrations = [];
                _viewNameRoutes[viewName] = viewRegistrations;
            }

            viewRegistrations.Add(registration);

            // Build event-type routes by reading HandledEventTypes from the builder
            var handledTypesProperty = registration.BuilderType
                .GetProperty(nameof(IMaterializedViewBuilder<>.HandledEventTypes));

            if (handledTypesProperty?.GetValue(registration.BuilderInstance) is IReadOnlyList<Type> handledTypes)
            {
                foreach (var eventType in handledTypes)
                {
                    if (!_eventTypeRoutes.TryGetValue(eventType, out var routes))
                    {
                        routes = [];
                        _eventTypeRoutes[eventType] = routes;
                    }

                    routes.Add(new BuilderRoute(viewName, registration));
                }
            }

            LogBuilderRegistered(viewName, registration.BuilderType.Name);
        }

        LogRoutingMapsBuilt(_eventTypeRoutes.Count, _viewNameRoutes.Count);
    }

    /// <summary>
    /// Routing entry mapping a builder registration to a view name.
    /// </summary>
    private readonly record struct BuilderRoute(string ViewName, MaterializedViewBuilderRegistration Registration);

    #region Logging

    [LoggerMessage(EventSourcingEventId.ViewProcessorEventProcessed, LogLevel.Debug,
        "Materialized view processor processed event {EventType} at position {Position}")]
    private partial void LogEventProcessed(string eventType, long position);

    [LoggerMessage(EventSourcingEventId.ViewProcessorBatchProcessed, LogLevel.Debug,
        "Materialized view processor batch completed, {ViewCount} views affected, position {Position}")]
    private partial void LogBatchProcessed(int viewCount, long position);

    [LoggerMessage(EventSourcingEventId.ViewProcessorRebuildStarting, LogLevel.Information,
        "Materialized view rebuild starting")]
    private partial void LogRebuildStarting();

    [LoggerMessage(EventSourcingEventId.ViewProcessorRebuildCompleted, LogLevel.Information,
        "Materialized view rebuild completed")]
    private partial void LogRebuildCompleted();

    [LoggerMessage(EventSourcingEventId.ViewProcessorCatchUpStarting, LogLevel.Information,
        "Materialized view catch-up starting for view {ViewName} from position {Position}")]
    private partial void LogCatchUpStarting(string viewName, long position);

    [LoggerMessage(EventSourcingEventId.ViewProcessorCatchUpCompleted, LogLevel.Information,
        "Materialized view catch-up completed for view {ViewName}")]
    private partial void LogCatchUpCompleted(string viewName);

    [LoggerMessage(EventSourcingEventId.ViewProcessorEventError, LogLevel.Error,
        "Error processing event {EventId} of type {EventType} in materialized view processor")]
    private partial void LogEventProcessingError(string eventId, string eventType, Exception ex);

    [LoggerMessage(EventSourcingEventId.ViewProcessorViewNotFound, LogLevel.Warning,
        "Catch-up requested for unknown view {ViewName}")]
    private partial void LogViewNotFound(string viewName);

    [LoggerMessage(EventSourcingEventId.ViewProcessorBuilderRegistered, LogLevel.Debug,
        "Materialized view builder registered: {ViewName} ({BuilderType})")]
    private partial void LogBuilderRegistered(string viewName, string builderType);

    [LoggerMessage(EventSourcingEventId.ViewProcessorRoutingMapsBuilt, LogLevel.Information,
        "Materialized view routing maps built: {EventTypeCount} event types, {ViewCount} views")]
    private partial void LogRoutingMapsBuilt(int eventTypeCount, int viewCount);

    #endregion Logging
}
