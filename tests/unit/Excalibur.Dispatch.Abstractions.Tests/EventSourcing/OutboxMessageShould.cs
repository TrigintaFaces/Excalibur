// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.EventSourcing;

/// <summary>
/// Unit tests for the <see cref="OutboxMessage"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class OutboxMessageShould
{
	[Fact]
	public void Should_SetAllRequiredProperties()
	{
		// Arrange
		var id = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		// Act
		var msg = new OutboxMessage
		{
			Id = id,
			AggregateId = "order-123",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{\"orderId\":\"123\"}",
			CreatedAt = now,
			MessageType = "DomainEvent",
		};

		// Assert
		msg.Id.ShouldBe(id);
		msg.AggregateId.ShouldBe("order-123");
		msg.AggregateType.ShouldBe("Order");
		msg.EventType.ShouldBe("OrderCreated");
		msg.EventData.ShouldBe("{\"orderId\":\"123\"}");
		msg.CreatedAt.ShouldBe(now);
		msg.MessageType.ShouldBe("DomainEvent");
	}

	[Fact]
	public void PublishedAt_Should_BeNull_ByDefault()
	{
		// Act
		var msg = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UtcNow,
			MessageType = "DomainEvent",
		};

		// Assert
		msg.PublishedAt.ShouldBeNull();
	}

	[Fact]
	public void RetryCount_Should_DefaultToZero()
	{
		// Act
		var msg = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UtcNow,
			MessageType = "DomainEvent",
		};

		// Assert
		msg.RetryCount.ShouldBe(0);
	}

	[Fact]
	public void Metadata_Should_BeNullable()
	{
		// Act
		var msg = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UtcNow,
			MessageType = "DomainEvent",
			Metadata = "{\"CorrelationId\":\"abc-123\"}",
		};

		// Assert
		msg.Metadata.ShouldBe("{\"CorrelationId\":\"abc-123\"}");
	}

	[Fact]
	public void Should_BeImmutableRecord()
	{
		// Assert
		typeof(OutboxMessage).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void Equality_Should_WorkForRecords()
	{
		// Arrange
		var id = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		var a = new OutboxMessage
		{
			Id = id,
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = now,
			MessageType = "DomainEvent",
		};

		var b = new OutboxMessage
		{
			Id = id,
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = now,
			MessageType = "DomainEvent",
		};

		// Assert
		a.ShouldBe(b);
	}
}
