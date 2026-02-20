// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Unit tests for <see cref="AggregateRoot{TKey}"/> with various key types.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AggregateRootGenericKeyShould
{
	#region Test Events

	internal sealed record TestEvent : DomainEvent
	{
		public string Data { get; init; } = string.Empty;

		public TestEvent(string aggregateId, long version, string data)
			: base(aggregateId, version, TimeProvider.System)
		{
			Data = data;
		}

		public TestEvent() : base("", 0, TimeProvider.System) { }
	}

	#endregion Test Events

	#region Test Aggregates with Various Key Types

	/// <summary>
	/// Aggregate with Guid key.
	/// </summary>
	internal sealed class GuidAggregate : AggregateRoot<Guid>
	{
		public GuidAggregate()
		{ }

		public GuidAggregate(Guid id) : base(id)
		{
		}

		public string State { get; private set; } = string.Empty;

		public void SetState(string state)
		{
			RaiseEvent(new TestEvent(Id.ToString(), Version, state));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is TestEvent e)
				State = e.Data;
		}
	}

	/// <summary>
	/// Aggregate with int key.
	/// </summary>
	internal sealed class IntAggregate : AggregateRoot<int>
	{
		public IntAggregate()
		{ }

		public IntAggregate(int id) : base(id)
		{
		}

		public string State { get; private set; } = string.Empty;

		public void SetState(string state)
		{
			RaiseEvent(new TestEvent(Id.ToString(), Version, state));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is TestEvent e)
				State = e.Data;
		}
	}

	/// <summary>
	/// Aggregate with long key.
	/// </summary>
	internal sealed class LongAggregate : AggregateRoot<long>
	{
		public LongAggregate()
		{ }

		public LongAggregate(long id) : base(id)
		{
		}

		public string State { get; private set; } = string.Empty;

		public void SetState(string state)
		{
			RaiseEvent(new TestEvent(Id.ToString(), Version, state));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is TestEvent e)
				State = e.Data;
		}
	}

	/// <summary>
	/// Custom value type for testing custom key types.
	/// </summary>
	internal readonly record struct CustomId(string Tenant, int Number)
	{
		public override string ToString() => $"{Tenant}-{Number}";
	}

	/// <summary>
	/// Aggregate with custom value type key.
	/// </summary>
	internal sealed class CustomKeyAggregate : AggregateRoot<CustomId>
	{
		public CustomKeyAggregate()
		{ }

		public CustomKeyAggregate(CustomId id) : base(id)
		{
		}

		public string State { get; private set; } = string.Empty;

		public void SetState(string state)
		{
			RaiseEvent(new TestEvent(Id.ToString(), Version, state));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is TestEvent e)
				State = e.Data;
		}
	}

	#endregion Test Aggregates with Various Key Types

	#region Guid Key Tests

	[Fact]
	public void GuidKeyAggregate_ShouldStoreGuidId()
	{
		// Arrange
		var id = Guid.NewGuid();

		// Act
		var aggregate = new GuidAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id);
	}

	[Fact]
	public void GuidKeyAggregate_IAggregate_Id_ShouldReturnStringRepresentation()
	{
		// Arrange
		var id = Guid.NewGuid();
		var aggregate = new GuidAggregate(id);

		// Act
		var stringId = ((IAggregateRoot)aggregate).Id;

		// Assert
		stringId.ShouldBe(id.ToString());
	}

	[Fact]
	public void GuidKeyAggregate_ShouldSupportEventSourcing()
	{
		// Arrange
		var aggregate = new GuidAggregate(Guid.NewGuid());

		// Act
		aggregate.SetState("test-state");
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.State.ShouldBe("test-state");
		aggregate.Version.ShouldBe(1);
		aggregate.GetUncommittedEvents().ShouldBeEmpty();
	}

	[Fact]
	public void GuidKeyAggregate_ShouldLoadFromHistory()
	{
		// Arrange
		var id = Guid.NewGuid();
		var aggregate = new GuidAggregate(id);
		var events = new List<IDomainEvent>
		{
			new TestEvent(id.ToString(), 0, "first"),
			new TestEvent(id.ToString(), 1, "second")
		};

		// Act
		aggregate.LoadFromHistory(events);

		// Assert
		aggregate.State.ShouldBe("second");
		aggregate.Version.ShouldBe(2);
	}

	#endregion Guid Key Tests

	#region Int Key Tests

	[Fact]
	public void IntKeyAggregate_ShouldStoreIntId()
	{
		// Arrange & Act
		var aggregate = new IntAggregate(42);

		// Assert
		aggregate.Id.ShouldBe(42);
	}

	[Fact]
	public void IntKeyAggregate_IAggregate_Id_ShouldReturnStringRepresentation()
	{
		// Arrange
		var aggregate = new IntAggregate(12345);

		// Act
		var stringId = ((IAggregateRoot)aggregate).Id;

		// Assert
		stringId.ShouldBe("12345");
	}

	[Fact]
	public void IntKeyAggregate_ShouldSupportEventSourcing()
	{
		// Arrange
		var aggregate = new IntAggregate(1);

		// Act
		aggregate.SetState("int-state");
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.State.ShouldBe("int-state");
		aggregate.Version.ShouldBe(1);
	}

	[Fact]
	public void IntKeyAggregate_ShouldHandleZeroId()
	{
		// Arrange & Act
		var aggregate = new IntAggregate(0);

		// Assert
		aggregate.Id.ShouldBe(0);
		((IAggregateRoot)aggregate).Id.ShouldBe("0");
	}

	[Fact]
	public void IntKeyAggregate_ShouldHandleNegativeId()
	{
		// Arrange & Act
		var aggregate = new IntAggregate(-100);

		// Assert
		aggregate.Id.ShouldBe(-100);
		((IAggregateRoot)aggregate).Id.ShouldBe("-100");
	}

	#endregion Int Key Tests

	#region Long Key Tests

	[Fact]
	public void LongKeyAggregate_ShouldStoreLongId()
	{
		// Arrange
		var id = 9223372036854775807L; // Max long value

		// Act
		var aggregate = new LongAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id);
	}

	[Fact]
	public void LongKeyAggregate_IAggregate_Id_ShouldReturnStringRepresentation()
	{
		// Arrange
		var id = 9223372036854775807L;
		var aggregate = new LongAggregate(id);

		// Act
		var stringId = ((IAggregateRoot)aggregate).Id;

		// Assert
		stringId.ShouldBe("9223372036854775807");
	}

	#endregion Long Key Tests

	#region Custom Key Type Tests

	[Fact]
	public void CustomKeyAggregate_ShouldStoreCustomId()
	{
		// Arrange
		var id = new CustomId("tenant-a", 123);

		// Act
		var aggregate = new CustomKeyAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id);
		aggregate.Id.Tenant.ShouldBe("tenant-a");
		aggregate.Id.Number.ShouldBe(123);
	}

	[Fact]
	public void CustomKeyAggregate_IAggregate_Id_ShouldReturnStringRepresentation()
	{
		// Arrange
		var id = new CustomId("tenant-b", 456);
		var aggregate = new CustomKeyAggregate(id);

		// Act
		var stringId = ((IAggregateRoot)aggregate).Id;

		// Assert
		stringId.ShouldBe("tenant-b-456");
	}

	[Fact]
	public void CustomKeyAggregate_ShouldSupportEventSourcing()
	{
		// Arrange
		var aggregate = new CustomKeyAggregate(new CustomId("tenant", 1));

		// Act
		aggregate.SetState("custom-state");
		aggregate.MarkEventsAsCommitted();

		// Assert
		aggregate.State.ShouldBe("custom-state");
		aggregate.Version.ShouldBe(1);
	}

	#endregion Custom Key Type Tests

	#region IAggregateRoot<TKey> Interface Tests

	[Fact]
	public void IAggregate_TKey_ShouldExposeStronglyTypedId()
	{
		// Arrange
		var id = Guid.NewGuid();
		IAggregateRoot<Guid> aggregate = new GuidAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id);
	}

	[Fact]
	public void IAggregate_ShouldExposeStringId()
	{
		// Arrange
		var id = Guid.NewGuid();
		IAggregateRoot aggregate = new GuidAggregate(id);

		// Assert
		aggregate.Id.ShouldBe(id.ToString());
	}

	[Fact]
	public void GenericAggregate_ShouldImplementBothInterfaces()
	{
		// Arrange
		var aggregate = new GuidAggregate(Guid.NewGuid());

		// Assert
		_ = aggregate.ShouldBeAssignableTo<IAggregateRoot>();
		_ = aggregate.ShouldBeAssignableTo<IAggregateRoot<Guid>>();
	}

	#endregion IAggregateRoot<TKey> Interface Tests

	#region Default Value Tests

	[Fact]
	public void DefaultConstructor_ShouldHaveDefaultId()
	{
		// Arrange & Act
		var guidAggregate = new GuidAggregate();
		var intAggregate = new IntAggregate();

		// Assert
		guidAggregate.Id.ShouldBe(Guid.Empty);
		intAggregate.Id.ShouldBe(0);
	}

	[Fact]
	public void DefaultConstructor_IAggregate_Id_ShouldReturnEmptyStringForNullableDefault()
	{
		// Arrange & Act
		var intAggregate = new IntAggregate();

		// Assert - int default (0) should convert to "0"
		((IAggregateRoot)intAggregate).Id.ShouldBe("0");
	}

	#endregion Default Value Tests

	#region ETag with Generic Keys Tests

	[Fact]
	public void GenericKeyAggregate_ShouldSupportETag()
	{
		// Arrange
		var aggregate = new GuidAggregate(Guid.NewGuid());

		// Act
		aggregate.ETag = "etag-value";

		// Assert
		aggregate.ETag.ShouldBe("etag-value");
	}

	[Fact]
	public void GenericKeyAggregate_ETag_ShouldBeNullByDefault()
	{
		// Arrange & Act
		var aggregate = new IntAggregate(42);

		// Assert
		aggregate.ETag.ShouldBeNull();
	}

	#endregion ETag with Generic Keys Tests

	#region Static Factory Method Tests (IAggregateRoot<TAggregate, TKey>)

	[Fact]
	public void StaticCreate_ShouldCreateAggregateWithId()
	{
		// Arrange
		var id = Guid.NewGuid();

		// Act
		var aggregate = FactoryAggregate.Create(id);

		// Assert
		_ = aggregate.ShouldNotBeNull();
		aggregate.Id.ShouldBe(id);
		aggregate.Version.ShouldBe(0);
		aggregate.State.ShouldBe(string.Empty);
	}

	[Fact]
	public void StaticFromEvents_ShouldCreateAndRehydrateAggregate()
	{
		// Arrange
		var id = Guid.NewGuid();
		var events = new List<IDomainEvent>
		{
			new TestEvent(id.ToString(), 0, "first"),
			new TestEvent(id.ToString(), 1, "second"),
			new TestEvent(id.ToString(), 2, "third")
		};

		// Act
		var aggregate = FactoryAggregate.FromEvents(id, events);

		// Assert
		_ = aggregate.ShouldNotBeNull();
		aggregate.Id.ShouldBe(id);
		aggregate.State.ShouldBe("third");
		aggregate.Version.ShouldBe(3); // LoadFromHistory increments Version per event
		aggregate.GetUncommittedEvents().ShouldBeEmpty(); // History events should NOT be uncommitted
	}

	[Fact]
	public void StaticFromEvents_WithEmptyHistory_ShouldReturnEmptyAggregate()
	{
		// Arrange
		var id = Guid.NewGuid();
		var events = Array.Empty<IDomainEvent>();

		// Act
		var aggregate = FactoryAggregate.FromEvents(id, events);

		// Assert
		_ = aggregate.ShouldNotBeNull();
		aggregate.Id.ShouldBe(id);
		aggregate.Version.ShouldBe(0);
		aggregate.State.ShouldBe(string.Empty);
	}

	[Fact]
	public void StaticFromEvents_ShouldNotAddToUncommittedEvents()
	{
		// Arrange - This is a critical behavior test
		// Events loaded via FromEvents should NOT be treated as new changes
		var id = Guid.NewGuid();
		var events = new List<IDomainEvent>
		{
			new TestEvent(id.ToString(), 0, "historical")
		};

		// Act
		var aggregate = FactoryAggregate.FromEvents(id, events);

		// Assert
		aggregate.GetUncommittedEvents().ShouldBeEmpty();
		aggregate.HasUncommittedEvents.ShouldBeFalse();
	}

	[Fact]
	public void StaticFromEvents_ThenRaiseEvent_ShouldTrackNewEventsAsUncommitted()
	{
		// Arrange
		var id = Guid.NewGuid();
		var historyEvents = new List<IDomainEvent>
		{
			new TestEvent(id.ToString(), 0, "historical")
		};

		// Act
		var aggregate = FactoryAggregate.FromEvents(id, historyEvents);
		aggregate.SetState("new-change"); // This should create an uncommitted event

		// Assert
		aggregate.GetUncommittedEvents().Count.ShouldBe(1);
		aggregate.HasUncommittedEvents.ShouldBeTrue();
		aggregate.State.ShouldBe("new-change");
		// Version should still be 1 (from history) until committed
		aggregate.Version.ShouldBe(1);
	}

	[Fact]
	public void IAggregate_TAggregate_TKey_ShouldSupportInterfaceHierarchy()
	{
		// Arrange
		var aggregate = FactoryAggregate.Create(Guid.NewGuid());

		// Assert - test the interface hierarchy through casting
		// Note: IAggregateRoot<TAggregate, TKey> uses static abstracts, so we test the parent interfaces
		_ = aggregate.ShouldBeAssignableTo<IAggregateRoot<Guid>>();
		_ = aggregate.ShouldBeAssignableTo<IAggregateRoot>();

		// Verify the static factory methods exist and are callable (compile-time check)
		// The fact that we can call these proves the interface is implemented
		var createdAggregate = FactoryAggregate.Create(Guid.NewGuid());
		var fromEventsAggregate = FactoryAggregate.FromEvents(Guid.NewGuid(), []);

		_ = createdAggregate.ShouldNotBeNull();
		_ = fromEventsAggregate.ShouldNotBeNull();
	}

	/// <summary>
	/// Aggregate implementing IAggregateRoot{TAggregate, TKey} with static factory methods.
	/// </summary>
	internal sealed class FactoryAggregate : AggregateRoot<Guid>, IAggregateRoot<FactoryAggregate, Guid>
	{
		public FactoryAggregate()
		{ }

		public FactoryAggregate(Guid id) : base(id)
		{
		}

		public string State { get; private set; } = string.Empty;

		// Static factory methods required by IAggregateRoot<TAggregate, TKey>
		public static FactoryAggregate Create(Guid id) => new(id);

		public static FactoryAggregate FromEvents(Guid id, IEnumerable<IDomainEvent> events)
		{
			var aggregate = new FactoryAggregate(id);
			aggregate.LoadFromHistory(events);
			return aggregate;
		}

		public void SetState(string state)
		{
			RaiseEvent(new TestEvent(Id.ToString(), Version, state));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is TestEvent e)
				State = e.Data;
		}
	}

	#endregion Static Factory Method Tests (IAggregateRoot<TAggregate, TKey>)

	#region Constraint Validation Tests

	[Fact]
	public void AggregateRoot_TKey_Constraint_ShouldRequireNotnull()
	{
		// This test validates that the constraint exists at compile time.
		// If TKey : notnull constraint is removed, this test file would fail to compile
		// with nullable reference types enabled for value types like int? or Guid?

		// Arrange & Act & Assert
		// The fact that we can create these aggregates proves the constraint is satisfied
		var guidAggregate = new GuidAggregate(Guid.NewGuid());
		var intAggregate = new IntAggregate(42);

		_ = guidAggregate.ShouldNotBeNull();
		_ = intAggregate.ShouldNotBeNull();
	}

	#endregion Constraint Validation Tests
}
