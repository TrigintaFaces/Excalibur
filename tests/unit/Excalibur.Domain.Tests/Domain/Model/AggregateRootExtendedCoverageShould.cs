using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class AggregateRootExtendedCoverageShould
{
    [Fact]
    public void DefaultConstructor_InitializeWithDefaults()
    {
        // Act
        var aggregate = new TestAggregate();

        // Assert
        aggregate.Id.ShouldBeNull();
        aggregate.Version.ShouldBe(0);
        aggregate.ETag.ShouldBeNull();
        aggregate.HasUncommittedEvents.ShouldBeFalse();
        aggregate.GetUncommittedEvents().ShouldBeEmpty();
    }

    [Fact]
    public void ConstructorWithId_SetId()
    {
        // Act
        var aggregate = new TestAggregate("test-id");

        // Assert
        aggregate.Id.ShouldBe("test-id");
    }

    [Fact]
    public void AggregateType_ReturnTypeName()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act & Assert
        aggregate.AggregateType.ShouldBe("TestAggregate");
    }

    [Fact]
    public void RaiseEvent_ApplyEventAndAddToUncommitted()
    {
        // Arrange
        var aggregate = new TestAggregate("agg-1");

        // Act
        aggregate.DoSomething("value1");

        // Assert
        aggregate.HasUncommittedEvents.ShouldBeTrue();
        aggregate.GetUncommittedEvents().Count.ShouldBe(1);
        aggregate.LastValue.ShouldBe("value1");
    }

    [Fact]
    public void MarkEventsAsCommitted_ClearUncommittedAndIncrementVersion()
    {
        // Arrange
        var aggregate = new TestAggregate("agg-1");
        aggregate.DoSomething("v1");
        aggregate.DoSomething("v2");
        aggregate.GetUncommittedEvents().Count.ShouldBe(2);

        // Act
        aggregate.MarkEventsAsCommitted();

        // Assert
        aggregate.HasUncommittedEvents.ShouldBeFalse();
        aggregate.GetUncommittedEvents().ShouldBeEmpty();
        aggregate.Version.ShouldBe(2);
    }

    [Fact]
    public void LoadFromHistory_ApplyEventsAndIncrementVersion()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var events = new IDomainEvent[]
        {
            new TestEvent { AggregateId = "agg-1", Value = "h1" },
            new TestEvent { AggregateId = "agg-1", Value = "h2" },
        };

        // Act
        aggregate.LoadFromHistory(events);

        // Assert
        aggregate.Version.ShouldBe(2);
        aggregate.LastValue.ShouldBe("h2");
        aggregate.HasUncommittedEvents.ShouldBeFalse();
    }

    [Fact]
    public void LoadFromHistory_ThrowOnNullHistory()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => aggregate.LoadFromHistory(null!));
    }

    [Fact]
    public void ApplyEvent_ThrowOnNull()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => aggregate.ApplyEvent(null!));
    }

    [Fact]
    public void ApplyEvent_ApplyWithoutAddingToUncommitted()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var evt = new TestEvent { Value = "applied" };

        // Act
        aggregate.ApplyEvent(evt);

        // Assert
        aggregate.LastValue.ShouldBe("applied");
        aggregate.HasUncommittedEvents.ShouldBeFalse();
    }

    [Fact]
    public void LoadFromSnapshot_ApplySnapshotAndSetVersion()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var snapshot = new Snapshot
        {
            SnapshotId = "snap-1",
            AggregateId = "agg-1",
            Version = 10,
            CreatedAt = DateTimeOffset.UtcNow,
            Data = [1, 2, 3],
            AggregateType = "TestAggregate",
        };

        // Act
        aggregate.LoadFromSnapshot(snapshot);

        // Assert
        aggregate.Version.ShouldBe(10);
    }

    [Fact]
    public void LoadFromSnapshot_ThrowOnNull()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => aggregate.LoadFromSnapshot(null!));
    }

    [Fact]
    public void CreateSnapshot_ThrowNotSupportedByDefault()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act & Assert
#pragma warning disable IL2026
#pragma warning disable IL3050
        Should.Throw<NotSupportedException>(() => aggregate.CreateSnapshot());
#pragma warning restore IL3050
#pragma warning restore IL2026
    }

    [Fact]
    public void GetService_ReturnSelfForMatchingType()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act
        var result = aggregate.GetService(typeof(TestAggregate));

        // Assert
        result.ShouldBeSameAs(aggregate);
    }

    [Fact]
    public void GetService_ReturnNullForNonMatchingType()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act
        var result = aggregate.GetService(typeof(string));

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetService_ThrowOnNull()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => aggregate.GetService(null!));
    }

    [Fact]
    public void ExplicitInterfaceId_ReturnStringRepresentation()
    {
        // Arrange
        var aggregate = new TestAggregate("my-id");

        // Act
        var id = ((IAggregateRoot)aggregate).Id;

        // Assert
        id.ShouldBe("my-id");
    }

    [Fact]
    public void ExplicitInterfaceId_ReturnEmptyForNullId()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act
        var id = ((IAggregateRoot)aggregate).Id;

        // Assert
        id.ShouldBe(string.Empty);
    }

    [Fact]
    public void ETag_SetAndGet()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act
        aggregate.ETag = "etag-value";

        // Assert
        aggregate.ETag.ShouldBe("etag-value");
    }

    [Fact]
    public void MultipleRaiseThenCommit_TrackVersionCorrectly()
    {
        // Arrange
        var aggregate = new TestAggregate("agg-1");
        aggregate.DoSomething("v1");
        aggregate.DoSomething("v2");
        aggregate.DoSomething("v3");

        // Act
        aggregate.MarkEventsAsCommitted();

        // Assert
        aggregate.Version.ShouldBe(3);

        // Raise more
        aggregate.DoSomething("v4");
        aggregate.MarkEventsAsCommitted();
        aggregate.Version.ShouldBe(4);
    }

    private sealed class TestAggregate : AggregateRoot
    {
        public string? LastValue { get; private set; }

        public TestAggregate() { }
        public TestAggregate(string id) : base(id) { }

        public void DoSomething(string value) =>
            RaiseEvent(new TestEvent { AggregateId = Id ?? string.Empty, Value = value });

        protected override void ApplyEventInternal(IDomainEvent @event)
        {
            if (@event is TestEvent te)
                LastValue = te.Value;
        }
    }

    private sealed record TestEvent : DomainEventBase
    {
        public string Value { get; init; } = string.Empty;
    }
}
