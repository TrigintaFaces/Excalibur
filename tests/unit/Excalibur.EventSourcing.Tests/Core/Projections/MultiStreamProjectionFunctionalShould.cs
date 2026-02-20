// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Projections;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Functional tests for <see cref="MultiStreamProjection{TProjection}"/> and
/// <see cref="MultiStreamProjectionBuilder{TProjection}"/> covering event routing,
/// handler registration, and projection state updates.
/// </summary>
[Trait("Category", "Unit")]
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

	[Fact]
	public void Builder_ShouldBuildProjectionWithStreamsAndHandlers()
	{
		// Arrange & Act
		var projection = new MultiStreamProjectionBuilder<OrderSummary>()
			.FromStream("orders-stream")
			.FromStream("inventory-stream")
			.FromCategory("orders")
			.When<OrderPlacedEvent>((state, evt) => state.TotalOrders++)
			.When<OrderCancelledEvent>((state, evt) => state.TotalOrders--)
			.Build();

		// Assert
		projection.Streams.Count.ShouldBe(2);
		projection.Streams.ShouldContain("orders-stream");
		projection.Streams.ShouldContain("inventory-stream");
		projection.Categories.Count.ShouldBe(1);
		projection.Categories.ShouldContain("orders");
		projection.HandledEventTypes.Count.ShouldBe(2);
		projection.HandledEventTypes.ShouldContain(typeof(OrderPlacedEvent));
		projection.HandledEventTypes.ShouldContain(typeof(OrderCancelledEvent));
	}

	[Fact]
	public void Apply_ShouldInvokeMatchingHandler()
	{
		// Arrange
		var projection = new MultiStreamProjectionBuilder<OrderSummary>()
			.When<OrderPlacedEvent>((state, evt) =>
			{
				state.TotalOrders++;
				state.TotalRevenue += evt.Amount;
				state.LastCustomerName = evt.CustomerName;
			})
			.Build();

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
		var projection = new MultiStreamProjectionBuilder<OrderSummary>()
			.When<OrderPlacedEvent>((state, evt) => state.TotalOrders++)
			.Build();

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
		var projection = new MultiStreamProjectionBuilder<OrderSummary>()
			.When<OrderPlacedEvent>((state, evt) =>
			{
				state.TotalOrders++;
				state.TotalRevenue += evt.Amount;
			})
			.When<OrderCancelledEvent>((state, evt) =>
			{
				state.TotalOrders--;
			})
			.Build();

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
		var projection = new MultiStreamProjectionBuilder<OrderSummary>()
			.When<OrderPlacedEvent>((state, evt) => { })
			.Build();

		Should.Throw<ArgumentNullException>(() =>
			projection.Apply(null!, new OrderPlacedEvent()));
	}

	[Fact]
	public void Apply_ShouldThrowOnNullEvent()
	{
		var projection = new MultiStreamProjectionBuilder<OrderSummary>()
			.When<OrderPlacedEvent>((state, evt) => { })
			.Build();

		Should.Throw<ArgumentNullException>(() =>
			projection.Apply(new OrderSummary(), null!));
	}

	[Fact]
	public void Builder_FromStream_ShouldThrowOnEmptyStreamId()
	{
		Should.Throw<ArgumentException>(() =>
			new MultiStreamProjectionBuilder<OrderSummary>().FromStream(""));
	}

	[Fact]
	public void Builder_FromCategory_ShouldThrowOnEmptyCategory()
	{
		Should.Throw<ArgumentException>(() =>
			new MultiStreamProjectionBuilder<OrderSummary>().FromCategory(""));
	}

	[Fact]
	public void Builder_When_ShouldThrowOnNullHandler()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MultiStreamProjectionBuilder<OrderSummary>()
				.When<OrderPlacedEvent>(null!));
	}
}
