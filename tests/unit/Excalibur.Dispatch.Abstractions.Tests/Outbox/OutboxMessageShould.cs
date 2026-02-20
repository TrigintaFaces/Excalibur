// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Unit tests for <see cref="OutboxMessage"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxMessageShould
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		// Arrange
		var id = Guid.NewGuid();
		var createdAt = DateTimeOffset.UtcNow;

		// Act
		var message = new OutboxMessage
		{
			Id = id,
			AggregateId = "order-123",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{\"orderId\":\"123\"}",
			CreatedAt = createdAt,
			MessageType = "DomainEvent",
		};

		// Assert
		message.Id.ShouldBe(id);
		message.AggregateId.ShouldBe("order-123");
		message.AggregateType.ShouldBe("Order");
		message.EventType.ShouldBe("OrderCreated");
		message.EventData.ShouldBe("{\"orderId\":\"123\"}");
		message.CreatedAt.ShouldBe(createdAt);
		message.MessageType.ShouldBe("DomainEvent");
	}

	[Fact]
	public void PublishedAt_DefaultsToNull()
	{
		// Act
		var message = CreateTestMessage();

		// Assert
		message.PublishedAt.ShouldBeNull();
	}

	[Fact]
	public void RetryCount_DefaultsToZero()
	{
		// Act
		var message = CreateTestMessage();

		// Assert
		message.RetryCount.ShouldBe(0);
	}

	[Fact]
	public void Metadata_DefaultsToNull()
	{
		// Act
		var message = CreateTestMessage();

		// Assert
		message.Metadata.ShouldBeNull();
	}

	[Fact]
	public void PublishedAt_CanBeSet()
	{
		// Arrange
		var publishedAt = DateTimeOffset.UtcNow;

		// Act
		var message = CreateTestMessage() with { PublishedAt = publishedAt };

		// Assert
		message.PublishedAt.ShouldBe(publishedAt);
	}

	[Fact]
	public void RetryCount_CanBeSet()
	{
		// Act
		var message = CreateTestMessage() with { RetryCount = 3 };

		// Assert
		message.RetryCount.ShouldBe(3);
	}

	[Fact]
	public void Metadata_CanBeSet()
	{
		// Act
		var message = CreateTestMessage() with { Metadata = "{\"CorrelationId\":\"abc\"}" };

		// Assert
		message.Metadata.ShouldBe("{\"CorrelationId\":\"abc\"}");
	}

	[Fact]
	public void SupportsRecordEquality()
	{
		// Arrange
		var id = Guid.NewGuid();
		var time = DateTimeOffset.UtcNow;

		var msg1 = new OutboxMessage
		{
			Id = id, AggregateId = "agg-1", AggregateType = "Order",
			EventType = "Created", EventData = "{}", CreatedAt = time, MessageType = "DomainEvent",
		};
		var msg2 = new OutboxMessage
		{
			Id = id, AggregateId = "agg-1", AggregateType = "Order",
			EventType = "Created", EventData = "{}", CreatedAt = time, MessageType = "DomainEvent",
		};

		// Assert
		msg1.ShouldBe(msg2);
	}

	[Fact]
	public void SupportsWithExpression()
	{
		// Arrange
		var original = CreateTestMessage();

		// Act
		var modified = original with { RetryCount = 5, Metadata = "{\"key\":\"val\"}" };

		// Assert
		modified.RetryCount.ShouldBe(5);
		modified.Metadata.ShouldBe("{\"key\":\"val\"}");
		modified.AggregateId.ShouldBe(original.AggregateId); // Unchanged
	}

	private static OutboxMessage CreateTestMessage() => new()
	{
		Id = Guid.NewGuid(),
		AggregateId = "agg-1",
		AggregateType = "Order",
		EventType = "OrderCreated",
		EventData = "{}",
		CreatedAt = DateTimeOffset.UtcNow,
		MessageType = "DomainEvent",
	};
}
