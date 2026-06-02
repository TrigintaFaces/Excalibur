// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Tests for the CRTP <c>AggregateRoot&lt;TAggregate, TKey&gt;</c> base class
/// added in bd-g4lspc. Verifies factory methods, type inference,
/// snapshot compatibility, and non-regression with existing <c>AggregateRoot&lt;TKey&gt;</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class CrtpAggregateRootShould
{
    #region Create Factory Method

    [Fact]
    public void CreateAggregateWithGuidId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var aggregate = TestCrtpGuidAggregate.Create(id);

        // Assert
        aggregate.ShouldNotBeNull();
        aggregate.Id.ShouldBe(id);
        aggregate.ShouldBeOfType<TestCrtpGuidAggregate>();
    }

    [Fact]
    public void CreateAggregateWithStringId()
    {
        // Arrange
        const string id = "order-123";

        // Act
        var aggregate = TestCrtpStringAggregate.Create(id);

        // Assert
        aggregate.ShouldNotBeNull();
        aggregate.Id.ShouldBe(id);
    }

    [Fact]
    public void CreateAggregateWithIntId()
    {
        // Arrange
        const int id = 42;

        // Act
        var aggregate = TestCrtpIntAggregate.Create(id);

        // Assert
        aggregate.ShouldNotBeNull();
        aggregate.Id.ShouldBe(id);
    }

    [Fact]
    public void CreateAggregateWithVersionZero()
    {
        // Arrange & Act
        var aggregate = TestCrtpGuidAggregate.Create(Guid.NewGuid());

        // Assert
        aggregate.Version.ShouldBe(0);
    }

    [Fact]
    public void CreateAggregateWithNoUncommittedEvents()
    {
        // Arrange & Act
        var aggregate = TestCrtpGuidAggregate.Create(Guid.NewGuid());

        // Assert
        aggregate.HasUncommittedEvents.ShouldBeFalse();
        aggregate.GetUncommittedEvents().Count.ShouldBe(0);
    }

    #endregion Create Factory Method

    #region FromEvents Factory Method

    [Fact]
    public void FromEventsReplaysHistoryCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var events = new List<IDomainEvent>
        {
            new TestCrtpEvent(id.ToString(), "First"),
            new TestCrtpEvent(id.ToString(), "Second"),
            new TestCrtpEvent(id.ToString(), "Third"),
        };

        // Act
        var aggregate = TestCrtpGuidAggregate.FromEvents(id, events);

        // Assert
        aggregate.ShouldNotBeNull();
        aggregate.Id.ShouldBe(id);
        aggregate.Version.ShouldBe(3);
        aggregate.LastValue.ShouldBe("Third");
    }

    [Fact]
    public void FromEventsProducesNoUncommittedEvents()
    {
        // Arrange
        var id = Guid.NewGuid();
        var events = new List<IDomainEvent>
        {
            new TestCrtpEvent(id.ToString(), "Value"),
        };

        // Act
        var aggregate = TestCrtpGuidAggregate.FromEvents(id, events);

        // Assert
        aggregate.HasUncommittedEvents.ShouldBeFalse();
    }

    [Fact]
    public void FromEventsWithEmptyHistoryCreatesCleanAggregate()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var aggregate = TestCrtpGuidAggregate.FromEvents(id, []);

        // Assert
        aggregate.Id.ShouldBe(id);
        aggregate.Version.ShouldBe(0);
    }

    #endregion FromEvents Factory Method

    #region IAggregateRoot<TAggregate, TKey> Compliance

    [Fact]
    public void ImplementIAggregateRootInterface()
    {
        // Arrange & Act
        var aggregate = TestCrtpGuidAggregate.Create(Guid.NewGuid());

        // Assert — verify interface implementation via reflection since IAggregateRoot<TAggregate, TKey>
        // has static abstract members that prevent direct ShouldBeAssignableTo<> usage
        var aggregateType = aggregate.GetType();
        aggregateType.GetInterfaces().ShouldContain(
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAggregateRoot<,>),
            "Aggregate should implement IAggregateRoot<TAggregate, TKey>");
        _ = aggregate.ShouldBeAssignableTo<IAggregateRoot<Guid>>();
        _ = aggregate.ShouldBeAssignableTo<IAggregateRoot>();
    }

    [Fact]
    public void InheritFromAggregateRootOfTKey()
    {
        // Arrange & Act
        var aggregate = TestCrtpGuidAggregate.Create(Guid.NewGuid());

        // Assert
        _ = aggregate.ShouldBeAssignableTo<AggregateRoot<Guid>>();
    }

    [Fact]
    public void ExposeStringIdViaIAggregateRoot()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate = TestCrtpGuidAggregate.Create(id);

        // Act
        var stringId = ((IAggregateRoot)aggregate).Id;

        // Assert
        stringId.ShouldBe(id.ToString());
    }

    #endregion IAggregateRoot<TAggregate, TKey> Compliance

    #region Event Application (RaiseEvent)

    [Fact]
    public void RaiseEventAppliesStateAndTracksUncommitted()
    {
        // Arrange
        var aggregate = TestCrtpGuidAggregate.Create(Guid.NewGuid());

        // Act
        aggregate.DoSomething("test-value");

        // Assert
        aggregate.LastValue.ShouldBe("test-value");
        aggregate.HasUncommittedEvents.ShouldBeTrue();
        aggregate.GetUncommittedEvents().Count.ShouldBe(1);
    }

    [Fact]
    public void MarkEventsAsCommittedIncrementsVersion()
    {
        // Arrange
        var aggregate = TestCrtpGuidAggregate.Create(Guid.NewGuid());
        aggregate.DoSomething("v1");
        aggregate.DoSomething("v2");

        // Act
        aggregate.MarkEventsAsCommitted();

        // Assert
        aggregate.Version.ShouldBe(2);
        aggregate.HasUncommittedEvents.ShouldBeFalse();
    }

    #endregion Event Application (RaiseEvent)

    #region Snapshot Compatibility

    [Fact]
    public void SupportSnapshotLoad()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate = TestCrtpSnapshotAggregate.Create(id);
        var snapshot = new TestCrtpSnapshot
        {
            AggregateId = id.ToString(),
            Version = 10,
            AggregateType = nameof(TestCrtpSnapshotAggregate),
            Data = System.Text.Encoding.UTF8.GetBytes("snapshot-data"),
        };

        // Act
        aggregate.LoadFromSnapshot(snapshot);

        // Assert
        aggregate.Version.ShouldBe(10);
        aggregate.SnapshotData.ShouldBe("snapshot-data");
    }

    [Fact]
    public void SupportEventsAfterSnapshotLoad()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate = TestCrtpSnapshotAggregate.Create(id);
        var snapshot = new TestCrtpSnapshot
        {
            AggregateId = id.ToString(),
            Version = 5,
            AggregateType = nameof(TestCrtpSnapshotAggregate),
            Data = System.Text.Encoding.UTF8.GetBytes("initial"),
        };
        aggregate.LoadFromSnapshot(snapshot);

        var trailingEvents = new List<IDomainEvent>
        {
            new TestCrtpEvent(id.ToString(), "After-Snapshot-1"),
            new TestCrtpEvent(id.ToString(), "After-Snapshot-2"),
        };

        // Act
        aggregate.LoadFromHistory(trailingEvents);

        // Assert
        aggregate.Version.ShouldBe(7); // 5 from snapshot + 2 events
        aggregate.LastValue.ShouldBe("After-Snapshot-2");
    }

    #endregion Snapshot Compatibility

    #region Non-Regression: Existing AggregateRoot<TKey>

    [Fact]
    public void NonCrtpAggregateRootStillWorks()
    {
        // Arrange & Act — use a plain AggregateRoot<Guid> (no CRTP)
        var aggregate = new TestNonCrtpGuidAggregate(Guid.NewGuid());

        // Assert
        aggregate.Id.ShouldNotBe(Guid.Empty);
        aggregate.Version.ShouldBe(0);
        _ = aggregate.ShouldBeAssignableTo<AggregateRoot<Guid>>();
        // Non-CRTP aggregate should NOT implement IAggregateRoot<TAggregate, TKey>
        var aggregateType = aggregate.GetType();
        aggregateType.GetInterfaces().ShouldNotContain(
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAggregateRoot<,>),
            "Non-CRTP aggregate should not implement IAggregateRoot<TAggregate, TKey>");
    }

    [Fact]
    public void NonCrtpAggregateCanRaiseEvents()
    {
        // Arrange
        var aggregate = new TestNonCrtpGuidAggregate(Guid.NewGuid());

        // Act
        aggregate.DoSomething("non-crtp-value");

        // Assert
        aggregate.LastValue.ShouldBe("non-crtp-value");
        aggregate.HasUncommittedEvents.ShouldBeTrue();
    }

    #endregion Non-Regression: Existing AggregateRoot<TKey>

    #region Edge Cases

    [Fact]
    public void CreateWithDefaultKeyValue()
    {
        // Arrange & Act — Guid.Empty is a valid (if unusual) ID
        var aggregate = TestCrtpGuidAggregate.Create(Guid.Empty);

        // Assert
        aggregate.Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void OverriddenCreateWorksCorrectly()
    {
        // Arrange & Act
        var id = Guid.NewGuid();
        var aggregate = TestCrtpOverriddenAggregate.Create(id);

        // Assert
        aggregate.Id.ShouldBe(id);
        aggregate.WasCustomCreated.ShouldBeTrue();
    }

    [Fact]
    public void AggregateTypeReturnsConcreteTypeName()
    {
        // Arrange
        var aggregate = TestCrtpGuidAggregate.Create(Guid.NewGuid());

        // Act
        var typeName = aggregate.AggregateType;

        // Assert
        typeName.ShouldBe(nameof(TestCrtpGuidAggregate));
    }

    #endregion Edge Cases

    #region Test Aggregates

    /// <summary>
    /// CRTP aggregate with Guid key — the primary test subject.
    /// </summary>
    private sealed class TestCrtpGuidAggregate : AggregateRoot<TestCrtpGuidAggregate, Guid>
    {
        public string? LastValue { get; private set; }

        public void DoSomething(string value)
        {
            RaiseEvent(new TestCrtpEvent(Id.ToString(), value));
        }

        protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
        {
            TestCrtpEvent e => Apply(e),
            _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}"),
        };

        private bool Apply(TestCrtpEvent e)
        {
            LastValue = e.Value;
            return true;
        }
    }

    /// <summary>
    /// CRTP aggregate with string key.
    /// </summary>
    private sealed class TestCrtpStringAggregate : AggregateRoot<TestCrtpStringAggregate, string>
    {
        protected override void ApplyEventInternal(IDomainEvent @event)
        {
            // No-op for test
        }
    }

    /// <summary>
    /// CRTP aggregate with int key.
    /// </summary>
    private sealed class TestCrtpIntAggregate : AggregateRoot<TestCrtpIntAggregate, int>
    {
        protected override void ApplyEventInternal(IDomainEvent @event)
        {
            // No-op for test
        }
    }

    /// <summary>
    /// CRTP aggregate with snapshot support.
    /// </summary>
    private sealed class TestCrtpSnapshotAggregate : AggregateRoot<TestCrtpSnapshotAggregate, Guid>
    {
        public string? SnapshotData { get; private set; }
        public string? LastValue { get; private set; }

        protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
        {
            TestCrtpEvent e => ApplyEvent(e),
            _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}"),
        };

        private bool ApplyEvent(TestCrtpEvent e)
        {
            LastValue = e.Value;
            return true;
        }

        protected override void ApplySnapshot(ISnapshot snapshot)
        {
            SnapshotData = System.Text.Encoding.UTF8.GetString(snapshot.Data.Span);
        }
    }

    /// <summary>
    /// CRTP aggregate that overrides Create to verify custom factory behavior.
    /// </summary>
    private sealed class TestCrtpOverriddenAggregate : AggregateRoot<TestCrtpOverriddenAggregate, Guid>
    {
        public bool WasCustomCreated { get; private set; }

        public new static TestCrtpOverriddenAggregate Create(Guid id)
        {
            var aggregate = new TestCrtpOverriddenAggregate { Id = id, WasCustomCreated = true };
            return aggregate;
        }

        protected override void ApplyEventInternal(IDomainEvent @event)
        {
            // No-op for test
        }
    }

    /// <summary>
    /// Non-CRTP aggregate for regression testing — uses plain AggregateRoot&lt;Guid&gt;.
    /// </summary>
    private sealed class TestNonCrtpGuidAggregate : AggregateRoot<Guid>
    {
        public string? LastValue { get; private set; }

        public TestNonCrtpGuidAggregate(Guid id) : base(id)
        {
        }

        public void DoSomething(string value)
        {
            RaiseEvent(new TestCrtpEvent(Id.ToString(), value));
        }

        protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
        {
            TestCrtpEvent e => Apply(e),
            _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}"),
        };

        private bool Apply(TestCrtpEvent e)
        {
            LastValue = e.Value;
            return true;
        }
    }

    #endregion Test Aggregates

    #region Test Events

    private sealed record TestCrtpEvent(string AggregateId, string Value) : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        string IDomainEvent.AggregateId => AggregateId;
        public long Version { get; init; } = 1;
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        public string EventType => nameof(TestCrtpEvent);
        public IDictionary<string, object>? Metadata { get; init; } = new Dictionary<string, object>();
    }

    #endregion Test Events

    #region Test Snapshots

    private sealed class TestCrtpSnapshot : ISnapshot
    {
        public string SnapshotId { get; init; } = Guid.NewGuid().ToString();
        public string AggregateId { get; init; } = string.Empty;
        public string AggregateType { get; init; } = string.Empty;
        public long Version { get; init; }
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public ReadOnlyMemory<byte> Data { get; init; }
        public IDictionary<string, object>? Metadata { get; init; }
    }

    #endregion Test Snapshots
}
