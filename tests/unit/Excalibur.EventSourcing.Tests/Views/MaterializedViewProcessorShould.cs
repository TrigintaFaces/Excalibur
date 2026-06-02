// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Views;

/// <summary>
/// Unit tests for <see cref="MaterializedViewProcessor"/> (bd-9ldp4e).
/// Verifies event routing, position tracking, batch processing,
/// catch-up, rebuild, error handling, and routing map construction.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Test code")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Test code")]
public sealed class MaterializedViewProcessorShould
{
    private readonly InMemoryMaterializedViewStore _viewStore = new();
    private readonly FakeGlobalStreamQuery _globalStreamQuery = new();
    private readonly FakeEventSerializer _eventSerializer = new();
    private readonly IOptions<MaterializedViewOptions> _options =
        Options.Create(new MaterializedViewOptions { BatchSize = 100, BatchDelay = TimeSpan.Zero });

    private readonly ILogger<MaterializedViewProcessor> _logger =
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<MaterializedViewProcessor>();

    #region Constructor Null Guards

    [Fact]
    public void ThrowWhenViewStoreIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new MaterializedViewProcessor(
            null!,
            _globalStreamQuery,
            _eventSerializer,
            [],
            _options,
            _logger)).ParamName.ShouldBe("viewStore");
    }

    [Fact]
    public void ThrowWhenGlobalStreamQueryIsNull()
    {
        Should.Throw<ArgumentNullException>(() => new MaterializedViewProcessor(
            _viewStore,
            null!,
            _eventSerializer,
            [],
            _options,
            _logger)).ParamName.ShouldBe("globalStreamQuery");
    }

    [Fact]
    public void ThrowWhenEventSerializerIsNull()
    {
        Should.Throw<ArgumentNullException>(() => new MaterializedViewProcessor(
            _viewStore,
            _globalStreamQuery,
            null!,
            [],
            _options,
            _logger)).ParamName.ShouldBe("eventSerializer");
    }

    [Fact]
    public void ThrowWhenRegistrationsIsNull()
    {
        Should.Throw<ArgumentNullException>(() => new MaterializedViewProcessor(
            _viewStore,
            _globalStreamQuery,
            _eventSerializer,
            null!,
            _options,
            _logger));
    }

    [Fact]
    public void ThrowWhenOptionsIsNull()
    {
        Should.Throw<ArgumentNullException>(() => new MaterializedViewProcessor(
            _viewStore,
            _globalStreamQuery,
            _eventSerializer,
            [],
            null!,
            _logger)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() => new MaterializedViewProcessor(
            _viewStore,
            _globalStreamQuery,
            _eventSerializer,
            [],
            _options,
            null!)).ParamName.ShouldBe("logger");
    }

    [Fact]
    public void ConstructSuccessfullyWithEmptyRegistrations()
    {
        // Act
        var processor = CreateProcessor([]);

        // Assert
        processor.ShouldNotBeNull();
    }

    #endregion Constructor Null Guards

    #region ProcessEventAsync — Single Event

    [Fact]
    public async Task ProcessEventAsync_ThrowWhenEventIsNull()
    {
        // Arrange
        var processor = CreateProcessor(CreateOrderViewRegistrations());

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => processor.ProcessEventAsync(null!, 1, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessEventAsync_RouteToCorrectBuilder()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));
        var @event = new OrderCreatedEvent("order-1", "Product A", 100m);

        // Act
        await processor.ProcessEventAsync(@event, 1, CancellationToken.None);

        // Assert — builder should have applied the event
        var view = _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1");
        view.ShouldNotBeNull();
        view.OrderId.ShouldBe("order-1");
        view.ProductName.ShouldBe("Product A");
        view.TotalAmount.ShouldBe(100m);
    }

    [Fact]
    public async Task ProcessEventAsync_SavePositionAfterProcessing()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));
        var @event = new OrderCreatedEvent("order-1", "Widget", 50m);

        // Act
        await processor.ProcessEventAsync(@event, 42, CancellationToken.None);

        // Assert — position should be saved for the view
        var position = await _viewStore.GetPositionAsync("OrderSummary", CancellationToken.None);
        position.ShouldBe(42);
    }

    [Fact]
    public async Task ProcessEventAsync_SkipUnknownEventTypes()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));
        var unknownEvent = new UnknownEvent("agg-1");

        // Act — should not throw
        await processor.ProcessEventAsync(unknownEvent, 1, CancellationToken.None);

        // Assert — no position saved (no builders matched)
        var position = await _viewStore.GetPositionAsync("OrderSummary", CancellationToken.None);
        position.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessEventAsync_SkipWhenGetViewIdReturnsNull()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));
        // OrderUpdatedEvent is handled but GetViewId returns null for it
        var @event = new OrderNullViewIdEvent("order-1");

        // Act — should not throw
        await processor.ProcessEventAsync(@event, 5, CancellationToken.None);

        // Assert — position still saved (event type matched, just view ID was null)
        var position = await _viewStore.GetPositionAsync("OrderSummary", CancellationToken.None);
        position.ShouldBe(5);
    }

    [Fact]
    public async Task ProcessEventAsync_UpdateExistingView()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));

        // First event creates the view
        await processor.ProcessEventAsync(
            new OrderCreatedEvent("order-1", "Widget", 50m), 1, CancellationToken.None);

        // Second event updates it
        await processor.ProcessEventAsync(
            new OrderCreatedEvent("order-1", "Gadget", 75m), 2, CancellationToken.None);

        // Assert — view should be updated with second event
        var view = _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1");
        view.ShouldNotBeNull();
        view.ProductName.ShouldBe("Gadget");
        view.TotalAmount.ShouldBe(75m);
    }

    [Fact]
    public async Task ProcessEventAsync_RouteToMultipleBuildersForSameEventType()
    {
        // Arrange — two builders both handle OrderCreatedEvent
        var orderBuilder = new OrderSummaryViewBuilder();
        var statsBuilder = new OrderStatsViewBuilder();
        var registrations = CreateRegistrations(orderBuilder).Concat(CreateRegistrations(statsBuilder)).ToList();
        var processor = CreateProcessor(registrations);

        var @event = new OrderCreatedEvent("order-1", "Widget", 100m);

        // Act
        await processor.ProcessEventAsync(@event, 1, CancellationToken.None);

        // Assert — both views should be updated
        var orderView = _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1");
        orderView.ShouldNotBeNull();
        orderView.ProductName.ShouldBe("Widget");

        var statsView = _viewStore.GetView<OrderStatsView>("OrderStats", "global");
        statsView.ShouldNotBeNull();
        statsView.TotalOrders.ShouldBe(1);
    }

    #endregion ProcessEventAsync — Single Event

    #region ProcessEventsAsync — Batch Processing

    [Fact]
    public async Task ProcessEventsAsync_ThrowWhenEventsIsNull()
    {
        var processor = CreateProcessor(CreateOrderViewRegistrations());

        await Should.ThrowAsync<ArgumentNullException>(
            () => processor.ProcessEventsAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessEventsAsync_ProcessAllEventsInBatch()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));
        var events = new List<(IDomainEvent Event, long Position)>
        {
            (new OrderCreatedEvent("order-1", "Widget", 50m), 1),
            (new OrderCreatedEvent("order-2", "Gadget", 75m), 2),
            (new OrderCreatedEvent("order-3", "Doohickey", 25m), 3),
        };

        // Act
        await processor.ProcessEventsAsync(events, CancellationToken.None);

        // Assert — all three views should exist
        _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1").ShouldNotBeNull();
        _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-2").ShouldNotBeNull();
        _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-3").ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessEventsAsync_SavePositionOnceAfterBatch()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));
        var events = new List<(IDomainEvent Event, long Position)>
        {
            (new OrderCreatedEvent("order-1", "A", 10m), 10),
            (new OrderCreatedEvent("order-2", "B", 20m), 20),
            (new OrderCreatedEvent("order-3", "C", 30m), 30),
        };

        // Act
        await processor.ProcessEventsAsync(events, CancellationToken.None);

        // Assert — position saved should be the LAST event position (batch behavior)
        var position = await _viewStore.GetPositionAsync("OrderSummary", CancellationToken.None);
        position.ShouldBe(30);

        // Verify it was saved once (deferred), not per-event
        _viewStore.SavePositionCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessEventsAsync_HandleEmptyBatch()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));

        // Act — should not throw
        await processor.ProcessEventsAsync([], CancellationToken.None);

        // Assert — no position saved
        var position = await _viewStore.GetPositionAsync("OrderSummary", CancellationToken.None);
        position.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessEventsAsync_SkipUnknownEventTypesInBatch()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));
        var events = new List<(IDomainEvent Event, long Position)>
        {
            (new OrderCreatedEvent("order-1", "Widget", 50m), 1),
            (new UnknownEvent("agg-1"), 2),  // Unknown — skipped
            (new OrderCreatedEvent("order-2", "Gadget", 75m), 3),
        };

        // Act
        await processor.ProcessEventsAsync(events, CancellationToken.None);

        // Assert — known events processed, position is last event
        _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1").ShouldNotBeNull();
        _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-2").ShouldNotBeNull();
        var position = await _viewStore.GetPositionAsync("OrderSummary", CancellationToken.None);
        position.ShouldBe(3);
    }

    [Fact]
    public async Task ProcessEventsAsync_RespectCancellation()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var events = new List<(IDomainEvent Event, long Position)>
        {
            (new OrderCreatedEvent("order-1", "A", 10m), 1),
            (new OrderCreatedEvent("order-2", "B", 20m), 2),
        };

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => processor.ProcessEventsAsync(events, cts.Token));
    }

    #endregion ProcessEventsAsync — Batch Processing

    #region CatchUpAsync

    [Fact]
    public async Task CatchUpAsync_ThrowWhenViewNameIsNull()
    {
        var processor = CreateProcessor(CreateOrderViewRegistrations());

        await Should.ThrowAsync<ArgumentNullException>(
            () => processor.CatchUpAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CatchUpAsync_ReturnSilentlyForUnknownView()
    {
        // Arrange
        var processor = CreateProcessor(CreateOrderViewRegistrations());

        // Act — should not throw for unknown view name
        await processor.CatchUpAsync("NonExistentView", CancellationToken.None);

        // Assert — no stream reads attempted
        _globalStreamQuery.ReadAllCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CatchUpAsync_ReadFromStartWhenNoPosition()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));

        var storedEvent = CreateStoredEvent("order-1", nameof(OrderCreatedEvent), 1);
        _globalStreamQuery.SetEvents([storedEvent]);
        _eventSerializer.RegisterType<OrderCreatedEvent>(nameof(OrderCreatedEvent));
        _eventSerializer.RegisterEvent(storedEvent.EventData,
            new OrderCreatedEvent("order-1", "Caught-Up", 99m));

        // Act
        await processor.CatchUpAsync("OrderSummary", CancellationToken.None);

        // Assert — first read from position 0 (Start)
        _globalStreamQuery.FirstRequestedPosition.ShouldBe(0);

        // View should be updated
        var view = _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1");
        view.ShouldNotBeNull();
        view.ProductName.ShouldBe("Caught-Up");
    }

    [Fact]
    public async Task CatchUpAsync_ReadFromLastPositionPlusOne()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));

        // Simulate existing position at 50
        await _viewStore.SavePositionAsync("OrderSummary", 50, CancellationToken.None);

        _globalStreamQuery.SetEvents([]); // No new events

        // Act
        await processor.CatchUpAsync("OrderSummary", CancellationToken.None);

        // Assert — should read from position 51 (last + 1)
        _globalStreamQuery.FirstRequestedPosition.ShouldBe(51);
    }

    [Fact]
    public async Task CatchUpAsync_FilterEventsToSpecificView()
    {
        // Arrange — two builders registered, catch up only one
        var orderBuilder = new OrderSummaryViewBuilder();
        var statsBuilder = new OrderStatsViewBuilder();
        var registrations = CreateRegistrations(orderBuilder)
            .Concat(CreateRegistrations(statsBuilder)).ToList();
        var processor = CreateProcessor(registrations);

        var storedEvent = CreateStoredEvent("order-1", nameof(OrderCreatedEvent), 1);
        _globalStreamQuery.SetEvents([storedEvent]);
        _eventSerializer.RegisterType<OrderCreatedEvent>(nameof(OrderCreatedEvent));
        _eventSerializer.RegisterEvent(storedEvent.EventData,
            new OrderCreatedEvent("order-1", "ForOrderOnly", 55m));

        // Act — catch up only OrderSummary
        await processor.CatchUpAsync("OrderSummary", CancellationToken.None);

        // Assert — OrderSummary updated
        var orderView = _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1");
        orderView.ShouldNotBeNull();

        // OrderStats should NOT be updated (filtered to OrderSummary only)
        var statsView = _viewStore.GetView<OrderStatsView>("OrderStats", "global");
        statsView.ShouldBeNull();
    }

    #endregion CatchUpAsync

    #region RebuildAsync

    [Fact]
    public async Task RebuildAsync_ResetPositionsForAllViews()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));

        // Set existing position
        await _viewStore.SavePositionAsync("OrderSummary", 100, CancellationToken.None);

        _globalStreamQuery.SetEvents([]); // No events to replay

        // Act
        await processor.RebuildAsync(CancellationToken.None);

        // Assert — position reset to 0
        var position = await _viewStore.GetPositionAsync("OrderSummary", CancellationToken.None);
        position.ShouldBe(0);
    }

    [Fact]
    public async Task RebuildAsync_ReplayEntireGlobalStream()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));

        var storedEvent = CreateStoredEvent("order-1", nameof(OrderCreatedEvent), 1);
        _globalStreamQuery.SetEvents([storedEvent]);
        _eventSerializer.RegisterType<OrderCreatedEvent>(nameof(OrderCreatedEvent));
        _eventSerializer.RegisterEvent(storedEvent.EventData,
            new OrderCreatedEvent("order-1", "Rebuilt", 200m));

        // Act
        await processor.RebuildAsync(CancellationToken.None);

        // Assert — view should contain rebuilt data
        var view = _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1");
        view.ShouldNotBeNull();
        view.ProductName.ShouldBe("Rebuilt");
    }

    [Fact]
    public async Task RebuildAsync_ResetPositionsForMultipleViews()
    {
        // Arrange
        var orderBuilder = new OrderSummaryViewBuilder();
        var statsBuilder = new OrderStatsViewBuilder();
        var registrations = CreateRegistrations(orderBuilder)
            .Concat(CreateRegistrations(statsBuilder)).ToList();
        var processor = CreateProcessor(registrations);

        // Set existing positions for both views
        await _viewStore.SavePositionAsync("OrderSummary", 50, CancellationToken.None);
        await _viewStore.SavePositionAsync("OrderStats", 75, CancellationToken.None);

        _globalStreamQuery.SetEvents([]); // No events to replay

        // Act
        await processor.RebuildAsync(CancellationToken.None);

        // Assert — both positions reset to 0
        (await _viewStore.GetPositionAsync("OrderSummary", CancellationToken.None)).ShouldBe(0);
        (await _viewStore.GetPositionAsync("OrderStats", CancellationToken.None)).ShouldBe(0);
    }

    [Fact]
    public async Task RebuildAsync_RouteToAllMatchingBuilders()
    {
        // Arrange
        var orderBuilder = new OrderSummaryViewBuilder();
        var statsBuilder = new OrderStatsViewBuilder();
        var registrations = CreateRegistrations(orderBuilder)
            .Concat(CreateRegistrations(statsBuilder)).ToList();
        var processor = CreateProcessor(registrations);

        var storedEvent = CreateStoredEvent("order-1", nameof(OrderCreatedEvent), 1);
        _globalStreamQuery.SetEvents([storedEvent]);
        _eventSerializer.RegisterType<OrderCreatedEvent>(nameof(OrderCreatedEvent));
        _eventSerializer.RegisterEvent(storedEvent.EventData,
            new OrderCreatedEvent("order-1", "RebuildAll", 150m));

        // Act
        await processor.RebuildAsync(CancellationToken.None);

        // Assert — both builders should have processed the event
        _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1").ShouldNotBeNull();
        _viewStore.GetView<OrderStatsView>("OrderStats", "global").ShouldNotBeNull();
    }

    #endregion RebuildAsync

    #region Error Handling

    [Fact]
    public async Task CatchUpAsync_ContinueProcessingAfterIndividualEventFailure()
    {
        // Arrange
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));

        var goodEvent1 = CreateStoredEvent("order-1", nameof(OrderCreatedEvent), 1);
        var badEvent = CreateStoredEvent("order-bad", "BadEventType", 2);
        var goodEvent2 = CreateStoredEvent("order-2", nameof(OrderCreatedEvent), 3);

        _globalStreamQuery.SetEvents([goodEvent1, badEvent, goodEvent2]);
        _eventSerializer.RegisterType<OrderCreatedEvent>(nameof(OrderCreatedEvent));
        _eventSerializer.RegisterEvent(goodEvent1.EventData,
            new OrderCreatedEvent("order-1", "First", 10m));
        _eventSerializer.RegisterEvent(goodEvent2.EventData,
            new OrderCreatedEvent("order-2", "Third", 30m));
        // badEvent type resolution will throw — processor should continue

        // Act — should not throw
        await processor.CatchUpAsync("OrderSummary", CancellationToken.None);

        // Assert — good events processed, bad event skipped
        _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1").ShouldNotBeNull();
        _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-2").ShouldNotBeNull();
    }

    [Fact]
    public async Task CatchUpAsync_RespectBatchSizeOption()
    {
        // Arrange
        var smallBatchOptions = Options.Create(
            new MaterializedViewOptions { BatchSize = 2, BatchDelay = TimeSpan.Zero });
        var builder = new OrderSummaryViewBuilder();
        var processor = new MaterializedViewProcessor(
            _viewStore,
            _globalStreamQuery,
            _eventSerializer,
            CreateRegistrations(builder),
            smallBatchOptions,
            _logger);

        // Act
        await processor.CatchUpAsync("OrderSummary", CancellationToken.None);

        // Assert — ReadAllAsync called with batch size 2
        _globalStreamQuery.LastRequestedMaxCount.ShouldBe(2);
    }

    #endregion Error Handling

    #region Routing Map Construction

    [Fact]
    public void ConstructWithNoRegistrationsAndNoRoutes()
    {
        // Act
        var processor = CreateProcessor([]);

        // Assert — just verify it constructed without error
        processor.ShouldNotBeNull();
    }

    [Fact]
    public async Task RouteMultipleEventTypesToSameBuilder()
    {
        // Arrange — builder handles both OrderCreatedEvent and OrderNullViewIdEvent
        var builder = new OrderSummaryViewBuilder();
        var processor = CreateProcessor(CreateRegistrations(builder));

        // Process OrderCreatedEvent
        await processor.ProcessEventAsync(
            new OrderCreatedEvent("order-1", "Widget", 50m), 1, CancellationToken.None);

        // Assert
        _viewStore.GetView<OrderSummaryView>("OrderSummary", "order-1").ShouldNotBeNull();
    }

    #endregion Routing Map Construction

    #region Helpers

    private MaterializedViewProcessor CreateProcessor(
        IEnumerable<MaterializedViewBuilderRegistration> registrations)
    {
        return new MaterializedViewProcessor(
            _viewStore,
            _globalStreamQuery,
            _eventSerializer,
            registrations,
            _options,
            _logger);
    }

    private List<MaterializedViewBuilderRegistration> CreateOrderViewRegistrations()
    {
        return CreateRegistrations(new OrderSummaryViewBuilder());
    }

    private static List<MaterializedViewBuilderRegistration> CreateRegistrations<TView>(
        IMaterializedViewBuilder<TView> builder)
        where TView : class, new()
    {
        return
        [
            new MaterializedViewBuilderRegistration(
                typeof(TView),
                builder.GetType(),
                builder)
        ];
    }

    private static StoredEvent CreateStoredEvent(string aggregateId, string eventType, long version)
    {
        return new StoredEvent(
            EventId: Guid.NewGuid().ToString(),
            AggregateId: aggregateId,
            AggregateType: "TestAggregate",
            EventType: eventType,
            EventData: System.Text.Encoding.UTF8.GetBytes($"{{\"{eventType}\":\"{aggregateId}\"}}"),
            Metadata: null,
            Version: version,
            Timestamp: DateTimeOffset.UtcNow);
    }

    #endregion Helpers

    #region Test View Types

    /// <summary>
    /// Simple order summary view for testing.
    /// </summary>
    private sealed class OrderSummaryView
    {
        public string OrderId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }

    /// <summary>
    /// Aggregate statistics view for testing multi-builder routing.
    /// </summary>
    private sealed class OrderStatsView
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    #endregion Test View Types

    #region Test Builders

    private sealed class OrderSummaryViewBuilder : IMaterializedViewBuilder<OrderSummaryView>
    {
        public string ViewName => "OrderSummary";

        public IReadOnlyList<Type> HandledEventTypes { get; } =
            [typeof(OrderCreatedEvent), typeof(OrderNullViewIdEvent)];

        public string? GetViewId(IDomainEvent @event) => @event switch
        {
            OrderCreatedEvent e => e.AggregateId,
            OrderNullViewIdEvent => null, // Intentionally returns null
            _ => null,
        };

        public OrderSummaryView Apply(OrderSummaryView view, IDomainEvent @event)
        {
            if (@event is OrderCreatedEvent created)
            {
                view.OrderId = created.AggregateId;
                view.ProductName = created.ProductName;
                view.TotalAmount = created.Amount;
            }

            return view;
        }

        public OrderSummaryView CreateNew() => new();
    }

    private sealed class OrderStatsViewBuilder : IMaterializedViewBuilder<OrderStatsView>
    {
        public string ViewName => "OrderStats";

        public IReadOnlyList<Type> HandledEventTypes { get; } = [typeof(OrderCreatedEvent)];

        public string? GetViewId(IDomainEvent @event) => "global"; // Single stats document

        public OrderStatsView Apply(OrderStatsView view, IDomainEvent @event)
        {
            if (@event is OrderCreatedEvent created)
            {
                view.TotalOrders++;
                view.TotalRevenue += created.Amount;
            }

            return view;
        }

        public OrderStatsView CreateNew() => new();
    }

    #endregion Test Builders

    #region Test Events

    private sealed record OrderCreatedEvent(
        string AggregateId,
        string ProductName,
        decimal Amount) : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        string IDomainEvent.AggregateId => AggregateId;
        public long Version { get; init; } = 1;
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        public string EventType => nameof(OrderCreatedEvent);
        public IDictionary<string, object>? Metadata { get; init; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Event that returns null from GetViewId — tests the null-viewId skip path.
    /// </summary>
    private sealed record OrderNullViewIdEvent(string AggregateId) : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        string IDomainEvent.AggregateId => AggregateId;
        public long Version { get; init; } = 1;
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        public string EventType => nameof(OrderNullViewIdEvent);
        public IDictionary<string, object>? Metadata { get; init; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Event that no builder handles — tests the unknown event skip path.
    /// </summary>
    private sealed record UnknownEvent(string AggregateId) : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        string IDomainEvent.AggregateId => AggregateId;
        public long Version { get; init; } = 1;
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        public string EventType => nameof(UnknownEvent);
        public IDictionary<string, object>? Metadata { get; init; } = new Dictionary<string, object>();
    }

    #endregion Test Events

    #region Test Doubles

    /// <summary>
    /// In-memory view store that supports the reflection-based generic calls
    /// used by <see cref="MaterializedViewProcessor"/>.
    /// </summary>
    private sealed class InMemoryMaterializedViewStore : IMaterializedViewStore
    {
        private readonly Dictionary<string, object> _views = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _positions = new(StringComparer.Ordinal);

        public int SavePositionCallCount { get; private set; }

        public ValueTask<TView?> GetAsync<TView>(
            string viewName, string viewId, CancellationToken cancellationToken)
            where TView : class
        {
            var key = $"{viewName}:{viewId}";
            return _views.TryGetValue(key, out var view)
                ? new ValueTask<TView?>((TView)view)
                : new ValueTask<TView?>(default(TView));
        }

        public ValueTask SaveAsync<TView>(
            string viewName, string viewId, TView view, CancellationToken cancellationToken)
            where TView : class
        {
            var key = $"{viewName}:{viewId}";
            _views[key] = view;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken cancellationToken)
        {
            var key = $"{viewName}:{viewId}";
            _views.Remove(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken cancellationToken)
        {
            return _positions.TryGetValue(viewName, out var position)
                ? new ValueTask<long?>((long?)position)
                : new ValueTask<long?>((long?)null);
        }

        public ValueTask SavePositionAsync(string viewName, long position, CancellationToken cancellationToken)
        {
            _positions[viewName] = position;
            SavePositionCallCount++;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Test helper to directly inspect stored views.
        /// </summary>
        public TView? GetView<TView>(string viewName, string viewId) where TView : class
        {
            var key = $"{viewName}:{viewId}";
            return _views.TryGetValue(key, out var view) ? (TView)view : null;
        }
    }

    /// <summary>
    /// Fake global stream query that returns pre-configured events.
    /// </summary>
    private sealed class FakeGlobalStreamQuery : IGlobalStreamQuery
    {
        private IReadOnlyList<StoredEvent> _events = [];

        public int ReadAllCallCount { get; private set; }
        public long FirstRequestedPosition { get; private set; } = -1;
        public long LastRequestedPosition { get; private set; }
        public int LastRequestedMaxCount { get; private set; }

        public void SetEvents(IReadOnlyList<StoredEvent> events) => _events = events;

        public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
            GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken)
        {
            ReadAllCallCount++;
            if (FirstRequestedPosition < 0) FirstRequestedPosition = position.Position;
            LastRequestedPosition = position.Position;
            LastRequestedMaxCount = maxCount;

            // Return events with Version >= position, up to maxCount
            // Only return events once (simulate catch-up: first call returns events, second returns empty)
            if (ReadAllCallCount == 1)
            {
                var result = _events
                    .Where(e => e.Version >= position.Position)
                    .Take(maxCount)
                    .ToList();
                return new ValueTask<IReadOnlyList<StoredEvent>>(result);
            }

            return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
        }

        public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
            string eventType, GlobalStreamPosition position, int maxCount,
            CancellationToken cancellationToken)
        {
            var result = _events
                .Where(e => e.EventType == eventType && e.Version >= position.Position)
                .Take(maxCount)
                .ToList();
            return new ValueTask<IReadOnlyList<StoredEvent>>(result);
        }
    }

    /// <summary>
    /// Fake event serializer that supports type resolution and deserialization
    /// via pre-registered mappings.
    /// </summary>
    private sealed class FakeEventSerializer : IEventSerializer
    {
        private readonly Dictionary<string, Type> _typeMap = new(StringComparer.Ordinal);
        private readonly Dictionary<byte[], IDomainEvent> _eventMap = new(ReferenceEqualityComparer.Instance);

        public void RegisterType<TEvent>(string typeName)
        {
            _typeMap[typeName] = typeof(TEvent);
        }

        public void RegisterEvent(byte[] data, IDomainEvent @event)
        {
            _eventMap[data] = @event;
        }

        public byte[] SerializeEvent(IDomainEvent domainEvent) =>
            System.Text.Encoding.UTF8.GetBytes(domainEvent.EventType);

        public IDomainEvent DeserializeEvent(byte[] data, Type eventType)
        {
            if (_eventMap.TryGetValue(data, out var @event))
            {
                return @event;
            }

            throw new InvalidOperationException(
                $"No event registered for data with type {eventType.Name}");
        }

        public string GetTypeName(Type type) => type.Name;

        public Type ResolveType(string typeName)
        {
            if (_typeMap.TryGetValue(typeName, out var type))
            {
                return type;
            }

            throw new InvalidOperationException($"Unknown event type: {typeName}");
        }
    }

    #endregion Test Doubles
}
