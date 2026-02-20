// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Unit tests for <see cref="AggregateRoot"/> and <see cref="AggregateRoot{TKey}"/> base classes.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AggregateRootShould
{
	#region Test Domain Events

	internal sealed record SomethingHappened : DomainEvent
	{
		public string Value { get; init; } = string.Empty;

		public SomethingHappened(string aggregateId, long version, string value)
			: base(aggregateId, version, TimeProvider.System)
		{
			Value = value;
		}

		public SomethingHappened() : base("", 0, TimeProvider.System) { }
	}

	internal sealed record SomethingElseHappened : DomainEvent
	{
		public int Number { get; init; }

		public SomethingElseHappened(string aggregateId, long version, int number)
			: base(aggregateId, version, TimeProvider.System)
		{
			Number = number;
		}

		public SomethingElseHappened() : base("", 0, TimeProvider.System) { }
	}

	#endregion Test Domain Events

	#region Test Aggregates

	/// <summary>
	/// Test aggregate using string key (default AggregateRoot).
	/// </summary>
	internal sealed class TestAggregate : AggregateRoot
	{
		public TestAggregate()
		{ }

		public TestAggregate(string id) : base(id)
		{
		}

		public string CurrentValue { get; private set; } = string.Empty;
		public int CurrentNumber { get; private set; }
		public int ApplyCount { get; private set; }

		public void DoSomething(string value)
		{
			RaiseEvent(new SomethingHappened(Id, Version, value));
		}

		public void DoSomethingElse(int number)
		{
			RaiseEvent(new SomethingElseHappened(Id, Version, number));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			ApplyCount++;
			_ = @event switch
			{
				SomethingHappened e => Apply(e),
				SomethingElseHappened e => Apply(e),
				_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
			};
		}

		private bool Apply(SomethingHappened e)
		{
			CurrentValue = e.Value;
			return true;
		}

		private bool Apply(SomethingElseHappened e)
		{
			CurrentNumber = e.Number;
			return true;
		}
	}

	/// <summary>
	/// Test aggregate with snapshot support.
	/// </summary>
	internal sealed class SnapshottableAggregate : AggregateRoot
	{
		public SnapshottableAggregate()
		{ }

		public SnapshottableAggregate(string id) : base(id)
		{
		}

		public string State { get; private set; } = string.Empty;
		public bool SnapshotApplied { get; private set; }

		public void SetState(string state)
		{
			RaiseEvent(new SomethingHappened(Id, Version, state));
		}

		public override ISnapshot CreateSnapshot()
		{
			return new TestSnapshot
			{
				AggregateId = Id,
				AggregateType = AggregateType,
				Version = Version,
				Data = System.Text.Encoding.UTF8.GetBytes(State)
			};
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is SomethingHappened e)
				State = e.Value;
		}

		protected override void ApplySnapshot(ISnapshot snapshot)
		{
			SnapshotApplied = true;
			State = System.Text.Encoding.UTF8.GetString(snapshot.Data);
		}
	}

	internal sealed record TestSnapshot : ISnapshot
	{
		public string SnapshotId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = string.Empty;
		public string AggregateType { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
		public byte[] Data { get; init; } = [];
		public IDictionary<string, object>? Metadata { get; init; }
	}

	#endregion Test Aggregates

	#region Version Tracking Tests

	[Fact]
	public void RaiseEvent_ShouldNotIncrementVersion()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");

		// Act
		aggregate.DoSomething("value");

		// Assert
		aggregate.Version.ShouldBe(0, "Version should not increment on RaiseEvent");
	}

	[Fact]
	public void MarkEventsAsCommitted_ShouldIncrementVersionByEventCount()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");
		aggregate.DoSomething("first");
		aggregate.DoSomethingElse(42);

		// Act
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.Version.ShouldBe(2);
	}

	[Fact]
	public void LoadFromHistory_ShouldIncrementVersionPerEvent()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");
		var events = new List<IDomainEvent>
		{
			new SomethingHappened("test-1", 0, "first"),
			new SomethingElseHappened("test-1", 1, 100),
			new SomethingHappened("test-1", 2, "third")
		};

		// Act
		aggregate.LoadFromHistory(events);

		// Assert
		aggregate.Version.ShouldBe(3);
	}

	[Fact]
	public void Version_ShouldStartAtZero()
	{
		// Arrange & Act
		var aggregate = new TestAggregate("test-1");

		// Assert
		aggregate.Version.ShouldBe(0);
	}

	#endregion Version Tracking Tests

	#region Uncommitted Events Tests

	[Fact]
	public void RaiseEvent_ShouldAddToUncommittedEvents()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");

		// Act
		aggregate.DoSomething("value");

		// Assert
		_ = aggregate.GetUncommittedEvents().ShouldHaveSingleItem();
		aggregate.HasUncommittedEvents.ShouldBeTrue();
	}

	[Fact]
	public void LoadFromHistory_ShouldNotAddToUncommittedEvents()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");
		var events = new List<IDomainEvent>
		{
			new SomethingHappened("test-1", 0, "value")
		};

		// Act
		aggregate.LoadFromHistory(events);

		// Assert
		aggregate.GetUncommittedEvents().ShouldBeEmpty();
		aggregate.HasUncommittedEvents.ShouldBeFalse();
	}

	[Fact]
	public void MarkEventsAsCommitted_ShouldClearUncommittedEvents()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");
		aggregate.DoSomething("value");
		aggregate.DoSomethingElse(42);
		aggregate.GetUncommittedEvents().Count.ShouldBe(2);

		// Act
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.GetUncommittedEvents().ShouldBeEmpty();
		aggregate.HasUncommittedEvents.ShouldBeFalse();
	}

	[Fact]
	public void GetUncommittedEvents_ShouldReturnEventsInOrder()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");

		// Act
		aggregate.DoSomething("first");
		aggregate.DoSomethingElse(1);
		aggregate.DoSomething("second");

		// Assert
		var events = aggregate.GetUncommittedEvents();
		events.Count.ShouldBe(3);
		events[0].ShouldBeOfType<SomethingHappened>().Value.ShouldBe("first");
		events[1].ShouldBeOfType<SomethingElseHappened>().Number.ShouldBe(1);
		events[2].ShouldBeOfType<SomethingHappened>().Value.ShouldBe("second");
	}

	#endregion Uncommitted Events Tests

	#region Event Application Tests

	[Fact]
	public void RaiseEvent_ShouldApplyEventToState()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");

		// Act
		aggregate.DoSomething("test-value");

		// Assert
		aggregate.CurrentValue.ShouldBe("test-value");
	}

	[Fact]
	public void LoadFromHistory_ShouldApplyAllEvents()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");
		var events = new List<IDomainEvent>
		{
			new SomethingHappened("test-1", 0, "initial"),
			new SomethingElseHappened("test-1", 1, 42),
			new SomethingHappened("test-1", 2, "final")
		};

		// Act
		aggregate.LoadFromHistory(events);

		// Assert
		aggregate.CurrentValue.ShouldBe("final");
		aggregate.CurrentNumber.ShouldBe(42);
		aggregate.ApplyCount.ShouldBe(3);
	}

	[Fact]
	public void ApplyEvent_ShouldApplyWithoutAddingToUncommitted()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");
		var @event = new SomethingHappened("test-1", 0, "applied");

		// Act
		aggregate.ApplyEvent(@event);

		// Assert
		aggregate.CurrentValue.ShouldBe("applied");
		aggregate.GetUncommittedEvents().ShouldBeEmpty();
	}

	#endregion Event Application Tests

	#region ETag Tests

	[Fact]
	public void ETag_ShouldBeSettable()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");

		// Act
		aggregate.ETag = "some-etag-value";

		// Assert
		aggregate.ETag.ShouldBe("some-etag-value");
	}

	[Fact]
	public void ETag_ShouldBeNullByDefault()
	{
		// Arrange & Act
		var aggregate = new TestAggregate("test-1");

		// Assert
		aggregate.ETag.ShouldBeNull();
	}

	[Fact]
	public void ETag_ShouldBeUpdatable()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");
		aggregate.ETag = "initial";

		// Act
		aggregate.ETag = "updated";

		// Assert
		aggregate.ETag.ShouldBe("updated");
	}

	#endregion ETag Tests

	#region Snapshot Tests

	[Fact]
	public void CreateSnapshot_WithoutOverride_ShouldThrowNotSupported()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");

		// Act & Assert
		_ = Should.Throw<NotSupportedException>(() => aggregate.CreateSnapshot());
	}

	[Fact]
	public void CreateSnapshot_WithOverride_ShouldReturnSnapshot()
	{
		// Arrange
		var aggregate = new SnapshottableAggregate("test-1");
		aggregate.SetState("snapshot-state");
		aggregate.MarkEventsAsCommitted();

		// Act
		var snapshot = aggregate.CreateSnapshot();

		// Assert
		_ = snapshot.ShouldNotBeNull();
		snapshot.AggregateId.ShouldBe("test-1");
		snapshot.Version.ShouldBe(1);
		System.Text.Encoding.UTF8.GetString(snapshot.Data).ShouldBe("snapshot-state");
	}

	[Fact]
	public void LoadFromSnapshot_ShouldRestoreState()
	{
		// Arrange
		var aggregate = new SnapshottableAggregate("test-1");
		var snapshot = new TestSnapshot
		{
			AggregateId = "test-1",
			AggregateType = "SnapshottableAggregate",
			Version = 10,
			Data = System.Text.Encoding.UTF8.GetBytes("restored-state")
		};

		// Act
		aggregate.LoadFromSnapshot(snapshot);

		// Assert
		aggregate.State.ShouldBe("restored-state");
		aggregate.Version.ShouldBe(10);
		aggregate.SnapshotApplied.ShouldBeTrue();
	}

	[Fact]
	public void LoadFromSnapshot_ShouldSetVersion()
	{
		// Arrange
		var aggregate = new SnapshottableAggregate("test-1");
		var snapshot = new TestSnapshot
		{
			AggregateId = "test-1",
			Version = 50
		};

		// Act
		aggregate.LoadFromSnapshot(snapshot);

		// Assert
		aggregate.Version.ShouldBe(50);
	}

	#endregion Snapshot Tests

	#region Edge Cases

	[Fact]
	public void LoadFromHistory_WithEmptyHistory_ShouldNotThrow()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");

		// Act & Assert
		Should.NotThrow(() => aggregate.LoadFromHistory([]));
		aggregate.Version.ShouldBe(0);
	}

	[Fact]
	public void LoadFromHistory_WithNullHistory_ShouldThrow()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => aggregate.LoadFromHistory(null!));
	}

	[Fact]
	public void ApplyEvent_WithNullEvent_ShouldThrow()
	{
		// Arrange
		var aggregate = new TestAggregate("test-1");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => aggregate.ApplyEvent(null!));
	}

	[Fact]
	public void LoadFromSnapshot_WithNullSnapshot_ShouldThrow()
	{
		// Arrange
		var aggregate = new SnapshottableAggregate("test-1");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => aggregate.LoadFromSnapshot(null!));
	}

	#endregion Edge Cases

	#region AggregateType Tests

	[Fact]
	public void AggregateType_ShouldReturnTypeName()
	{
		// Arrange & Act
		var aggregate = new TestAggregate("test-1");

		// Assert
		aggregate.AggregateType.ShouldBe("TestAggregate");
	}

	#endregion AggregateType Tests

	#region Id Tests

	[Fact]
	public void Id_ShouldBeSetViaConstructor()
	{
		// Arrange & Act
		var aggregate = new TestAggregate("my-id");

		// Assert
		aggregate.Id.ShouldBe("my-id");
	}

	[Fact]
	public void Id_AsIAggregate_ShouldReturnStringRepresentation()
	{
		// Arrange
		var aggregate = new TestAggregate("test-id");

		// Act
		var id = ((IAggregateRoot)aggregate).Id;

		// Assert
		id.ShouldBe("test-id");
	}

	#endregion Id Tests
}
