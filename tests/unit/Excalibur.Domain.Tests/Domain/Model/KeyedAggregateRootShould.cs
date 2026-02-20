// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Unit tests for <see cref="KeyedAggregateRoot{TKey, TBusinessKey}"/>.
/// </summary>
[Trait("Category", "Unit")]
public class KeyedAggregateRootShould
{
	[Fact]
	public void Have_Separate_Id_And_BusinessKey()
	{
		// Arrange
		var orderId = Guid.NewGuid();
		const string orderNumber = "ORD-2026-001";

		// Act
		var aggregate = new TestOrderAggregate(orderId, orderNumber);

		// Assert
		aggregate.Id.ShouldBe(orderId);
		aggregate.BusinessKey.ShouldBe(orderNumber);
		aggregate.Id.ToString().ShouldNotBe(aggregate.BusinessKey);
	}

	[Fact]
	public void Inherit_From_AggregateRoot()
	{
		// Arrange & Act
		var aggregate = new TestOrderAggregate();

		// Assert
		_ = aggregate.ShouldBeAssignableTo<AggregateRoot<Guid>>();
	}

	[Fact]
	public void Support_Event_Application()
	{
		// Arrange
		var aggregate = new TestOrderAggregate();
		var orderId = Guid.NewGuid();
		const string orderNumber = "ORD-2026-002";

		// Act
		aggregate.Create(orderId, orderNumber);

		// Assert
		aggregate.Id.ShouldBe(orderId);
		aggregate.BusinessKey.ShouldBe(orderNumber);
		aggregate.HasUncommittedEvents.ShouldBeTrue();
	}

	[Fact]
	public void Support_Default_Constructor()
	{
		// Arrange & Act
		var aggregate = new TestOrderAggregate();

		// Assert
		aggregate.Id.ShouldBe(Guid.Empty);
		aggregate.BusinessKey.ShouldBe(string.Empty);
	}

	[Fact]
	public void Support_Constructor_With_Id()
	{
		// Arrange
		var orderId = Guid.NewGuid();

		// Act
		var aggregate = new TestOrderAggregate(orderId);

		// Assert
		aggregate.Id.ShouldBe(orderId);
		aggregate.BusinessKey.ShouldBe(string.Empty);
	}

	[Fact]
	public void Support_Constructor_With_Id_And_BusinessKey()
	{
		// Arrange
		var orderId = Guid.NewGuid();
		const string orderNumber = "ORD-2026-003";

		// Act
		var aggregate = new TestOrderAggregate(orderId, orderNumber);

		// Assert
		aggregate.Id.ShouldBe(orderId);
		aggregate.BusinessKey.ShouldBe(orderNumber);
	}

	[Fact]
	public void Track_Uncommitted_Events()
	{
		// Arrange
		var aggregate = new TestOrderAggregate();
		var orderId = Guid.NewGuid();

		// Act
		aggregate.Create(orderId, "ORD-001");
		aggregate.Ship("TRACK-123");

		// Assert
		aggregate.HasUncommittedEvents.ShouldBeTrue();
		aggregate.GetUncommittedEvents().Count.ShouldBe(2);
	}

	[Fact]
	public void Clear_Events_After_Commit()
	{
		// Arrange
		var aggregate = new TestOrderAggregate();
		aggregate.Create(Guid.NewGuid(), "ORD-001");

		// Act
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.HasUncommittedEvents.ShouldBeFalse();
		aggregate.GetUncommittedEvents().Count.ShouldBe(0);
		aggregate.Version.ShouldBe(1);
	}

	[Fact]
	public void Support_History_Replay()
	{
		// Arrange
		var orderId = Guid.NewGuid();
		const string orderNumber = "ORD-2026-004";
		var history = new List<IDomainEvent>
		{
			new TestOrderCreated(orderId, orderNumber),
			new TestOrderShipped("TRACK-456"),
		};

		var aggregate = new TestOrderAggregate();

		// Act
		aggregate.LoadFromHistory(history);

		// Assert
		aggregate.Id.ShouldBe(orderId);
		aggregate.BusinessKey.ShouldBe(orderNumber);
		aggregate.TrackingNumber.ShouldBe("TRACK-456");
		aggregate.Version.ShouldBe(2);
		aggregate.HasUncommittedEvents.ShouldBeFalse();
	}

	[Fact]
	public void Allow_BusinessKey_To_Change_Via_Events()
	{
		// Arrange
		var aggregate = new TestOrderAggregate();
		var orderId = Guid.NewGuid();

		// Act
		aggregate.Create(orderId, "ORD-OLD-001");
		aggregate.UpdateOrderNumber("ORD-NEW-001");

		// Assert
		aggregate.BusinessKey.ShouldBe("ORD-NEW-001");
		aggregate.GetUncommittedEvents().Count.ShouldBe(2);
	}

	[Fact]
	public void Support_Different_BusinessKey_Types()
	{
		// Arrange
		var accountId = Guid.NewGuid();
		const int accountNumber = 12345678;

		// Act
		var aggregate = new TestBankAccountAggregate(accountId, accountNumber);

		// Assert
		aggregate.Id.ShouldBe(accountId);
		aggregate.BusinessKey.ShouldBe(accountNumber);
	}

	#region Test Aggregates

	/// <summary>
	/// Test aggregate for order scenarios with Guid id and string business key.
	/// </summary>
	private sealed class TestOrderAggregate : KeyedAggregateRoot<Guid, string>
	{
		private string _orderNumber = string.Empty;

		public TestOrderAggregate()
		{
		}

		public TestOrderAggregate(Guid id) : base(id)
		{
		}

		public TestOrderAggregate(Guid id, string orderNumber) : base(id)
		{
			_orderNumber = orderNumber;
		}

		public override string BusinessKey => _orderNumber;

		public string? TrackingNumber { get; private set; }

		public void Create(Guid orderId, string orderNumber)
		{
			RaiseEvent(new TestOrderCreated(orderId, orderNumber));
		}

		public void Ship(string trackingNumber)
		{
			RaiseEvent(new TestOrderShipped(trackingNumber));
		}

		public void UpdateOrderNumber(string newOrderNumber)
		{
			RaiseEvent(new TestOrderNumberUpdated(newOrderNumber));
		}

		protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
		{
			TestOrderCreated e => Apply(e),
			TestOrderShipped e => Apply(e),
			TestOrderNumberUpdated e => Apply(e),
			_ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}"),
		};

		private bool Apply(TestOrderCreated e)
		{
			Id = e.OrderId;
			_orderNumber = e.OrderNumber;
			return true;
		}

		private bool Apply(TestOrderShipped e)
		{
			TrackingNumber = e.TrackingNumber;
			return true;
		}

		private bool Apply(TestOrderNumberUpdated e)
		{
			_orderNumber = e.NewOrderNumber;
			return true;
		}
	}

	/// <summary>
	/// Test aggregate with int business key to verify different key types work.
	/// </summary>
	private sealed class TestBankAccountAggregate : KeyedAggregateRoot<Guid, int>
	{
		private int _accountNumber;

		public TestBankAccountAggregate(Guid id, int accountNumber) : base(id)
		{
			_accountNumber = accountNumber;
		}

		public override int BusinessKey => _accountNumber;

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			throw new NotImplementedException();
		}
	}

	#endregion Test Aggregates

	#region Test Events

	private sealed record TestOrderCreated(Guid OrderId, string OrderNumber) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = OrderId.ToString();
		public long Version { get; init; } = 1;
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(TestOrderCreated);
		public IDictionary<string, object>? Metadata { get; init; } = new Dictionary<string, object>();
	}

	private sealed record TestOrderShipped(string TrackingNumber) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; } = 1;
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(TestOrderShipped);
		public IDictionary<string, object>? Metadata { get; init; } = new Dictionary<string, object>();
	}

	private sealed record TestOrderNumberUpdated(string NewOrderNumber) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; } = 1;
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(TestOrderNumberUpdated);
		public IDictionary<string, object>? Metadata { get; init; } = new Dictionary<string, object>();
	}

	#endregion Test Events
}
