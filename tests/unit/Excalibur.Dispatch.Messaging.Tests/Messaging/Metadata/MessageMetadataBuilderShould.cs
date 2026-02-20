// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Metadata;

namespace Excalibur.Dispatch.Tests.Messaging.Metadata;

/// <summary>
///     Tests for the <see cref="MessageMetadataBuilder" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessageMetadataBuilderShould
{
	[Fact]
	public void BuildWithDefaults()
	{
		var builder = new MessageMetadataBuilder();
		var metadata = builder.Build();

		metadata.ShouldNotBeNull();
		metadata.MessageId.ShouldNotBeNullOrWhiteSpace();
		metadata.ContentType.ShouldBe("application/json");
		metadata.MessageVersion.ShouldBe("1.0");
		metadata.SerializerVersion.ShouldBe("1.0");
	}

	[Fact]
	public void SetMessageId()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithMessageId("test-id-123");
		var metadata = builder.Build();

		metadata.MessageId.ShouldBe("test-id-123");
	}

	[Fact]
	public void SetCorrelationIdAutomaticallyFromMessageId()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithMessageId("test-id-123");
		var metadata = builder.Build();

		metadata.CorrelationId.ShouldBe("test-id-123");
	}

	[Fact]
	public void SetExplicitCorrelationId()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithMessageId("msg-1");
		builder.WithCorrelationId("corr-1");
		var metadata = builder.Build();

		metadata.CorrelationId.ShouldBe("corr-1");
	}

	[Fact]
	public void ThrowForNullOrWhiteSpaceMessageId()
	{
		var builder = new MessageMetadataBuilder();
		Should.Throw<ArgumentException>(() => builder.WithMessageId(string.Empty));
		Should.Throw<ArgumentException>(() => builder.WithMessageId("   "));
	}

	[Fact]
	public void ThrowForNullOrWhiteSpaceCorrelationId()
	{
		var builder = new MessageMetadataBuilder();
		Should.Throw<ArgumentException>(() => builder.WithCorrelationId(string.Empty));
		Should.Throw<ArgumentException>(() => builder.WithCorrelationId("   "));
	}

	[Fact]
	public void SetCausationId()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithCausationId("cause-1");
		var metadata = builder.Build();

		metadata.CausationId.ShouldBe("cause-1");
	}

	[Fact]
	public void SetExternalId()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithExternalId("ext-1");
		var metadata = builder.Build();

		metadata.ExternalId.ShouldBe("ext-1");
	}

	[Fact]
	public void SetUserId()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithUserId("user-1");
		var metadata = builder.Build();

		metadata.UserId.ShouldBe("user-1");
	}

	[Fact]
	public void SetTenantId()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithTenantId("tenant-1");
		var metadata = builder.Build();

		metadata.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void SetTraceParent()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithTraceParent("00-trace-parent");
		var metadata = builder.Build();

		metadata.TraceParent.ShouldBe("00-trace-parent");
	}

	[Fact]
	public void SetTraceState()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithTraceState("state=value");
		var metadata = builder.Build();

		metadata.TraceState.ShouldBe("state=value");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var builder = new MessageMetadataBuilder();
		var result = builder
			.WithMessageId("msg-1")
			.WithCorrelationId("corr-1")
			.WithCausationId("cause-1")
			.WithUserId("user-1")
			.WithTenantId("tenant-1");

		result.ShouldNotBeNull();
		result.ShouldBeAssignableTo<IMessageMetadataBuilder>();
	}

	[Fact]
	public void ImplementIMessageMetadataBuilder()
	{
		var builder = new MessageMetadataBuilder();
		builder.ShouldBeAssignableTo<IMessageMetadataBuilder>();
	}

	[Fact]
	public void SetMessageType()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithMessageType("OrderCreated");
		var metadata = builder.Build();

		metadata.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void SetContentType()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithContentType("application/xml");
		var metadata = builder.Build();

		metadata.ContentType.ShouldBe("application/xml");
	}

	[Fact]
	public void SetSource()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithSource("OrderService");
		var metadata = builder.Build();

		metadata.Source.ShouldBe("OrderService");
	}

	[Fact]
	public void SetDestination()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithDestination("PaymentService");
		var metadata = builder.Build();

		metadata.Destination.ShouldBe("PaymentService");
	}

	[Fact]
	public void AddHeader()
	{
		var builder = new MessageMetadataBuilder();
		builder.AddHeader("x-custom", "value1");
		var metadata = builder.Build();

		metadata.Headers.ShouldContainKeyAndValue("x-custom", "value1");
	}

	[Fact]
	public void AddMultipleHeaders()
	{
		var builder = new MessageMetadataBuilder();
		builder.AddHeader("x-one", "1");
		builder.AddHeader("x-two", "2");
		var metadata = builder.Build();

		metadata.Headers.Count.ShouldBe(2);
	}

	[Fact]
	public void SetPartitionKey()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithPartitionKey("pk-1");
		var metadata = builder.Build();

		metadata.PartitionKey.ShouldBe("pk-1");
	}

	[Fact]
	public void SetRoutingKey()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithRoutingKey("orders.created");
		var metadata = builder.Build();

		metadata.RoutingKey.ShouldBe("orders.created");
	}

	[Fact]
	public void SetSessionId()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithSessionId("session-1");
		var metadata = builder.Build();

		metadata.SessionId.ShouldBe("session-1");
	}

	[Fact]
	public void SetReplyTo()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithReplyTo("reply-queue");
		var metadata = builder.Build();

		metadata.ReplyTo.ShouldBe("reply-queue");
	}

	[Fact]
	public void SetTimeToLive()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithTimeToLive(TimeSpan.FromMinutes(5));
		var metadata = builder.Build();

		metadata.TimeToLive.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void SetPriority()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithPriority(3);
		var metadata = builder.Build();

		metadata.Priority.ShouldBe(3);
	}

	[Fact]
	public void SetDeliveryCount()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithDeliveryCount(2);
		var metadata = builder.Build();

		metadata.DeliveryCount.ShouldBe(2);
	}

	[Fact]
	public void SetAggregateIdViaEventSourcing()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithEventSourcing(aggregateId: "agg-1");
		var metadata = builder.Build();

		metadata.AggregateId.ShouldBe("agg-1");
	}

	[Fact]
	public void SetGroupId()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithGroupId("group-1");
		var metadata = builder.Build();

		metadata.GroupId.ShouldBe("group-1");
	}
}
