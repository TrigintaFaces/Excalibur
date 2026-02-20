// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="StoredEvent"/> record to verify immutability and property behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StoredEventShould
{
	[Fact]
	public void CreateStoredEvent_WithAllProperties()
	{
		// Arrange
		var eventId = Guid.NewGuid().ToString();
		var aggregateId = "order-123";
		var aggregateType = "OrderAggregate";
		var eventType = "OrderCreated";
		var eventData = new byte[] { 1, 2, 3, 4 };
		var metadata = new byte[] { 5, 6, 7 };
		var version = 1L;
		var timestamp = DateTimeOffset.UtcNow;
		var isDispatched = false;

		// Act
		var storedEvent = new StoredEvent(
			eventId,
			aggregateId,
			aggregateType,
			eventType,
			eventData,
			metadata,
			version,
			timestamp,
			isDispatched);

		// Assert
		storedEvent.EventId.ShouldBe(eventId);
		storedEvent.AggregateId.ShouldBe(aggregateId);
		storedEvent.AggregateType.ShouldBe(aggregateType);
		storedEvent.EventType.ShouldBe(eventType);
		storedEvent.EventData.ShouldBe(eventData);
		storedEvent.Metadata.ShouldBe(metadata);
		storedEvent.Version.ShouldBe(version);
		storedEvent.Timestamp.ShouldBe(timestamp);
		storedEvent.IsDispatched.ShouldBe(isDispatched);
	}

	[Fact]
	public void CreateStoredEvent_WithNullMetadata()
	{
		// Arrange & Act
		var storedEvent = new StoredEvent(
			"event-1",
			"aggregate-1",
			"TestAggregate",
			"TestEvent",
			new byte[] { 1 },
			null,
			1,
			DateTimeOffset.UtcNow,
			false);

		// Assert
		storedEvent.Metadata.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var eventData = new byte[] { 1, 2, 3 };

		var event1 = new StoredEvent(
			"event-1", "agg-1", "Agg", "Type", eventData, null, 1, timestamp, false);
		var event2 = new StoredEvent(
			"event-1", "agg-1", "Agg", "Type", eventData, null, 1, timestamp, false);

		// Assert - Records with same values should be equal
		event1.ShouldBe(event2);
	}

	[Fact]
	public void SupportRecordInequality_WhenVersionDiffers()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var eventData = new byte[] { 1, 2, 3 };

		var event1 = new StoredEvent(
			"event-1", "agg-1", "Agg", "Type", eventData, null, 1, timestamp, false);
		var event2 = new StoredEvent(
			"event-1", "agg-1", "Agg", "Type", eventData, null, 2, timestamp, false);

		// Assert
		event1.ShouldNotBe(event2);
	}

	[Fact]
	public void SupportWithExpression_ForImmutableUpdates()
	{
		// Arrange
		var original = new StoredEvent(
			"event-1", "agg-1", "Agg", "Type", new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow, false);

		// Act
		var updated = original with { IsDispatched = true };

		// Assert
		updated.IsDispatched.ShouldBeTrue();
		original.IsDispatched.ShouldBeFalse();
		updated.EventId.ShouldBe(original.EventId);
	}

	[Fact]
	public void HandleEmptyEventData()
	{
		// Arrange & Act
		var storedEvent = new StoredEvent(
			"event-1",
			"aggregate-1",
			"TestAggregate",
			"TestEvent",
			Array.Empty<byte>(),
			null,
			1,
			DateTimeOffset.UtcNow,
			false);

		// Assert
		storedEvent.EventData.ShouldBeEmpty();
	}
}
