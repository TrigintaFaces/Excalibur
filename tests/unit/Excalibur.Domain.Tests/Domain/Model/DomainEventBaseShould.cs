using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class DomainEventBaseShould
{
	[Fact]
	public void GenerateUniqueEventId()
	{
		// Arrange & Act
		var event1 = new TestDomainEvent();
		var event2 = new TestDomainEvent();

		// Assert
		event1.EventId.ShouldNotBeNullOrEmpty();
		event2.EventId.ShouldNotBeNullOrEmpty();
		event1.EventId.ShouldNotBe(event2.EventId);
	}

	[Fact]
	public void HaveDefaultAggregateIdAsEmpty()
	{
		// Arrange & Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.AggregateId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultVersionOfZero()
	{
		// Arrange & Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.Version.ShouldBe(0);
	}

	[Fact]
	public void SetOccurredAtToUtcNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		var after = DateTimeOffset.UtcNow;
		domainEvent.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
		domainEvent.OccurredAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void ReturnTypeNameAsEventType()
	{
		// Arrange & Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.EventType.ShouldBe(nameof(TestDomainEvent));
	}

	[Fact]
	public void HaveNullMetadataByDefault()
	{
		// Arrange & Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMetadata()
	{
		// Arrange & Act
		var metadata = new Dictionary<string, object> { ["key"] = "value" };
		var domainEvent = new TestDomainEvent { Metadata = metadata };

		// Assert
		domainEvent.Metadata.ShouldNotBeNull();
		domainEvent.Metadata["key"].ShouldBe("value");
	}

	[Fact]
	public void AllowOverridingAggregateId()
	{
		// Arrange & Act
		var domainEvent = new OrderCreatedEvent { AggregateId = "order-123" };

		// Assert
		domainEvent.AggregateId.ShouldBe("order-123");
	}

	[Fact]
	public void AllowOverridingVersion()
	{
		// Arrange & Act
		var domainEvent = new TestDomainEvent { Version = 42 };

		// Assert
		domainEvent.Version.ShouldBe(42);
	}

	[Fact]
	public void AllowOverridingEventId()
	{
		// Arrange & Act
		var domainEvent = new TestDomainEvent { EventId = "custom-id" };

		// Assert
		domainEvent.EventId.ShouldBe("custom-id");
	}

	[Fact]
	public void AllowOverridingOccurredAt()
	{
		// Arrange
		var customTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var domainEvent = new TestDomainEvent { OccurredAt = customTime };

		// Assert
		domainEvent.OccurredAt.ShouldBe(customTime);
	}

	[Fact]
	public void SupportRecordWithClause()
	{
		// Arrange
		var original = new TestDomainEvent { Version = 1 };

		// Act
		var modified = original with { Version = 2 };

		// Assert
		modified.Version.ShouldBe(2);
		original.Version.ShouldBe(1);
		modified.EventId.ShouldBe(original.EventId);
	}

	[Fact]
	public void ImplementIDomainEvent()
	{
		// Arrange & Act
		IDomainEvent domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.ShouldNotBeNull();
		domainEvent.EventId.ShouldNotBeNullOrEmpty();
	}

	private record TestDomainEvent : DomainEventBase;

	private record OrderCreatedEvent : DomainEventBase
	{
		public override string AggregateId { get; init; } = string.Empty;
	}
}
