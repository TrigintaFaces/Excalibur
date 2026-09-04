// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Unit tests for <see cref="AggregateRoot{TKey}"/> and <see cref="AggregateRoot"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class AggregateRootShould
{
	#region T419.1: Core AggregateRoot Tests

	[Fact]
	public void Initialize_WithDefaultConstructor_HasDefaultValues()
	{
		// Arrange & Act
		var aggregate = new TestStringAggregate();

		// Assert
		aggregate.Id.ShouldBeNull();
		aggregate.Version.ShouldBe(0);
		aggregate.HasUncommittedEvents.ShouldBeFalse();
		aggregate.ETag.ShouldBeNull();
	}

	[Fact]
	public void Initialize_WithId_SetsIdCorrectly()
	{
		// Arrange
		const string id = "test-aggregate-id";

		// Act
		var aggregate = new TestStringAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id);
		aggregate.Version.ShouldBe(0);
	}

	[Fact]
	public void AggregateType_ReturnsTypeName()
	{
		// Arrange
		var aggregate = new TestStringAggregate("test-id");

		// Act
		var aggregateType = aggregate.AggregateType;

		// Assert
		aggregateType.ShouldBe(nameof(TestStringAggregate));
	}

	[Fact]
	public void Version_IncrementsAfterMarkEventsAsCommitted()
	{
		// Arrange
		var aggregate = new TestStringAggregate();
		aggregate.DoSomething("test-id", "Initial value");

		// Act
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.Version.ShouldBe(1);
	}

	[Fact]
	public void Version_IncrementsByEventCount()
	{
		// Arrange
		var aggregate = new TestStringAggregate();
		aggregate.DoSomething("id-1", "Value 1");
		aggregate.DoSomething("id-2", "Value 2");
		aggregate.DoSomething("id-3", "Value 3");

		// Act
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.Version.ShouldBe(3);
	}

	#endregion T419.1: Core AggregateRoot Tests

	#region T419.2: AggregateRoot (string) Alias Tests

	[Fact]
	public void StringAggregate_InheritsFromAggregateRootOfString()
	{
		// Arrange & Act
		var aggregate = new TestStringAggregate();

		// Assert
		_ = aggregate.ShouldBeAssignableTo<AggregateRoot<string>>();
		_ = aggregate.ShouldBeAssignableTo<AggregateRoot>();
	}

	[Fact]
	public void StringAggregate_IdPropertyWorksWithStringKeys()
	{
		// Arrange
		const string id = "string-aggregate-id-123";

		// Act
		var aggregate = new TestStringAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id);
		((IAggregateRoot)aggregate).Id.ShouldBe(id);
	}

	#endregion T419.2: AggregateRoot (string) Alias Tests

	#region T419.3: Snapshot Integration Tests

	[Fact]
	public void LoadFromSnapshot_AppliesVersion()
	{
		// Arrange
		var aggregate = new TestSnapshotAggregate();
		var snapshot = new TestSnapshot
		{
			AggregateId = "snap-id",
			Version = 42,
			AggregateType = nameof(TestSnapshotAggregate),
			Data = System.Text.Encoding.UTF8.GetBytes("snapshot data"),
		};

		// Act
		aggregate.LoadFromSnapshot(snapshot);

		// Assert
		aggregate.Version.ShouldBe(42);
		aggregate.SnapshotData.ShouldBe("snapshot data");
	}

	[Fact]
	public void LoadFromSnapshot_ThrowsArgumentNullException_WhenSnapshotIsNull()
	{
		// Arrange
		var aggregate = new TestSnapshotAggregate();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => aggregate.LoadFromSnapshot(null!));
	}

	[Fact]
	public void CreateSnapshot_ThrowsNotSupportedException_ByDefault()
	{
		// Arrange
		var aggregate = new TestStringAggregate("test-id");

		// Act & Assert
		var exception = Should.Throw<NotSupportedException>(() => aggregate.CreateSnapshot());
		exception.Message.ShouldContain(nameof(TestStringAggregate));
	}

	#endregion T419.3: Snapshot Integration Tests

	#region T419.4: Event Versioning Tests

	[Fact]
	public void LoadFromHistory_ReplaysEventsInOrder()
	{
		// Arrange
		var aggregate = new TestStringAggregate();
		var history = new List<HistoricEvent>
		{
			new(new TestEvent("id-1", "First"), 0),
			new(new TestEvent("id-1", "Second"), 1),
			new(new TestEvent("id-1", "Third"), 2),
		};

		// Act
		aggregate.LoadFromHistory(history);

		// Assert
		aggregate.Id.ShouldBe("id-1");
		aggregate.LastValue.ShouldBe("Third");
		aggregate.Version.ShouldBe(3);
		aggregate.HasUncommittedEvents.ShouldBeFalse();
	}

	[Fact]
	public void LoadFromHistory_ThrowsArgumentNullException_WhenHistoryIsNull()
	{
		// Arrange
		var aggregate = new TestStringAggregate();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => aggregate.LoadFromHistory(null!));
	}

	[Fact]
	public void LoadFromHistory_TakesOnlyTheHistoricEventEnvelope()
	{
		// K3 (fvq5qf) structural guard. The authoritative stream version enters replay ONLY via the
		// persistence envelope (HistoricEvent.Version) — never off the event payload. Guard the seam:
		// LoadFromHistory has exactly one overload whose element type is HistoricEvent. RED if a
		// payload-version overload (e.g. IEnumerable<IDomainEvent>) is reintroduced to the replay path.
		var overloads = typeof(AggregateRoot<string>).GetMethods()
			.Where(m => m.Name == "LoadFromHistory")
			.ToArray();
		overloads.Length.ShouldBe(1, "LoadFromHistory must have exactly one overload — the HistoricEvent envelope");
		var parameter = overloads[0].GetParameters().ShouldHaveSingleItem();
		parameter.ParameterType.ShouldBe(typeof(IEnumerable<HistoricEvent>),
			"replay input must be IEnumerable<HistoricEvent>; the version lives on the envelope, not the payload");
	}

	[Fact]
	public void ApplyEvent_ThrowsArgumentNullException_WhenEventIsNull()
	{
		// Arrange
		var aggregate = new TestStringAggregate();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => aggregate.ApplyEvent(null!));
	}

	[Fact]
	public void RaiseEvent_AppliesEventAndAddsToUncommitted()
	{
		// Arrange
		var aggregate = new TestStringAggregate();

		// Act
		aggregate.DoSomething("test-id", "test-value");

		// Assert
		aggregate.Id.ShouldBe("test-id");
		aggregate.LastValue.ShouldBe("test-value");
		aggregate.HasUncommittedEvents.ShouldBeTrue();
		aggregate.GetUncommittedEvents().Count.ShouldBe(1);
	}

	[Fact]
	public void MarkEventsAsCommitted_ClearsUncommittedEvents()
	{
		// Arrange
		var aggregate = new TestStringAggregate();
		aggregate.DoSomething("test-id", "test-value");

		// Act
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.HasUncommittedEvents.ShouldBeFalse();
		aggregate.GetUncommittedEvents().Count.ShouldBe(0);
	}

	[Fact]
	public void GetUncommittedEvents_ReturnImmutableSnapshot_CannotBeCastToMutableList()
	{
		// Arrange
		var aggregate = new TestStringAggregate();
		aggregate.DoSomething("test-id", "test-value");

		// Act
		var events = aggregate.GetUncommittedEvents();

		// Assert -- cannot cast back to List<T> and mutate aggregate state
		events.ShouldNotBeNull();
		events.Count.ShouldBe(1);
		(events is IList<IDomainEvent> mutableList && mutableList.IsReadOnly == false)
			.ShouldBeFalse("GetUncommittedEvents() must return an immutable collection that cannot be cast to a mutable list");
	}

	#endregion T419.4: Event Versioning Tests

	#region Generic AggregateRoot<TKey> Tests

	[Fact]
	public void GenericAggregate_WorksWithGuidKey()
	{
		// Arrange
		var id = Guid.NewGuid();

		// Act
		var aggregate = new TestGuidAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id);
		((IAggregateRoot)aggregate).Id.ShouldBe(id.ToString());
	}

	[Fact]
	public void GenericAggregate_WorksWithIntKey()
	{
		// Arrange
		const int id = 12345;

		// Act
		var aggregate = new TestIntAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id);
		((IAggregateRoot)aggregate).Id.ShouldBe("12345");
	}

	[Fact]
	public void ETag_CanBeSetAndRetrieved()
	{
		// Arrange
		var aggregate = new TestStringAggregate("test-id");

		// Act
		aggregate.ETag = "etag-value-123";

		// Assert
		aggregate.ETag.ShouldBe("etag-value-123");
	}

	#endregion Generic AggregateRoot<TKey> Tests

	#region Test Aggregates

	/// <summary>
	/// Test aggregate with string key (uses AggregateRoot alias).
	/// </summary>
	private sealed class TestStringAggregate : AggregateRoot
	{
		public string? LastValue { get; private set; }

		public TestStringAggregate()
		{
		}

		public TestStringAggregate(string id) : base(id)
		{
		}

		public void DoSomething(string id, string value)
		{
			RaiseEvent(new TestEvent(id, value));
		}

		protected override bool ApplyEventInternal(IDomainEvent @event) => @event switch
		{
			TestEvent e => Apply(e),
			_ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}"),
		};

		private bool Apply(TestEvent e)
		{
			Id = e.AggregateId;
			LastValue = e.Value;
			return true;
		}
	}

	/// <summary>
	/// Test aggregate with Guid key.
	/// </summary>
	private sealed class TestGuidAggregate : AggregateRoot<Guid>
	{
		public TestGuidAggregate(Guid id) : base(id)
		{
		}

		protected override bool ApplyEventInternal(IDomainEvent @event)
		{
			// No-op for test
					return true;
		}
	}

	/// <summary>
	/// Test aggregate with int key.
	/// </summary>
	private sealed class TestIntAggregate : AggregateRoot<int>
	{
		public TestIntAggregate(int id) : base(id)
		{
		}

		protected override bool ApplyEventInternal(IDomainEvent @event)
		{
			// No-op for test
					return true;
		}
	}

	/// <summary>
	/// Test aggregate with snapshot support.
	/// </summary>
	private sealed class TestSnapshotAggregate : AggregateRoot
	{
		public string? SnapshotData { get; private set; }

		protected override bool ApplyEventInternal(IDomainEvent @event)
		{
			// No-op for test
					return true;
		}

		protected override void ApplySnapshot(ISnapshot snapshot)
		{
			SnapshotData = System.Text.Encoding.UTF8.GetString(snapshot.Data.Span);
		}
	}

	#endregion Test Aggregates

	#region Test Events

	[MessageName("Test.AggregateRoot.TestEvent")]
	private sealed record TestEvent(string AggregateId, string Value) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; } = 1;
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; } = new Dictionary<string, object>();
	}

	#endregion Test Events

	#region Test Snapshots

	private sealed class TestSnapshot : ISnapshot
	{
		public string SnapshotId { get; init; } = Guid.NewGuid().ToString();

		// Single-tenant fixture. Declared explicitly rather than inherited, so a reader can see
		// that this double is unscoped instead of assuming it.
		public string? TenantId { get; init; }
		public string AggregateId { get; init; } = string.Empty;
		public string AggregateType { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
		public ReadOnlyMemory<byte> Data { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
	}

	#endregion Test Snapshots
}
