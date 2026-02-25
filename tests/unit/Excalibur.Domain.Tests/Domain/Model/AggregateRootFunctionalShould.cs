// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

// ── Test domain events ──

public record OrderCreated(string OrderId, decimal Total) : DomainEventBase
{
    public override string AggregateId => OrderId;
}

public record OrderItemAdded(string OrderId, string ItemId, int Quantity) : DomainEventBase
{
    public override string AggregateId => OrderId;
}

public record OrderShipped(string OrderId, DateTimeOffset ShippedAt) : DomainEventBase
{
    public override string AggregateId => OrderId;
}

public record OrderCancelled(string OrderId, string Reason) : DomainEventBase
{
    public override string AggregateId => OrderId;
}

// ── Test snapshot ──

public record OrderSnapshot : ISnapshot
{
    public string SnapshotId { get; init; } = Guid.NewGuid().ToString();
    public string AggregateId { get; init; } = string.Empty;
    public long Version { get; init; }
    public string AggregateType { get; init; } = nameof(OrderAggregate);
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public byte[] Data { get; init; } = [];
    public IDictionary<string, object>? Metadata { get; init; }
    public decimal Total { get; init; }
    public int ItemCount { get; init; }
    public bool IsShipped { get; init; }
}

// ── Test aggregate ──

public class OrderAggregate : AggregateRoot
{
    public decimal Total { get; private set; }
    public int ItemCount { get; private set; }
    public bool IsShipped { get; private set; }
    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }

    public OrderAggregate() { }

    public OrderAggregate(string id) : base(id) { }

    public void Create(string orderId, decimal total)
    {
        RaiseEvent(new OrderCreated(orderId, total));
    }

    public void AddItem(string itemId, int quantity)
    {
        RaiseEvent(new OrderItemAdded(Id, itemId, quantity));
    }

    public void Ship()
    {
        RaiseEvent(new OrderShipped(Id, DateTimeOffset.UtcNow));
    }

    public void Cancel(string reason)
    {
        RaiseEvent(new OrderCancelled(Id, reason));
    }

    protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
    {
        OrderCreated e => ApplyCreated(e),
        OrderItemAdded e => ApplyItemAdded(e),
        OrderShipped e => ApplyShipped(e),
        OrderCancelled e => ApplyCancelled(e),
        _ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
    };

    protected override void ApplySnapshot(ISnapshot snapshot)
    {
        if (snapshot is OrderSnapshot s)
        {
            Id = s.AggregateId;
            Total = s.Total;
            ItemCount = s.ItemCount;
            IsShipped = s.IsShipped;
        }
    }

    public override ISnapshot CreateSnapshot() => new OrderSnapshot
    {
        AggregateId = Id,
        Version = Version,
        Total = Total,
        ItemCount = ItemCount,
        IsShipped = IsShipped,
    };

    private bool ApplyCreated(OrderCreated e)
    {
        Id = e.OrderId;
        Total = e.Total;
        return true;
    }

    private bool ApplyItemAdded(OrderItemAdded e)
    {
        ItemCount += e.Quantity;
        return true;
    }

    private bool ApplyShipped(OrderShipped _)
    {
        IsShipped = true;
        return true;
    }

    private bool ApplyCancelled(OrderCancelled e)
    {
        IsCancelled = true;
        CancellationReason = e.Reason;
        return true;
    }
}

// ── Test aggregate with Guid key ──

public class GuidAggregate : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;

    protected override void ApplyEventInternal(IDomainEvent @event)
    {
        // Simple implementation
    }
}

[Trait("Category", "Unit")]
public class AggregateRootFunctionalShould
{
    [Fact]
    public void RaiseEvent_ShouldApplyEventAndTrackAsUncommitted()
    {
        // Arrange
        var aggregate = new OrderAggregate();

        // Act
        aggregate.Create("order-1", 99.99m);

        // Assert
        aggregate.Id.ShouldBe("order-1");
        aggregate.Total.ShouldBe(99.99m);
        aggregate.HasUncommittedEvents.ShouldBeTrue();
        aggregate.GetUncommittedEvents().Count.ShouldBe(1);
        aggregate.GetUncommittedEvents()[0].ShouldBeOfType<OrderCreated>();
    }

    [Fact]
    public void RaiseMultipleEvents_ShouldApplyAllInOrder()
    {
        // Arrange
        var aggregate = new OrderAggregate();

        // Act
        aggregate.Create("order-1", 100m);
        aggregate.AddItem("item-A", 3);
        aggregate.AddItem("item-B", 2);
        aggregate.Ship();

        // Assert
        aggregate.Total.ShouldBe(100m);
        aggregate.ItemCount.ShouldBe(5);
        aggregate.IsShipped.ShouldBeTrue();
        aggregate.GetUncommittedEvents().Count.ShouldBe(4);
    }

    [Fact]
    public void MarkEventsAsCommitted_ShouldClearUncommittedAndIncrementVersion()
    {
        // Arrange
        var aggregate = new OrderAggregate();
        aggregate.Create("order-1", 50m);
        aggregate.AddItem("item-1", 1);

        // Act
        aggregate.MarkEventsAsCommitted();

        // Assert
        aggregate.HasUncommittedEvents.ShouldBeFalse();
        aggregate.GetUncommittedEvents().Count.ShouldBe(0);
        aggregate.Version.ShouldBe(2); // Two events committed
    }

    [Fact]
    public void MarkEventsAsCommitted_ThenRaiseMore_ShouldTrackNewEventsOnly()
    {
        // Arrange
        var aggregate = new OrderAggregate();
        aggregate.Create("order-1", 100m);
        aggregate.MarkEventsAsCommitted();

        // Act
        aggregate.AddItem("item-1", 5);

        // Assert
        aggregate.Version.ShouldBe(1); // Only first event committed
        aggregate.GetUncommittedEvents().Count.ShouldBe(1);
        aggregate.GetUncommittedEvents()[0].ShouldBeOfType<OrderItemAdded>();
    }

    [Fact]
    public void LoadFromHistory_ShouldApplyAllEventsAndSetVersion()
    {
        // Arrange
        var aggregate = new OrderAggregate();
        var history = new IDomainEvent[]
        {
            new OrderCreated("order-1", 200m),
            new OrderItemAdded("order-1", "item-1", 3),
            new OrderShipped("order-1", DateTimeOffset.UtcNow),
        };

        // Act
        aggregate.LoadFromHistory(history);

        // Assert
        aggregate.Id.ShouldBe("order-1");
        aggregate.Total.ShouldBe(200m);
        aggregate.ItemCount.ShouldBe(3);
        aggregate.IsShipped.ShouldBeTrue();
        aggregate.Version.ShouldBe(3);
        aggregate.HasUncommittedEvents.ShouldBeFalse(); // History events are NOT uncommitted
    }

    [Fact]
    public void LoadFromHistory_WithNullHistory_ShouldThrow()
    {
        var aggregate = new OrderAggregate();
        Should.Throw<ArgumentNullException>(() => aggregate.LoadFromHistory(null!));
    }

    [Fact]
    public void LoadFromSnapshot_ShouldRestoreStateAndSetVersion()
    {
        // Arrange
        var aggregate = new OrderAggregate();
        var snapshot = new OrderSnapshot
        {
            AggregateId = "order-42",
            Version = 10,
            Total = 500m,
            ItemCount = 7,
            IsShipped = true,
        };

        // Act
        aggregate.LoadFromSnapshot(snapshot);

        // Assert
        aggregate.Id.ShouldBe("order-42");
        aggregate.Total.ShouldBe(500m);
        aggregate.ItemCount.ShouldBe(7);
        aggregate.IsShipped.ShouldBeTrue();
        aggregate.Version.ShouldBe(10);
    }

    [Fact]
    public void LoadFromSnapshot_ThenApplyMoreEvents_ShouldContinueFromSnapshotState()
    {
        // Arrange
        var aggregate = new OrderAggregate();
        var snapshot = new OrderSnapshot
        {
            AggregateId = "order-42",
            Version = 5,
            Total = 300m,
            ItemCount = 3,
        };

        // Act
        aggregate.LoadFromSnapshot(snapshot);
        aggregate.AddItem("item-new", 2);
        aggregate.Ship();

        // Assert
        aggregate.Version.ShouldBe(5); // Snapshot version; uncommitted events don't increment yet
        aggregate.ItemCount.ShouldBe(5); // 3 from snapshot + 2 new
        aggregate.IsShipped.ShouldBeTrue();
        aggregate.GetUncommittedEvents().Count.ShouldBe(2);
    }

    [Fact]
    public void CreateSnapshot_ShouldCaptureCurrentState()
    {
        // Arrange
        var aggregate = new OrderAggregate();
        aggregate.Create("order-99", 750m);
        aggregate.AddItem("item-1", 4);
        aggregate.MarkEventsAsCommitted();

        // Act
        var snapshot = aggregate.CreateSnapshot();

        // Assert
        snapshot.ShouldBeOfType<OrderSnapshot>();
        var orderSnapshot = (OrderSnapshot)snapshot;
        orderSnapshot.AggregateId.ShouldBe("order-99");
        orderSnapshot.Total.ShouldBe(750m);
        orderSnapshot.ItemCount.ShouldBe(4);
        orderSnapshot.IsShipped.ShouldBeFalse();
    }

    [Fact]
    public void LoadFromSnapshot_WithNull_ShouldThrow()
    {
        var aggregate = new OrderAggregate();
        Should.Throw<ArgumentNullException>(() => aggregate.LoadFromSnapshot(null!));
    }

    [Fact]
    public void ApplyEvent_WithNull_ShouldThrow()
    {
        var aggregate = new OrderAggregate();
        Should.Throw<ArgumentNullException>(() => aggregate.ApplyEvent(null!));
    }

    [Fact]
    public void AggregateType_ShouldReturnClassName()
    {
        var aggregate = new OrderAggregate();
        aggregate.AggregateType.ShouldBe("OrderAggregate");
    }

    [Fact]
    public void ETag_ShouldBeSettable()
    {
        var aggregate = new OrderAggregate();
        aggregate.ETag.ShouldBeNull();

        aggregate.ETag = "etag-123";
        aggregate.ETag.ShouldBe("etag-123");
    }

    [Fact]
    public void GetService_WithMatchingType_ShouldReturnThis()
    {
        var aggregate = new OrderAggregate();
        var result = aggregate.GetService(typeof(OrderAggregate));
        result.ShouldBe(aggregate);
    }

    [Fact]
    public void GetService_WithNonMatchingType_ShouldReturnNull()
    {
        var aggregate = new OrderAggregate();
        var result = aggregate.GetService(typeof(string));
        result.ShouldBeNull();
    }

    [Fact]
    public void GetService_WithNull_ShouldThrow()
    {
        var aggregate = new OrderAggregate();
        Should.Throw<ArgumentNullException>(() => aggregate.GetService(null!));
    }

    [Fact]
    public void StringId_Interface_ShouldReturnStringRepresentation()
    {
        var aggregate = new OrderAggregate();
        aggregate.Create("order-abc", 10m);

        var asInterface = (IAggregateRoot)aggregate;
        asInterface.Id.ShouldBe("order-abc");
    }

    [Fact]
    public void VersionTracking_ShouldBeCorrectAcrossMultipleCommitCycles()
    {
        // Arrange
        var aggregate = new OrderAggregate();

        // First cycle: 2 events
        aggregate.Create("order-1", 100m);
        aggregate.AddItem("item-1", 1);
        aggregate.MarkEventsAsCommitted();
        aggregate.Version.ShouldBe(2);

        // Second cycle: 1 event
        aggregate.AddItem("item-2", 3);
        aggregate.MarkEventsAsCommitted();
        aggregate.Version.ShouldBe(3);

        // Third cycle: 2 events
        aggregate.Ship();
        aggregate.Cancel("test");
        aggregate.MarkEventsAsCommitted();
        aggregate.Version.ShouldBe(5);
    }

    [Fact]
    public void ConstructorWithId_ShouldSetId()
    {
        var aggregate = new OrderAggregate("my-order");
        aggregate.Id.ShouldBe("my-order");
    }

    [Fact]
    public void UnknownEvent_ShouldThrowFromApplyEventInternal()
    {
        var aggregate = new OrderAggregate();
        var unknownEvent = A.Fake<IDomainEvent>();

        Should.Throw<InvalidOperationException>(() => aggregate.ApplyEvent(unknownEvent));
    }
}
