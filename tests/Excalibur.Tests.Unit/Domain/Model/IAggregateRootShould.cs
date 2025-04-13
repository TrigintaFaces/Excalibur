using Excalibur.Core.Domain.Events;
using Excalibur.Domain.Model;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain.Model;

public class IAggregateRootShould
{
	[Fact]
	public void InheritFromIEntity()
	{
		// Arrange
		var aggregateRoot = new TestAggregateRoot("test-key");

		// Act & Assert
		_ = aggregateRoot.ShouldBeAssignableTo<IEntity>();
	}

	[Fact]
	public void InheritFromGenericIEntity()
	{
		// Arrange
		var aggregateRoot = new TestAggregateRoot("test-key");

		// Act & Assert
		_ = aggregateRoot.ShouldBeAssignableTo<IEntity<string>>();
	}

	[Fact]
	public void ProvideAccessToETag()
	{
		// Arrange
		IAggregateRoot aggregateRoot = new TestAggregateRoot("test-key") { ETag = "test-etag" };

		// Act & Assert
		aggregateRoot.ETag.ShouldBe("test-etag");
	}

	[Fact]
	public void AllowSettingETag()
	{
		// Arrange
		IAggregateRoot aggregateRoot = new TestAggregateRoot("test-key");
		var newETag = "new-etag-value";

		// Act
		aggregateRoot.ETag = newETag;

		// Assert
		aggregateRoot.ETag.ShouldBe(newETag);
	}

	[Fact]
	public void ProvideAccessToDomainEvents()
	{
		// Arrange
		var aggregateRoot = new TestAggregateRoot("test-key");
		var events = new List<IDomainEvent> { new TestDomainEvent("Event 1"), new TestDomainEvent("Event 2") };

		// Act
		foreach (var evt in events)
		{
			aggregateRoot.AddDomainEvent(evt);
		}

		// Assert
		var domainEvents = ((IAggregateRoot)aggregateRoot).DomainEvents.ToList();
		domainEvents.Count.ShouldBe(2);
		domainEvents[0].ShouldBeOfType<TestDomainEvent>().EventData.ShouldBe("Event 1");
		domainEvents[1].ShouldBeOfType<TestDomainEvent>().EventData.ShouldBe("Event 2");
	}

	[Fact]
	public void SupportDifferentKeyTypes()
	{
		// Arrange
		IAggregateRoot<string> stringKeyRoot = new TestAggregateRoot("string-key");
		IAggregateRoot<int> intKeyRoot = new TestIntKeyAggregateRoot(42);
		IAggregateRoot<Guid> guidKeyRoot = new TestGuidKeyAggregateRoot(Guid.NewGuid());

		// Act & Assert
		_ = stringKeyRoot.Key.ShouldBeOfType<string>();
		_ = intKeyRoot.Key.ShouldBeOfType<int>();
		_ = guidKeyRoot.Key.ShouldBeOfType<Guid>();
	}

	// Test AggregateRoot implementations
	private sealed class TestAggregateRoot(string key) : IAggregateRoot<string>
	{
		private readonly List<IDomainEvent> _domainEvents = new();

		public string Key { get; } = key;

		public string ETag { get; set; } = "default-etag";

		public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;

		public void AddDomainEvent(IDomainEvent domainEvent)
		{
			_domainEvents.Add(domainEvent);
		}
	}

	private sealed class TestIntKeyAggregateRoot(int key) : IAggregateRoot<int>
	{
		private readonly List<IDomainEvent> _domainEvents = new();

		public int Key { get; } = key;

		public string ETag { get; set; } = "default-etag";

		public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;
	}

	private sealed class TestGuidKeyAggregateRoot(Guid key) : IAggregateRoot<Guid>
	{
		private readonly List<IDomainEvent> _domainEvents = new();

		public Guid Key { get; } = key;

		public string ETag { get; set; } = "default-etag";

		public IEnumerable<IDomainEvent> DomainEvents => _domainEvents;
	}

	// Simple domain event for testing
	private sealed class TestDomainEvent(string eventData) : IDomainEvent
	{
		public string EventData { get; } = eventData;
	}
}
