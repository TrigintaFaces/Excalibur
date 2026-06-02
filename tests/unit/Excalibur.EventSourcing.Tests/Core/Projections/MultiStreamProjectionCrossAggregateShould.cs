// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Tests for <see cref="MultiStreamProjection{TProjection}"/> features that support
/// cross-aggregate event routing: key selectors, async handlers, GetHandler,
/// HasKeySelectors, and HasAsyncHandlers.
/// These are the core multi-stream features that the basic tests don't cover.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class MultiStreamProjectionCrossAggregateShould
{
	#region Test Types

	/// <summary>
	/// A dashboard projection that aggregates data from multiple aggregate streams
	/// (orders and payments) keyed by CustomerId.
	/// </summary>
	private sealed class CustomerDashboard
	{
		public string? CustomerId { get; set; }
		public int OrderCount { get; set; }
		public decimal TotalPayments { get; set; }
		public int EventsProcessed { get; set; }
	}

	private sealed record OrderCreatedEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = "order-123";
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(OrderCreatedEvent);
		public IDictionary<string, object>? Metadata { get; init; }
		public string CustomerId { get; init; } = "cust-1";
	}

	private sealed record PaymentReceivedEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = "payment-456";
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(PaymentReceivedEvent);
		public IDictionary<string, object>? Metadata { get; init; }
		public string CustomerId { get; init; } = "cust-1";
		public decimal Amount { get; init; }
	}

	private sealed record ShipmentDispatchedEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = "shipment-789";
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(ShipmentDispatchedEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	#endregion

	// --- Key Selectors ---

	[Fact]
	public void RegisterKeySelector_ForEventType()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();

		// Act
		projection.AddKeySelector<OrderCreatedEvent>(e => e.CustomerId);

		// Assert
		projection.HasKeySelectors.ShouldBeTrue();
	}

	[Fact]
	public void GetKeySelector_ReturnsRegisteredSelector()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();
		projection.AddKeySelector<OrderCreatedEvent>(e => e.CustomerId);

		var evt = new OrderCreatedEvent { CustomerId = "cust-42" };

		// Act
		var selector = projection.GetKeySelector(typeof(OrderCreatedEvent));

		// Assert
		selector.ShouldNotBeNull();
		selector(evt).ShouldBe("cust-42");
	}

	[Fact]
	public void GetKeySelector_ReturnsNull_WhenNotRegistered()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();

		// Act
		var selector = projection.GetKeySelector(typeof(OrderCreatedEvent));

		// Assert
		selector.ShouldBeNull();
	}

	[Fact]
	public void HasKeySelectors_ReturnsFalse_WhenNoneRegistered()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();

		// Assert
		projection.HasKeySelectors.ShouldBeFalse();
	}

	[Fact]
	public void RegisterMultipleKeySelectors_ForDifferentEventTypes()
	{
		// Arrange & Act
		var projection = new MultiStreamProjection<CustomerDashboard>();
		projection.AddKeySelector<OrderCreatedEvent>(e => e.CustomerId);
		projection.AddKeySelector<PaymentReceivedEvent>(e => e.CustomerId);

		// Assert
		projection.HasKeySelectors.ShouldBeTrue();

		var orderSelector = projection.GetKeySelector(typeof(OrderCreatedEvent));
		var paymentSelector = projection.GetKeySelector(typeof(PaymentReceivedEvent));
		var shipmentSelector = projection.GetKeySelector(typeof(ShipmentDispatchedEvent));

		orderSelector.ShouldNotBeNull();
		paymentSelector.ShouldNotBeNull();
		shipmentSelector.ShouldBeNull(); // no selector registered for this type
	}

	[Fact]
	public void KeySelector_RoutesEventsFromDifferentAggregatesToSameProjectionKey()
	{
		// Arrange — the core multi-stream scenario:
		// Events from order-123 and payment-456 both route to "cust-1"
		var projection = new MultiStreamProjection<CustomerDashboard>();
		projection.AddKeySelector<OrderCreatedEvent>(e => e.CustomerId);
		projection.AddKeySelector<PaymentReceivedEvent>(e => e.CustomerId);

		var orderEvt = new OrderCreatedEvent { AggregateId = "order-123", CustomerId = "cust-1" };
		var paymentEvt = new PaymentReceivedEvent { AggregateId = "payment-456", CustomerId = "cust-1" };

		// Act
		var orderKey = projection.GetKeySelector(typeof(OrderCreatedEvent))!(orderEvt);
		var paymentKey = projection.GetKeySelector(typeof(PaymentReceivedEvent))!(paymentEvt);

		// Assert — both events from different aggregates resolve to the same projection key
		orderKey.ShouldBe("cust-1");
		paymentKey.ShouldBe("cust-1");
		orderKey.ShouldBe(paymentKey);
	}

	[Fact]
	public void CrossAggregate_ApplyWithKeySelectors_UpdatesSameProjectionState()
	{
		// Arrange — full cross-aggregate scenario: two event types from different
		// aggregates, with key selectors and sync handlers, updating one projection
		var projection = new MultiStreamProjection<CustomerDashboard>();

		projection.AddHandler<OrderCreatedEvent>((state, evt) =>
		{
			state.CustomerId = evt.CustomerId;
			state.OrderCount++;
			state.EventsProcessed++;
		});
		projection.AddHandler<PaymentReceivedEvent>((state, evt) =>
		{
			state.TotalPayments += evt.Amount;
			state.EventsProcessed++;
		});
		projection.AddKeySelector<OrderCreatedEvent>(e => e.CustomerId);
		projection.AddKeySelector<PaymentReceivedEvent>(e => e.CustomerId);

		var state = new CustomerDashboard();

		// Act — apply events from different aggregates
		var handled1 = projection.Apply(state, new OrderCreatedEvent
		{
			AggregateId = "order-111",
			CustomerId = "cust-1",
		});
		var handled2 = projection.Apply(state, new PaymentReceivedEvent
		{
			AggregateId = "payment-222",
			CustomerId = "cust-1",
			Amount = 49.99m,
		});
		var handled3 = projection.Apply(state, new PaymentReceivedEvent
		{
			AggregateId = "payment-333",
			CustomerId = "cust-1",
			Amount = 25.00m,
		});

		// Assert — state accumulated from events across two different aggregate types
		handled1.ShouldBeTrue();
		handled2.ShouldBeTrue();
		handled3.ShouldBeTrue();
		state.CustomerId.ShouldBe("cust-1");
		state.OrderCount.ShouldBe(1);
		state.TotalPayments.ShouldBe(74.99m);
		state.EventsProcessed.ShouldBe(3);
	}

	// --- GetHandler ---

	[Fact]
	public void GetHandler_ReturnsSyncEntry_WhenRegistered()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();
		projection.AddHandler<OrderCreatedEvent>((state, _) => state.OrderCount++);

		// Act
		var entry = projection.GetHandler(typeof(OrderCreatedEvent));

		// Assert
		entry.ShouldNotBeNull();
		entry.Value.SyncAction.ShouldNotBeNull();
		entry.Value.AsyncHandler.ShouldBeNull();
	}

	[Fact]
	public void GetHandler_ReturnsNull_WhenNotRegistered()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();

		// Act
		var entry = projection.GetHandler(typeof(OrderCreatedEvent));

		// Assert
		entry.ShouldBeNull();
	}

	// --- Async Handlers ---

	[Fact]
	public void AddAsyncHandler_SetsAsyncHandlerEntry()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();

		// Act
		projection.AddAsyncHandler<OrderCreatedEvent>(
			(state, evt, ctx, sp, ct) =>
			{
				state.OrderCount++;
				return Task.CompletedTask;
			});

		// Assert
		var entry = projection.GetHandler(typeof(OrderCreatedEvent));
		entry.ShouldNotBeNull();
		entry.Value.AsyncHandler.ShouldNotBeNull();
		entry.Value.SyncAction.ShouldBeNull();
	}

	[Fact]
	public void HasAsyncHandlers_ReturnsTrue_WhenAsyncHandlerRegistered()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();
		projection.AddAsyncHandler<OrderCreatedEvent>(
			(_, _, _, _, _) => Task.CompletedTask);

		// Assert
		projection.HasAsyncHandlers.ShouldBeTrue();
	}

	[Fact]
	public void HasAsyncHandlers_ReturnsFalse_WhenOnlySyncHandlers()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();
		projection.AddHandler<OrderCreatedEvent>((state, _) => state.OrderCount++);

		// Assert
		projection.HasAsyncHandlers.ShouldBeFalse();
	}

	[Fact]
	public void HasAsyncHandlers_ReturnsFalse_WhenNoHandlers()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();

		// Assert
		projection.HasAsyncHandlers.ShouldBeFalse();
	}

	[Fact]
	public void Apply_ReturnsFalse_ForAsyncOnlyHandler()
	{
		// Arrange — Apply only invokes sync handlers; async handlers require the async path
		var projection = new MultiStreamProjection<CustomerDashboard>();
		projection.AddAsyncHandler<OrderCreatedEvent>(
			(_, _, _, _, _) => Task.CompletedTask);

		var state = new CustomerDashboard();

		// Act
		var result = projection.Apply(state, new OrderCreatedEvent());

		// Assert — Apply skips async-only entries (SyncAction is null)
		result.ShouldBeFalse();
	}

	[Fact]
	public void MixedSyncAndAsyncHandlers_TrackedCorrectly()
	{
		// Arrange
		var projection = new MultiStreamProjection<CustomerDashboard>();
		projection.AddHandler<OrderCreatedEvent>((state, _) => state.OrderCount++);
		projection.AddAsyncHandler<PaymentReceivedEvent>(
			(_, _, _, _, _) => Task.CompletedTask);

		// Assert
		projection.HasAsyncHandlers.ShouldBeTrue();
		projection.HandledEventTypes.Count.ShouldBe(2);
		projection.HandledEventTypes.ShouldContain(typeof(OrderCreatedEvent));
		projection.HandledEventTypes.ShouldContain(typeof(PaymentReceivedEvent));

		// Sync handler entry
		var orderEntry = projection.GetHandler(typeof(OrderCreatedEvent));
		orderEntry.ShouldNotBeNull();
		orderEntry.Value.SyncAction.ShouldNotBeNull();
		orderEntry.Value.AsyncHandler.ShouldBeNull();

		// Async handler entry
		var paymentEntry = projection.GetHandler(typeof(PaymentReceivedEvent));
		paymentEntry.ShouldNotBeNull();
		paymentEntry.Value.SyncAction.ShouldBeNull();
		paymentEntry.Value.AsyncHandler.ShouldNotBeNull();
	}

	[Fact]
	public async Task AsyncHandler_InvokedCorrectly()
	{
		// Arrange
		var invokedState = new CustomerDashboard();
		var projection = new MultiStreamProjection<CustomerDashboard>();
		projection.AddAsyncHandler<PaymentReceivedEvent>(
			(state, evt, ctx, sp, ct) =>
			{
				state.TotalPayments += ((PaymentReceivedEvent)evt).Amount;
				return Task.CompletedTask;
			});

		var entry = projection.GetHandler(typeof(PaymentReceivedEvent));
		entry.ShouldNotBeNull();

		var evt = new PaymentReceivedEvent { Amount = 100m };
		var context = new ProjectionHandlerContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

		// Act — invoke the async handler directly
		await entry.Value.AsyncHandler!(
			invokedState, evt, context, A.Fake<IServiceProvider>(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		invokedState.TotalPayments.ShouldBe(100m);
	}
}
