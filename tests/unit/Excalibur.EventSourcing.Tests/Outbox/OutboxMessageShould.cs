// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

using OutboxMessage = Excalibur.EventSourcing.Outbox.OutboxMessage;

namespace Excalibur.EventSourcing.Tests.Outbox;

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
		// Arrange & Act
		var message = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{\"orderId\":\"123\"}",
			CreatedAt = DateTimeOffset.UtcNow,
			MessageType = "OrderCreated"
		};

		// Assert
		message.AggregateId.ShouldBe("agg-1");
		message.AggregateType.ShouldBe("Order");
		message.EventType.ShouldBe("OrderCreated");
		message.EventData.ShouldBe("{\"orderId\":\"123\"}");
		message.MessageType.ShouldBe("OrderCreated");
		message.PublishedAt.ShouldBeNull();
		message.RetryCount.ShouldBe(0);
		message.Metadata.ShouldBeNull();
	}

	[Fact]
	public void SetOptionalProperties()
	{
		// Arrange
		var publishedAt = DateTimeOffset.UtcNow;

		// Act
		var message = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-2",
			AggregateType = "Customer",
			EventType = "CustomerRegistered",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
			MessageType = "CustomerRegistered",
			PublishedAt = publishedAt,
			RetryCount = 3,
			Metadata = "{\"correlationId\":\"corr-123\"}"
		};

		// Assert
		message.PublishedAt.ShouldBe(publishedAt);
		message.RetryCount.ShouldBe(3);
		message.Metadata.ShouldBe("{\"correlationId\":\"corr-123\"}");
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var id = Guid.NewGuid();
		var createdAt = DateTimeOffset.UtcNow;

		var message1 = new OutboxMessage
		{
			Id = id,
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = createdAt,
			MessageType = "OrderCreated"
		};

		var message2 = new OutboxMessage
		{
			Id = id,
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = createdAt,
			MessageType = "OrderCreated"
		};

		// Assert
		message1.ShouldBe(message2);
	}

	[Fact]
	public void SupportRecordInequality()
	{
		// Arrange
		var message1 = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UtcNow,
			MessageType = "OrderCreated"
		};

		var message2 = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-2",
			AggregateType = "Customer",
			EventType = "CustomerRegistered",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UtcNow,
			MessageType = "CustomerRegistered"
		};

		// Assert
		message1.ShouldNotBe(message2);
	}

	[Fact]
	public void SupportWithExpression_ForRecordCopy()
	{
		// Arrange
		var original = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UtcNow,
			MessageType = "OrderCreated",
			RetryCount = 0
		};

		// Act
		var published = original with { PublishedAt = DateTimeOffset.UtcNow, RetryCount = 1 };

		// Assert
		published.Id.ShouldBe(original.Id);
		published.AggregateId.ShouldBe(original.AggregateId);
		published.PublishedAt.ShouldNotBeNull();
		published.RetryCount.ShouldBe(1);
	}

	[Fact]
	public void SupportToString()
	{
		// Arrange
		var message = new OutboxMessage
		{
			Id = Guid.Empty,
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UnixEpoch,
			MessageType = "OrderCreated"
		};

		// Act
		var str = message.ToString();

		// Assert
		str.ShouldNotBeNullOrEmpty();
		str.ShouldContain("OutboxMessage");
	}

	[Fact]
	public void HaveConsistentHashCode_ForEqualRecords()
	{
		// Arrange
		var id = Guid.NewGuid();
		var createdAt = DateTimeOffset.UtcNow;

		var message1 = new OutboxMessage
		{
			Id = id,
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = createdAt,
			MessageType = "OrderCreated"
		};

		var message2 = new OutboxMessage
		{
			Id = id,
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = createdAt,
			MessageType = "OrderCreated"
		};

		// Assert
		message1.GetHashCode().ShouldBe(message2.GetHashCode());
	}
}
