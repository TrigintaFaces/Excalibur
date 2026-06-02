// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Functional tests for <see cref="MultiStreamProjection{TProjection}"/> covering
/// event routing, handler registration, and projection state updates.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class MultiStreamProjectionFunctionalShould
{
	#region Test Types

	private sealed class OrderSummary
	{
		public int TotalOrders { get; set; }
		public decimal TotalRevenue { get; set; }
		public string? LastCustomerName { get; set; }
	}

	private sealed record OrderPlacedEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = "order-1";
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => "OrderPlaced";
		public IDictionary<string, object>? Metadata { get; init; }
		public string CustomerName { get; init; } = string.Empty;
		public decimal Amount { get; init; }
	}

	private sealed record OrderCancelledEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = "order-1";
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => "OrderCancelled";
		public IDictionary<string, object>? Metadata { get; init; }
	}

	private sealed record UnhandledEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = "agg-1";
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => "Unhandled";
		public IDictionary<string, object>? Metadata { get; init; }
	}

	#endregion

	private static MultiStreamProjection<OrderSummary> CreateProjectionWithHandlers()
	{
		var projection = new MultiStreamProjection<OrderSummary>();
		projection.AddHandler<OrderPlacedEvent>((state, evt) =>
		{
			state.TotalOrders++;
			state.TotalRevenue += evt.Amount;
			state.LastCustomerName = evt.CustomerName;
		});
		projection.AddHandler<OrderCancelledEvent>((state, _) =>
		{
			state.TotalOrders--;
		});
		return projection;
	}

	[Fact]
	public void TrackHandledEventTypesFromRegisteredHandlers()
	{
		// Arrange & Act
		var projection = CreateProjectionWithHandlers();

		// Assert
		projection.HandledEventTypes.Count.ShouldBe(2);
		projection.HandledEventTypes.ShouldContain(typeof(OrderPlacedEvent));
		projection.HandledEventTypes.ShouldContain(typeof(OrderCancelledEvent));
	}

	[Fact]
	public void Apply_ShouldInvokeMatchingHandler()
	{
		// Arrange
		var projection = CreateProjectionWithHandlers();
		var state = new OrderSummary();
		var evt = new OrderPlacedEvent { CustomerName = "Alice", Amount = 99.99m };

		// Act
		var handled = projection.Apply(state, evt);

		// Assert
		handled.ShouldBeTrue();
		state.TotalOrders.ShouldBe(1);
		state.TotalRevenue.ShouldBe(99.99m);
		state.LastCustomerName.ShouldBe("Alice");
	}

	[Fact]
	public void Apply_ShouldReturnFalseForUnhandledEventType()
	{
		// Arrange
		var projection = new MultiStreamProjection<OrderSummary>();
		projection.AddHandler<OrderPlacedEvent>((state, _) => state.TotalOrders++);

		var state = new OrderSummary();
		var evt = new UnhandledEvent();

		// Act
		var handled = projection.Apply(state, evt);

		// Assert
		handled.ShouldBeFalse();
		state.TotalOrders.ShouldBe(0); // State unchanged
	}

	[Fact]
	public void Apply_MultipleEvents_ShouldAccumulateState()
	{
		// Arrange
		var projection = CreateProjectionWithHandlers();
		var state = new OrderSummary();

		// Act
		projection.Apply(state, new OrderPlacedEvent { Amount = 50m });
		projection.Apply(state, new OrderPlacedEvent { Amount = 75m });
		projection.Apply(state, new OrderCancelledEvent());

		// Assert
		state.TotalOrders.ShouldBe(1);
		state.TotalRevenue.ShouldBe(125m);
	}

	[Fact]
	public void Apply_ShouldThrowOnNullProjection()
	{
		var projection = new MultiStreamProjection<OrderSummary>();
		projection.AddHandler<OrderPlacedEvent>((state, _) => { });

		Should.Throw<ArgumentNullException>(() =>
			projection.Apply(null!, new OrderPlacedEvent()));
	}

	[Fact]
	public void Apply_ShouldThrowOnNullEvent()
	{
		var projection = new MultiStreamProjection<OrderSummary>();
		projection.AddHandler<OrderPlacedEvent>((state, _) => { });

		Should.Throw<ArgumentNullException>(() =>
			projection.Apply(new OrderSummary(), null!));
	}
}
