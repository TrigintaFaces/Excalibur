// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Depth coverage tests for <see cref="AggregateRoot{TKey}"/> and <see cref="AggregateRoot"/>.
/// Covers GetService, IAggregateRoot explicit interface, RaiseEvent null guard, etc.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class AggregateRootDepthShould
{
	[Fact]
	public void GetService_ReturnsSelf_WhenServiceTypeIsAssignable()
	{
		// Arrange
		var aggregate = new TestAggregate("agg-1");

		// Act
		var result = aggregate.GetService(typeof(TestAggregate));

		// Assert
		result.ShouldBeSameAs(aggregate);
	}

	[Fact]
	public void GetService_ReturnsSelf_WhenServiceTypeIsBaseType()
	{
		// Arrange
		var aggregate = new TestAggregate("agg-1");

		// Act
		var result = aggregate.GetService(typeof(AggregateRoot<string>));

		// Assert
		result.ShouldBeSameAs(aggregate);
	}

	[Fact]
	public void GetService_ReturnsNull_WhenServiceTypeIsNotAssignable()
	{
		// Arrange
		var aggregate = new TestAggregate("agg-1");

		// Act
		var result = aggregate.GetService(typeof(string));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetService_ThrowsArgumentNullException_WhenServiceTypeIsNull()
	{
		// Arrange
		var aggregate = new TestAggregate("agg-1");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => aggregate.GetService(null!));
	}

	[Fact]
	public void IAggregateRoot_Id_ReturnsEmptyString_WhenIdIsNull()
	{
		// Arrange
		var aggregate = new TestAggregate();

		// Act
		var id = ((IAggregateRoot)aggregate).Id;

		// Assert
		id.ShouldBe(string.Empty);
	}

	[Fact]
	public void IAggregateRoot_Id_ReturnsToString_WhenIdIsSet()
	{
		// Arrange
		var aggregate = new TestGuidAggregate();
		var guid = Guid.NewGuid();
		aggregate.SetId(guid);

		// Act
		var id = ((IAggregateRoot)aggregate).Id;

		// Assert
		id.ShouldBe(guid.ToString());
	}

	[Fact]
	public void RaiseEvent_ThrowsArgumentNullException_WhenEventIsNull()
	{
		// Arrange
		var aggregate = new TestAggregate("test");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => aggregate.DoSomethingWithNull());
	}

	[Fact]
	public void LoadFromHistory_Empty_KeepsVersionZero()
	{
		// Arrange
		var aggregate = new TestAggregate();

		// Act
		aggregate.LoadFromHistory(Array.Empty<IDomainEvent>());

		// Assert
		aggregate.Version.ShouldBe(0);
	}

	[Fact]
	public void LoadFromSnapshot_ThenLoadFromHistory_AddsToVersion()
	{
		// Arrange
		var aggregate = new TestSnapshotAggregate();
		var snapshot = new TestSnapshot
		{
			AggregateId = "snap-1",
			Version = 10,
			AggregateType = nameof(TestSnapshotAggregate),
			Data = System.Text.Encoding.UTF8.GetBytes("state"),
		};

		// Act
		aggregate.LoadFromSnapshot(snapshot);
		aggregate.LoadFromHistory(new IDomainEvent[]
		{
			new TestEvent("snap-1", "v11"),
			new TestEvent("snap-1", "v12"),
		});

		// Assert
		aggregate.Version.ShouldBe(12); // 10 from snapshot + 2 from history
	}

	[Fact]
	public void MarkEventsAsCommitted_MultipleTimes_AccumulatesVersion()
	{
		// Arrange
		var aggregate = new TestAggregate();
		aggregate.DoSomething("id-1", "val-1");
		aggregate.DoSomething("id-1", "val-2");
		aggregate.MarkEventsAsCommitted();

		aggregate.DoSomething("id-1", "val-3");

		// Act
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.Version.ShouldBe(3);
		aggregate.HasUncommittedEvents.ShouldBeFalse();
	}

	[Fact]
	public void CreateSnapshot_ThrowsNotSupportedException_ForDefaultImpl()
	{
		// Arrange
		var aggregate = new TestAggregate("test-id");

		// Act & Assert
		var ex = Should.Throw<NotSupportedException>(() => aggregate.CreateSnapshot());
		ex.Message.ShouldContain(nameof(TestAggregate));
	}

	[Fact]
	public void ApplyEvent_AppliesWithoutAddingToUncommitted()
	{
		// Arrange
		var aggregate = new TestAggregate();

		// Act
		aggregate.ApplyEvent(new TestEvent("id-1", "applied"));

		// Assert
		aggregate.LastValue.ShouldBe("applied");
		aggregate.HasUncommittedEvents.ShouldBeFalse();
	}

	[Fact]
	public void DefaultConstructor_SetsDefaultKeyValue()
	{
		// Arrange & Act
		var aggregate = new TestAggregate();

		// Assert
		aggregate.Id.ShouldBeNull();
		aggregate.Version.ShouldBe(0);
		aggregate.AggregateType.ShouldBe(nameof(TestAggregate));
	}

	private sealed class TestAggregate : AggregateRoot
	{
		public string? LastValue { get; private set; }

		public TestAggregate() { }
		public TestAggregate(string id) : base(id) { }

		public void DoSomething(string id, string value) => RaiseEvent(new TestEvent(id, value));
		public void DoSomethingWithNull() => RaiseEvent(null!);

		protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
		{
			TestEvent e => Apply(e),
			_ => throw new InvalidOperationException($"Unknown: {@event.GetType().Name}"),
		};

		private bool Apply(TestEvent e)
		{
			Id = e.AggregateId;
			LastValue = e.Value;
			return true;
		}
	}

	private sealed class TestGuidAggregate : AggregateRoot<Guid>
	{
		public void SetId(Guid id) => Id = id;
		protected override void ApplyEventInternal(IDomainEvent @event) { }
	}

	private sealed class TestSnapshotAggregate : AggregateRoot
	{
		protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
		{
			TestEvent _ => true,
			_ => throw new InvalidOperationException(),
		};

		protected override void ApplySnapshot(ISnapshot snapshot)
		{
			// Just apply version from base
		}
	}

	private sealed record TestEvent(string AggregateId, string Value) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		string IDomainEvent.AggregateId => AggregateId;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(TestEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	private sealed class TestSnapshot : ISnapshot
	{
		public string SnapshotId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = string.Empty;
		public string AggregateType { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
		public byte[] Data { get; init; } = [];
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
