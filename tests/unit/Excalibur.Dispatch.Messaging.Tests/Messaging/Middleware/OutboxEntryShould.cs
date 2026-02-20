// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="OutboxEntry"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxEntryShould
{
	[Fact]
	public void StoreIdProperty()
	{
		// Arrange
		var id = "entry-123";
		var entry = CreateTestOutboxEntry(id: id);

		// Assert
		entry.Id.ShouldBe(id);
	}

	[Fact]
	public void StoreMessageTypeProperty()
	{
		// Arrange
		var messageType = "TestMessage";
		var entry = CreateTestOutboxEntry(messageType: messageType);

		// Assert
		entry.MessageType.ShouldBe(messageType);
	}

	[Fact]
	public void StoreMessageDataProperty()
	{
		// Arrange
		var messageData = "{\"key\":\"value\"}";
		var entry = CreateTestOutboxEntry(messageData: messageData);

		// Assert
		entry.MessageData.ShouldBe(messageData);
	}

	[Fact]
	public void StoreCorrelationIdProperty()
	{
		// Arrange
		var correlationId = "correlation-456";
		var entry = CreateTestOutboxEntry(correlationId: correlationId);

		// Assert
		entry.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void StoreCausationIdProperty()
	{
		// Arrange
		var causationId = "causation-789";
		var entry = CreateTestOutboxEntry(causationId: causationId);

		// Assert
		entry.CausationId.ShouldBe(causationId);
	}

	[Fact]
	public void StoreTenantIdProperty()
	{
		// Arrange
		var tenantId = "tenant-abc";
		var entry = CreateTestOutboxEntry(tenantId: tenantId);

		// Assert
		entry.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public void StoreDestinationProperty()
	{
		// Arrange
		var destination = "queue://orders";
		var entry = CreateTestOutboxEntry(destination: destination);

		// Assert
		entry.Destination.ShouldBe(destination);
	}

	[Fact]
	public void StoreScheduledAtProperty()
	{
		// Arrange
		var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(5);
		var entry = CreateTestOutboxEntry(scheduledAt: scheduledAt);

		// Assert
		entry.ScheduledAt.ShouldBe(scheduledAt);
	}

	[Fact]
	public void StoreCreatedAtProperty()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow;
		var entry = CreateTestOutboxEntry(createdAt: createdAt);

		// Assert
		entry.CreatedAt.ShouldBe(createdAt);
	}

	[Fact]
	public void ThrowOnNullId()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => CreateTestOutboxEntry(id: null));
	}

	[Fact]
	public void ThrowOnNullMessageType()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => CreateTestOutboxEntry(messageType: null));
	}

	[Fact]
	public void ThrowOnNullMessageData()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => CreateTestOutboxEntry(messageData: null));
	}

	[Fact]
	public void AllowNullCorrelationId()
	{
		// Arrange & Act
		var entry = CreateTestOutboxEntry(correlationId: null);

		// Assert
		entry.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllowNullCausationId()
	{
		// Arrange & Act
		var entry = CreateTestOutboxEntry(causationId: null);

		// Assert
		entry.CausationId.ShouldBeNull();
	}

	[Fact]
	public void AllowNullTenantId()
	{
		// Arrange & Act
		var entry = CreateTestOutboxEntry(tenantId: null);

		// Assert
		entry.TenantId.ShouldBeNull();
	}

	[Fact]
	public void AllowNullDestination()
	{
		// Arrange & Act
		var entry = CreateTestOutboxEntry(destination: null);

		// Assert
		entry.Destination.ShouldBeNull();
	}

	[Fact]
	public void AcceptEmptyMessageData()
	{
		// Arrange & Act
		var entry = CreateTestOutboxEntry(messageData: "");

		// Assert
		entry.MessageData.ShouldBe("");
	}

	[Fact]
	public void AcceptLargeMessageData()
	{
		// Arrange
		var largeData = new string('x', 100000);

		// Act
		var entry = CreateTestOutboxEntry(messageData: largeData);

		// Assert
		entry.MessageData.ShouldBe(largeData);
		entry.MessageData.Length.ShouldBe(100000);
	}

	[Fact]
	public void AcceptJsonMessageData()
	{
		// Arrange
		var jsonData = """
			{
			    "orderId": 12345,
			    "items": [
			        {"name": "Widget", "qty": 2},
			        {"name": "Gadget", "qty": 1}
			    ],
			    "total": 99.99
			}
			""";

		// Act
		var entry = CreateTestOutboxEntry(messageData: jsonData);

		// Assert
		entry.MessageData.ShouldBe(jsonData);
	}

	[Fact]
	public void AcceptVariousMessageTypeFormats()
	{
		// Arrange & Act
		var entry1 = CreateTestOutboxEntry(messageType: "OrderCreated");
		var entry2 = CreateTestOutboxEntry(messageType: "Acme.Orders.Events.OrderCreated, Acme.Orders");
		var entry3 = CreateTestOutboxEntry(messageType: "com.acme.events.order-created");

		// Assert
		entry1.MessageType.ShouldBe("OrderCreated");
		entry2.MessageType.ShouldBe("Acme.Orders.Events.OrderCreated, Acme.Orders");
		entry3.MessageType.ShouldBe("com.acme.events.order-created");
	}

	[Fact]
	public void AcceptPastScheduledAt()
	{
		// Arrange
		var scheduledAt = DateTimeOffset.UtcNow.AddHours(-1);

		// Act
		var entry = CreateTestOutboxEntry(scheduledAt: scheduledAt);

		// Assert
		entry.ScheduledAt.ShouldBe(scheduledAt);
	}

	[Fact]
	public void AcceptFutureScheduledAt()
	{
		// Arrange
		var scheduledAt = DateTimeOffset.UtcNow.AddDays(7);

		// Act
		var entry = CreateTestOutboxEntry(scheduledAt: scheduledAt);

		// Assert
		entry.ScheduledAt.ShouldBe(scheduledAt);
	}

	[Fact]
	public void AcceptMinValueScheduledAt()
	{
		// Arrange & Act
		var entry = CreateTestOutboxEntry(scheduledAt: DateTimeOffset.MinValue);

		// Assert
		entry.ScheduledAt.ShouldBe(DateTimeOffset.MinValue);
	}

	[Fact]
	public void AcceptMaxValueScheduledAt()
	{
		// Arrange & Act
		var entry = CreateTestOutboxEntry(scheduledAt: DateTimeOffset.MaxValue);

		// Assert
		entry.ScheduledAt.ShouldBe(DateTimeOffset.MaxValue);
	}

	[Fact]
	public void HaveImmutableProperties()
	{
		// Arrange
		var entry = CreateTestOutboxEntry();

		// Assert - All properties are get-only (read-only)
		typeof(OutboxEntry).GetProperty(nameof(OutboxEntry.Id)).CanWrite.ShouldBeFalse();
		typeof(OutboxEntry).GetProperty(nameof(OutboxEntry.MessageType)).CanWrite.ShouldBeFalse();
		typeof(OutboxEntry).GetProperty(nameof(OutboxEntry.MessageData)).CanWrite.ShouldBeFalse();
		typeof(OutboxEntry).GetProperty(nameof(OutboxEntry.CorrelationId)).CanWrite.ShouldBeFalse();
		typeof(OutboxEntry).GetProperty(nameof(OutboxEntry.CausationId)).CanWrite.ShouldBeFalse();
		typeof(OutboxEntry).GetProperty(nameof(OutboxEntry.TenantId)).CanWrite.ShouldBeFalse();
		typeof(OutboxEntry).GetProperty(nameof(OutboxEntry.Destination)).CanWrite.ShouldBeFalse();
		typeof(OutboxEntry).GetProperty(nameof(OutboxEntry.ScheduledAt)).CanWrite.ShouldBeFalse();
		typeof(OutboxEntry).GetProperty(nameof(OutboxEntry.CreatedAt)).CanWrite.ShouldBeFalse();
	}

	[Fact]
	public void SimulateTypicalOutboxEntry()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var entry = new OutboxEntry(
			id: Guid.NewGuid().ToString(),
			messageType: "OrderPlaced",
			messageData: """{"orderId": 123, "customerId": "cust-456"}""",
			correlationId: "correlation-123",
			causationId: "causation-456",
			tenantId: "tenant-main",
			destination: "orders-topic",
			scheduledAt: now,
			createdAt: now);

		// Assert
		entry.Id.ShouldNotBeNullOrEmpty();
		entry.MessageType.ShouldBe("OrderPlaced");
		entry.MessageData.ShouldContain("orderId");
		entry.CorrelationId.ShouldBe("correlation-123");
		entry.CausationId.ShouldBe("causation-456");
		entry.TenantId.ShouldBe("tenant-main");
		entry.Destination.ShouldBe("orders-topic");
		entry.ScheduledAt.ShouldBe(now);
		entry.CreatedAt.ShouldBe(now);
	}

	[Fact]
	public void SimulateScheduledMessageOutboxEntry()
	{
		// Arrange - Message scheduled for future delivery
		var now = DateTimeOffset.UtcNow;
		var scheduledTime = now.AddHours(2);

		var entry = new OutboxEntry(
			id: Guid.NewGuid().ToString(),
			messageType: "ScheduledReminder",
			messageData: """{"reminder": "Follow up with customer"}""",
			correlationId: null,
			causationId: null,
			tenantId: null,
			destination: "notifications-queue",
			scheduledAt: scheduledTime,
			createdAt: now);

		// Assert
		entry.ScheduledAt.ShouldBeGreaterThan(entry.CreatedAt);
		entry.CorrelationId.ShouldBeNull();
		entry.CausationId.ShouldBeNull();
		entry.TenantId.ShouldBeNull();
	}

	private static OutboxEntry CreateTestOutboxEntry(
		string? id = "test-id",
		string? messageType = "TestMessage",
		string? messageData = "{}",
		string? correlationId = "correlation-1",
		string? causationId = "causation-1",
		string? tenantId = "tenant-1",
		string? destination = "test-destination",
		DateTimeOffset? scheduledAt = null,
		DateTimeOffset? createdAt = null)
	{
		return new OutboxEntry(
			id,
			messageType,
			messageData,
			correlationId,
			causationId,
			tenantId,
			destination,
			scheduledAt ?? DateTimeOffset.UtcNow,
			createdAt ?? DateTimeOffset.UtcNow);
	}
}
