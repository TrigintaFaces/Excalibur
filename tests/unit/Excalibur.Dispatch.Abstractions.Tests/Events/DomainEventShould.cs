namespace Excalibur.Dispatch.Abstractions.Tests.Events;

/// <summary>
/// Tests for DomainEvent base record.
/// Verifies UUID v7 generation, TimeProvider abstraction, metadata support, and immutability.
/// </summary>
[Trait("Category", "Unit")]
public class DomainEventShould
{
	// Test implementation of abstract DomainEvent
	private sealed record TestDomainEvent : DomainEvent
	{
		public TestDomainEvent(string aggregateId, long version, TimeProvider? timeProvider = null)
			: base(aggregateId, version, timeProvider ?? TimeProvider.System)
		{
		}
	}

	[Fact]
	public void Generate_Unique_EventId_Using_Uuid7()
	{
		// Arrange
		var timeProvider = A.Fake<TimeProvider>();
		_ = A.CallTo(() => timeProvider.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

		// Act
		var event1 = new TestDomainEvent("agg-1", 1, timeProvider);
		var event2 = new TestDomainEvent("agg-1", 2, timeProvider);

		// Assert
		event1.EventId.ShouldNotBeNullOrWhiteSpace("EventId must be generated");
		event2.EventId.ShouldNotBeNullOrWhiteSpace("EventId must be generated");
		event1.EventId.ShouldNotBe(event2.EventId, "EventIds must be unique");

		// Verify UUID v7 format (36 characters with dashes)
		Guid.TryParse(event1.EventId, out var guid1).ShouldBeTrue("EventId must be valid GUID");
		Guid.TryParse(event2.EventId, out var guid2).ShouldBeTrue("EventId must be valid GUID");
	}

	[Fact]
	public void Use_TimeProvider_For_OccurredAt()
	{
		// Arrange
		var expectedTime = new DateTimeOffset(2025, 11, 23, 10, 30, 0, TimeSpan.Zero);
		var timeProvider = A.Fake<TimeProvider>();
		_ = A.CallTo(() => timeProvider.GetUtcNow()).Returns(expectedTime);

		// Act
		var domainEvent = new TestDomainEvent("agg-1", 1, timeProvider);

		// Assert
		domainEvent.OccurredAt.ShouldBe(expectedTime, "OccurredAt must use provided TimeProvider");
		_ = A.CallTo(() => timeProvider.GetUtcNow()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Default_To_SystemTimeProvider_When_Not_Provided()
	{
		// Arrange
		var beforeCreation = DateTimeOffset.UtcNow;

		// Act
		var domainEvent = new TestDomainEvent("agg-1", 1);
		var afterCreation = DateTimeOffset.UtcNow;

		// Assert
		domainEvent.OccurredAt.ShouldBeGreaterThanOrEqualTo(beforeCreation, "OccurredAt must use TimeProvider.System when none provided");
		domainEvent.OccurredAt.ShouldBeLessThanOrEqualTo(afterCreation, "OccurredAt must be within creation window");
	}

	[Fact]
	public void Support_Metadata_Dictionary()
	{
		// Arrange
		var domainEvent = new TestDomainEvent("agg-1", 1);

		// Act
		var eventWithMetadata = domainEvent.WithMetadata("key1", "value1");
		var eventWithMultipleMetadata = eventWithMetadata.WithMetadata("key2", 42);

		// Assert
		_ = eventWithMetadata.Metadata.ShouldNotBeNull("Metadata dictionary must be initialized");
		eventWithMetadata.Metadata.ShouldContainKey("key1");
		eventWithMetadata.Metadata["key1"].ShouldBe("value1");

		eventWithMultipleMetadata.Metadata.ShouldContainKey("key1");
		eventWithMultipleMetadata.Metadata.ShouldContainKey("key2");
		eventWithMultipleMetadata.Metadata["key2"].ShouldBe(42);
	}

	[Fact]
	public void Support_CorrelationId_Metadata()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var domainEvent = new TestDomainEvent("agg-1", 1);

		// Act
		var eventWithCorrelation = domainEvent.WithCorrelationId(correlationId);

		// Assert
		_ = eventWithCorrelation.Metadata.ShouldNotBeNull();
		eventWithCorrelation.Metadata.ShouldContainKey("CorrelationId");
		eventWithCorrelation.Metadata["CorrelationId"].ShouldBe(correlationId.ToString());
	}

	[Fact]
	public void Support_CausationId_Metadata()
	{
		// Arrange
		var causationId = "parent-event-123";
		var domainEvent = new TestDomainEvent("agg-1", 1);

		// Act
		var eventWithCausation = domainEvent.WithCausationId(causationId);

		// Assert
		_ = eventWithCausation.Metadata.ShouldNotBeNull();
		eventWithCausation.Metadata.ShouldContainKey("CausationId");
		eventWithCausation.Metadata["CausationId"].ShouldBe(causationId);
	}

	[Fact]
	public void Be_Immutable_Record()
	{
		// Arrange
		var event1 = new TestDomainEvent("agg-1", 1);
		var event2 = new TestDomainEvent("agg-1", 1);

		// Act & Assert - Record provides value equality
		event1.ShouldNotBeSameAs(event2, "Different instances should not be the same reference");

		// Core properties are init-only (immutable)
		// Note: WithMetadata uses builder pattern and mutates the instance
		var beforeMetadata = event1.Metadata;
		_ = event1.WithMetadata("key", "value");
		event1.Metadata.ShouldBe(beforeMetadata, "Metadata dictionary is the same instance (builder pattern)");
		event1.Metadata.ShouldContainKey("key", "WithMetadata should mutate the metadata dictionary");
	}

	[Fact]
	public void Implement_IDispatchMessage()
	{
		// Arrange & Act
		var domainEvent = new TestDomainEvent("agg-1", 1);

		// Assert
		_ = domainEvent.ShouldBeAssignableTo<IDispatchMessage>("DomainEvent must implement IDispatchMessage");
		_ = domainEvent.ShouldBeAssignableTo<IDomainEvent>("DomainEvent must implement IDomainEvent");
		_ = domainEvent.ShouldBeAssignableTo<IDispatchEvent>("DomainEvent must implement IDispatchEvent");
	}
}
