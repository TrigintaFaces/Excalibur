// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Depth coverage tests for <see cref="KeyedAggregateRoot{TKey,TBusinessKey}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class KeyedAggregateRootDepthShould
{
	[Fact]
	public void DefaultConstructor_HasDefaultId()
	{
		// Arrange & Act
		var aggregate = new TestOrderAggregate();

		// Assert
		aggregate.Id.ShouldBe(Guid.Empty);
		aggregate.BusinessKey.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_WithId_SetsId()
	{
		// Arrange
		var id = Guid.NewGuid();

		// Act
		var aggregate = new TestOrderAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id);
	}

	[Fact]
	public void BusinessKey_IsDistinctFromId()
	{
		// Arrange
		var aggregate = new TestOrderAggregate();

		// Act
		aggregate.CreateOrder(Guid.NewGuid(), "ORD-001");

		// Assert
		aggregate.Id.ShouldNotBe(Guid.Empty);
		aggregate.BusinessKey.ShouldBe("ORD-001");
	}

	[Fact]
	public void InheritsFromAggregateRoot()
	{
		// Arrange & Act
		var aggregate = new TestOrderAggregate();

		// Assert
		aggregate.ShouldBeAssignableTo<AggregateRoot<Guid>>();
	}

	[Fact]
	public void RaiseEvent_UpdatesBothIdAndBusinessKey()
	{
		// Arrange
		var aggregate = new TestOrderAggregate();
		var id = Guid.NewGuid();

		// Act
		aggregate.CreateOrder(id, "ORD-100");

		// Assert
		aggregate.Id.ShouldBe(id);
		aggregate.BusinessKey.ShouldBe("ORD-100");
		aggregate.HasUncommittedEvents.ShouldBeTrue();
		aggregate.GetUncommittedEvents().Count.ShouldBe(1);
	}

	[Fact]
	public void AggregateType_ReturnsTypeName()
	{
		// Arrange
		var aggregate = new TestOrderAggregate();

		// Act & Assert
		aggregate.AggregateType.ShouldBe(nameof(TestOrderAggregate));
	}

	private sealed class TestOrderAggregate : KeyedAggregateRoot<Guid, string>
	{
		private string _orderNumber = string.Empty;

		public TestOrderAggregate() { }
		public TestOrderAggregate(Guid id) : base(id) { }

		public override string BusinessKey => _orderNumber;

		public void CreateOrder(Guid id, string orderNumber)
		{
			RaiseEvent(new OrderCreatedEvent(id, orderNumber));
		}

		protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
		{
			OrderCreatedEvent e => Apply(e),
			_ => throw new InvalidOperationException(),
		};

		private bool Apply(OrderCreatedEvent e)
		{
			Id = e.OrderId;
			_orderNumber = e.OrderNumber;
			return true;
		}
	}

	private sealed record OrderCreatedEvent(Guid OrderId, string OrderNumber) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId => OrderId.ToString();
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(OrderCreatedEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
