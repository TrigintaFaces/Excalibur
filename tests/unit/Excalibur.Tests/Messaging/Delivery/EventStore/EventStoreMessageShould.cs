// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Messaging.Delivery.EventStore;

/// <summary>
///     Unit tests for EventStoreMessage to verify event store message functionality.
/// </summary>
[Trait("Category", "Unit")]
public class EventStoreMessageShould
{
	[Fact]
	public void ConstructorShouldInitializeWithStringAggregateKey()
	{
		// Arrange
		var aggregateId = "test-aggregate-123";
		var occurredOn = DateTimeOffset.UtcNow;
		var eventId = Guid.NewGuid().ToString();
		var eventType = "Tests.Shared.Events.TestEvent";
		var eventBody = "{\"property\": \"value\"}";
		var eventMetadata = "{\"version\": \"1.0\"}";

		// Act
		var message = new EventStoreMessage<string>
		{
			AggregateId = aggregateId,
			OccurredOn = occurredOn,
			EventId = eventId,
			EventType = eventType,
			EventBody = eventBody,
			EventMetadata = eventMetadata,
		};

		// Assert
		message.AggregateId.ShouldBe(aggregateId);
		message.OccurredOn.ShouldBe(occurredOn);
		message.EventId.ShouldBe(eventId);
		message.EventType.ShouldBe(eventType);
		message.EventBody.ShouldBe(eventBody);
		message.EventMetadata.ShouldBe(eventMetadata);
		message.Attempts.ShouldBe(0);
		message.DispatcherId.ShouldBeNull();
		message.DispatchedOn.ShouldBeNull();
		message.DispatcherTimeout.ShouldBeNull();
	}

	[Fact]
	public void ConstructorShouldInitializeWithGuidAggregateKey()
	{
		// Arrange
		var aggregateId = Guid.NewGuid();
		var occurredOn = DateTimeOffset.UtcNow;
		var eventId = Guid.NewGuid().ToString();
		var eventType = "UserCreated";
		var eventBody = "{\"userId\": \"123\", \"name\": \"John Doe\"}";
		var eventMetadata = "{\"correlationId\": \"abc-123\"}";

		// Act
		var message = new EventStoreMessage<Guid>
		{
			AggregateId = aggregateId,
			OccurredOn = occurredOn,
			EventId = eventId,
			EventType = eventType,
			EventBody = eventBody,
			EventMetadata = eventMetadata,
		};

		// Assert
		message.AggregateId.ShouldBe(aggregateId);
		message.OccurredOn.ShouldBe(occurredOn);
		message.EventId.ShouldBe(eventId);
		message.EventType.ShouldBe(eventType);
		message.EventBody.ShouldBe(eventBody);
		message.EventMetadata.ShouldBe(eventMetadata);
	}

	[Fact]
	public void ConstructorShouldInitializeWithIntAggregateKey()
	{
		// Arrange
		var aggregateId = 12345;
		var occurredOn = DateTimeOffset.UtcNow;
		var eventId = Guid.NewGuid().ToString();
		var eventType = "OrderProcessed";
		var eventBody = "{\"orderId\": 12345, \"amount\": 99.99}";
		var eventMetadata = "{\"processedBy\": \"worker-1\"}";

		// Act
		var message = new EventStoreMessage<int>
		{
			AggregateId = aggregateId,
			OccurredOn = occurredOn,
			EventId = eventId,
			EventType = eventType,
			EventBody = eventBody,
			EventMetadata = eventMetadata,
		};

		// Assert
		message.AggregateId.ShouldBe(aggregateId);
		message.OccurredOn.ShouldBe(occurredOn);
		message.EventId.ShouldBe(eventId);
		message.EventType.ShouldBe(eventType);
		message.EventBody.ShouldBe(eventBody);
		message.EventMetadata.ShouldBe(eventMetadata);
	}

	[Fact]
	public void AttemptsPropertyShouldBeSettable()
	{
		// Arrange
		var message = CreateDefaultEventStoreMessage();

		// Act
		message.Attempts = 3;

		// Assert
		message.Attempts.ShouldBe(3);
	}

	[Fact]
	public void DispatcherIdPropertyShouldBeSettable()
	{
		// Arrange
		var message = CreateDefaultEventStoreMessage();
		var dispatcherId = "dispatcher-001";

		// Act
		message.DispatcherId = dispatcherId;

		// Assert
		message.DispatcherId.ShouldBe(dispatcherId);
	}

	[Fact]
	public void DispatchedOnPropertyShouldBeSettable()
	{
		// Arrange
		var message = CreateDefaultEventStoreMessage();
		var dispatchedOn = DateTimeOffset.UtcNow;

		// Act
		message.DispatchedOn = dispatchedOn;

		// Assert
		message.DispatchedOn.ShouldBe(dispatchedOn);
	}

	[Fact]
	public void DispatcherTimeoutPropertyShouldBeSettable()
	{
		// Arrange
		var message = CreateDefaultEventStoreMessage();
		var timeout = DateTimeOffset.UtcNow.AddMinutes(30);

		// Act
		message.DispatcherTimeout = timeout;

		// Assert
		message.DispatcherTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void MessageShouldSupportNullableDispatcherProperties()
	{
		// Arrange & Act
		var message = CreateDefaultEventStoreMessage();

		// Assert
		message.DispatcherId.ShouldBeNull();
		message.DispatchedOn.ShouldBeNull();
		message.DispatcherTimeout.ShouldBeNull();
	}

	[Fact]
	public void MessageShouldAllowClearingDispatcherProperties()
	{
		// Arrange
		var message = CreateDefaultEventStoreMessage();
		message.DispatcherId = "dispatcher-001";
		message.DispatchedOn = DateTimeOffset.UtcNow;
		message.DispatcherTimeout = DateTimeOffset.UtcNow.AddMinutes(30);

		// Act
		message.DispatcherId = null;
		message.DispatchedOn = null;
		message.DispatcherTimeout = null;

		// Assert
		message.DispatcherId.ShouldBeNull();
		message.DispatchedOn.ShouldBeNull();
		message.DispatcherTimeout.ShouldBeNull();
	}

	[Fact]
	public void FromEventStoreMessageShouldThrowNotSupportedException()
	{
		// Arrange
		var sourceMessage = new EventStoreMessage<string>
		{
			AggregateId = "test-id",
			OccurredOn = DateTimeOffset.UtcNow,
			EventId = Guid.NewGuid().ToString(),
			EventType = "Tests.Shared.Events.TestEvent",
			EventBody = "{}",
			EventMetadata = "{}",
		};

		// Act & Assert
		_ = Should.Throw<NotSupportedException>(() => _ = EventStoreMessage<Guid>.FromEventStoreMessage(sourceMessage));
	}

	[Fact]
	public void MessageShouldImplementIEventStoreMessage()
	{
		// Arrange & Act
		var message = CreateDefaultEventStoreMessage();

		// Assert
		_ = message.ShouldBeAssignableTo<IEventStoreMessage<string>>();
	}

	[Fact]
	public void MessageShouldSupportComplexAggregateKeyTypes()
	{
		// Arrange
		var aggregateKey = new ComplexKey { Id = "complex-123", Version = 5 };
		var occurredOn = DateTimeOffset.UtcNow;
		var eventId = Guid.NewGuid().ToString();

		// Act
		var message = new EventStoreMessage<ComplexKey>
		{
			AggregateId = aggregateKey,
			OccurredOn = occurredOn,
			EventId = eventId,
			EventType = "ComplexEvent",
			EventBody = "{}",
			EventMetadata = "{}",
		};

		// Assert
		message.AggregateId.ShouldBe(aggregateKey);
		message.AggregateId.Id.ShouldBe("complex-123");
		message.AggregateId.Version.ShouldBe(5);
	}

	[Fact]
	public void MessageShouldHandleEmptyStringsForRequiredProperties()
	{
		// Arrange & Act
		var message = new EventStoreMessage<string>
		{
			AggregateId = string.Empty,
			OccurredOn = DateTimeOffset.UtcNow,
			EventId = string.Empty,
			EventType = string.Empty,
			EventBody = string.Empty,
			EventMetadata = string.Empty,
		};

		// Assert
		message.AggregateId.ShouldBe(string.Empty);
		message.EventId.ShouldBe(string.Empty);
		message.EventType.ShouldBe(string.Empty);
		message.EventBody.ShouldBe(string.Empty);
		message.EventMetadata.ShouldBe(string.Empty);
	}

	[Fact]
	public void MessageShouldPreserveExactDateTimeOffsetValues()
	{
		// Arrange
		var specificTime = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.FromHours(5));
		var message = CreateDefaultEventStoreMessage();

		// Act
		message.DispatchedOn = specificTime;

		// Assert
		message.DispatchedOn.ShouldBe(specificTime);
		message.DispatchedOn.Value.Offset.ShouldBe(TimeSpan.FromHours(5));
	}

	[Fact]
	public void MessageShouldAllowUpdatingAttempts()
	{
		// Arrange
		var message = CreateDefaultEventStoreMessage();

		// Act
		message.Attempts = 1;
		message.Attempts++;
		message.Attempts += 2;

		// Assert
		message.Attempts.ShouldBe(4);
	}

	private static EventStoreMessage<string> CreateDefaultEventStoreMessage() =>
		new()
		{
			AggregateId = "test-aggregate-123",
			OccurredOn = DateTimeOffset.UtcNow,
			EventId = Guid.NewGuid().ToString(),
			EventType = "Tests.Shared.Events.TestEvent",
			EventBody = "{\"test\": \"data\"}",
			EventMetadata = "{\"version\": \"1.0\"}",
		};

	private sealed class ComplexKey
	{
		public required string Id { get; set; } = string.Empty;

		public int Version { get; set; }

		public override bool Equals(object? obj) => obj is ComplexKey other && Id == other.Id && Version == other.Version;

		public override int GetHashCode() => HashCode.Combine(Id, Version);
	}
}
