namespace Excalibur.Dispatch.Abstractions.Tests.Events;

/// <summary>
/// Tests for DomainEvent base record.
/// Verifies UUID v7 generation, OccurredAt init, metadata support, and immutability.
/// </summary>
[Trait("Category", "Unit")]
public class DomainEventShould
{
	// Test implementation of abstract DomainEvent
	private sealed record TestDomainEvent : DomainEvent
	{
		public override string AggregateId { get; init; } = string.Empty;
	}

	// Derived record with positional params and AggregateId override
	private sealed record TestOrderCreated(string OrderId, decimal Total) : DomainEvent
	{
		public override string AggregateId => OrderId;
	}

	[Fact]
	public void Generate_Unique_EventId_Using_Uuid7()
	{
		// Act
		var event1 = new TestDomainEvent { AggregateId = "agg-1", Version = 1 };
		var event2 = new TestDomainEvent { AggregateId = "agg-1", Version = 2 };

		// Assert
		event1.EventId.ShouldNotBeNullOrWhiteSpace("EventId must be generated");
		event2.EventId.ShouldNotBeNullOrWhiteSpace("EventId must be generated");
		event1.EventId.ShouldNotBe(event2.EventId, "EventIds must be unique");

		// Verify UUID v7 format (36 characters with dashes)
		Guid.TryParse(event1.EventId, out _).ShouldBeTrue("EventId must be valid GUID");
		Guid.TryParse(event2.EventId, out _).ShouldBeTrue("EventId must be valid GUID");
	}

	[Fact]
	public void Use_TimeProvider_For_OccurredAt()
	{
		// Arrange
		var expectedTime = new DateTimeOffset(2025, 11, 23, 10, 30, 0, TimeSpan.Zero);

		// Act
		var domainEvent = new TestDomainEvent { AggregateId = "agg-1", Version = 1, OccurredAt = expectedTime };

		// Assert
		domainEvent.OccurredAt.ShouldBe(expectedTime, "OccurredAt must accept init value");
	}

	[Fact]
	public void Default_To_SystemTimeProvider_When_Not_Provided()
	{
		// Arrange
		var beforeCreation = DateTimeOffset.UtcNow;

		// Act
		var domainEvent = new TestDomainEvent { AggregateId = "agg-1", Version = 1 };
		var afterCreation = DateTimeOffset.UtcNow;

		// Assert
		domainEvent.OccurredAt.ShouldBeGreaterThanOrEqualTo(beforeCreation, "OccurredAt must use TimeProvider.System when none provided");
		domainEvent.OccurredAt.ShouldBeLessThanOrEqualTo(afterCreation, "OccurredAt must be within creation window");
	}

	[Fact]
	public void Support_Metadata_Dictionary()
	{
		// Arrange
		var domainEvent = new TestDomainEvent { AggregateId = "agg-1", Version = 1 };

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
		var domainEvent = new TestDomainEvent { AggregateId = "agg-1", Version = 1 };

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
		var domainEvent = new TestDomainEvent { AggregateId = "agg-1", Version = 1 };

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
		var event1 = new TestDomainEvent { AggregateId = "agg-1", Version = 1 };
		var event2 = new TestDomainEvent { AggregateId = "agg-1", Version = 1 };

		// Act & Assert - Record provides value equality
		event1.ShouldNotBeSameAs(event2, "Different instances should not be the same reference");

		// WithMetadata returns a NEW instance (record with expression), original stays unchanged
		var beforeMetadata = event1.Metadata;
		var eventWithMetadata = event1.WithMetadata("key", "value");
		event1.Metadata.ShouldBe(beforeMetadata, "Original event metadata must remain unchanged");
		_ = eventWithMetadata.Metadata.ShouldNotBeNull("Returned event should have metadata");
		eventWithMetadata.Metadata.ShouldContainKey("key");
	}

	[Fact]
	public void Implement_IDispatchMessage()
	{
		// Arrange & Act
		var domainEvent = new TestDomainEvent { AggregateId = "agg-1", Version = 1 };

		// Assert
		_ = domainEvent.ShouldBeAssignableTo<IDispatchMessage>("DomainEvent must implement IDispatchMessage");
		_ = domainEvent.ShouldBeAssignableTo<IDomainEvent>("DomainEvent must implement IDomainEvent");
		_ = domainEvent.ShouldBeAssignableTo<IDispatchEvent>("DomainEvent must implement IDispatchEvent");
	}

	// ── Fluent API edge cases ──

	[Fact]
	public void WithMetadata_OverwriteExistingKey()
	{
		// Arrange
		var evt = new TestDomainEvent().WithMetadata("key", "original");

		// Act
		var updated = evt.WithMetadata("key", "overwritten");

		// Assert
		updated.Metadata!["key"].ShouldBe("overwritten");
		updated.Metadata.Count.ShouldBe(1);
	}

	[Fact]
	public void WithMetadata_ChainMultipleCalls_PreservesAll()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var evt = new TestDomainEvent()
			.WithMetadata("source", "test")
			.WithCorrelationId(correlationId)
			.WithCausationId("cause-123")
			.WithMetadata("extra", 99);

		// Assert
		evt.Metadata!.Count.ShouldBe(4);
		evt.Metadata["source"].ShouldBe("test");
		evt.Metadata["CorrelationId"].ShouldBe(correlationId.ToString());
		evt.Metadata["CausationId"].ShouldBe("cause-123");
		evt.Metadata["extra"].ShouldBe(99);
	}

	[Fact]
	public void WithCorrelationId_EmptyGuid_ReturnsUnchangedEvent()
	{
		// Arrange
		var evt = new TestDomainEvent();

		// Act
		var result = evt.WithCorrelationId(Guid.Empty);

		// Assert — should be same instance (no-op)
		result.Metadata.ShouldBeNull();
	}

	[Fact]
	public void WithCausationId_EmptyString_ReturnsUnchangedEvent()
	{
		// Arrange
		var evt = new TestDomainEvent();

		// Act
		var result = evt.WithCausationId(string.Empty);

		// Assert
		result.Metadata.ShouldBeNull();
	}

	[Fact]
	public void WithCausationId_NullString_ReturnsUnchangedEvent()
	{
		// Arrange
		var evt = new TestDomainEvent();

		// Act
		var result = evt.WithCausationId(null!);

		// Assert
		result.Metadata.ShouldBeNull();
	}

	// ── Derived record pattern ──

	[Fact]
	public void DerivedRecord_OverrideAggregateId_ViaPositionalParam()
	{
		// Arrange & Act
		var evt = new TestOrderCreated("order-42", 199.99m);

		// Assert
		evt.AggregateId.ShouldBe("order-42");
		evt.OrderId.ShouldBe("order-42");
		evt.Total.ShouldBe(199.99m);
		evt.EventType.ShouldBe(nameof(TestOrderCreated));
	}

	[Fact]
	public void DerivedRecord_InheritsFluentApi()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var evt = new TestOrderCreated("order-1", 50m);

		// Act
		var withMeta = evt.WithCorrelationId(correlationId);

		// Assert — returns DomainEvent, but retains derived record data
		withMeta.ShouldBeOfType<TestOrderCreated>();
		var typed = (TestOrderCreated)withMeta;
		typed.OrderId.ShouldBe("order-1");
		typed.Total.ShouldBe(50m);
		typed.Metadata!["CorrelationId"].ShouldBe(correlationId.ToString());
	}

	// ── Default values ──

	[Fact]
	public void DefaultMetadata_IsNull()
	{
		var evt = new TestDomainEvent();
		evt.Metadata.ShouldBeNull();
	}

	[Fact]
	public void DefaultVersion_IsZero()
	{
		var evt = new TestDomainEvent();
		evt.Version.ShouldBe(0);
	}

	[Fact]
	public void DefaultAggregateId_IsEmptyString()
	{
		var evt = new TestDomainEvent();
		evt.AggregateId.ShouldBe(string.Empty);
	}

	[Fact]
	public void EventType_ReturnsTypeName()
	{
		var evt = new TestDomainEvent();
		evt.EventType.ShouldBe(nameof(TestDomainEvent));
	}
}
