using Excalibur.Core.Domain.Events;
using Excalibur.Domain.Model;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain.Model;

public class AggregateRootBaseShould
{
	[Fact]
	public void InheritFromEntityBase()
	{
		// Arrange
		var aggregateRoot = new TestAggregateRoot("key1");

		// Act & Assert
		_ = aggregateRoot.ShouldBeAssignableTo<EntityBase<string>>();
	}

	[Fact]
	public void ImplementIAggregateRootInterface()
	{
		// Arrange
		var aggregateRoot = new TestAggregateRoot("key1");

		// Act & Assert
		_ = aggregateRoot.ShouldBeAssignableTo<IAggregateRoot>();
		_ = aggregateRoot.ShouldBeAssignableTo<IAggregateRoot<string>>();
	}

	[Fact]
	public void HaveDefaultETagValue()
	{
		// Arrange
		var aggregateRoot = new TestAggregateRoot("key1");

		// Act & Assert
		aggregateRoot.ETag.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void AllowSettingETagValue()
	{
		// Arrange
		var aggregateRoot = new TestAggregateRoot("key1");
		var newETag = "new-etag-value";

		// Act
		aggregateRoot.ETag = newETag;

		// Assert
		aggregateRoot.ETag.ShouldBe(newETag);
	}

	[Fact]
	public void ManageDomainEventsCorrectly()
	{
		// Arrange
		var aggregateRoot = new TestAggregateRoot("key1");

		// Act
		aggregateRoot.RaiseDomainEvent(new TestDomainEvent("Event 1"));
		aggregateRoot.RaiseDomainEvent(new TestDomainEvent("Event 2"));

		// Assert
		var events = ((IAggregateRoot)aggregateRoot).DomainEvents.ToList();
		events.Count.ShouldBe(2);
		events[0].ShouldBeOfType<TestDomainEvent>().EventData.ShouldBe("Event 1");
		events[1].ShouldBeOfType<TestDomainEvent>().EventData.ShouldBe("Event 2");
	}

	[Fact]
	public void ReturnEmptyEventCollectionByDefault()
	{
		// Arrange
		var aggregateRoot = new TestAggregateRoot("key1");

		// Act
		var events = ((IAggregateRoot)aggregateRoot).DomainEvents;

		// Assert
		_ = events.ShouldNotBeNull();
		events.ShouldBeEmpty();
	}

	[Fact]
	public void MaintainKeyAndEqualityFromEntityBase()
	{
		// Arrange
		var aggregateRoot1 = new TestAggregateRoot("key1");
		var aggregateRoot2 = new TestAggregateRoot("key1");
		var aggregateRoot3 = new TestAggregateRoot("key2");

		// Act & Assert Same key, same type
		aggregateRoot1.Equals(aggregateRoot2).ShouldBeTrue();

		// Different key, same type
		aggregateRoot1.Equals(aggregateRoot3).ShouldBeFalse();
	}

	[Fact]
	public void DefaultToStringKeyForNonGenericImplementation()
	{
		// Arrange & Act
		var aggregateRoot = new NonGenericTestAggregateRoot("test-key");

		// Assert
		aggregateRoot.Key.ShouldBe("test-key");
		_ = aggregateRoot.ShouldBeAssignableTo<AggregateRootBase>();
		_ = aggregateRoot.ShouldBeAssignableTo<AggregateRootBase<string>>();
	}

	// Test implementation of AggregateRootBase
	private sealed class TestAggregateRoot(string key) : AggregateRootBase<string>
	{
		public override string Key => EntityKey;
		private string EntityKey { get; } = key;

		public void RaiseDomainEvent(IDomainEvent domainEvent)
		{
			RaiseEvent(domainEvent);
		}
	}

	// Test implementation of non-generic AggregateRootBase
	private sealed class NonGenericTestAggregateRoot(string key) : AggregateRootBase
	{
		public override string Key => EntityKey;
		private string EntityKey { get; } = key;
	}

	// Simple domain event for testing
	private sealed class TestDomainEvent(string eventData) : IDomainEvent
	{
		public string EventData { get; } = eventData;
	}
}
