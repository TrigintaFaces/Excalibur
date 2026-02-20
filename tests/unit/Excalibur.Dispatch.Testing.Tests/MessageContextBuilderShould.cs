// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing;

namespace Excalibur.Dispatch.Testing.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Testing")]
public sealed class MessageContextBuilderShould
{
	[Fact]
	public void GenerateDefaultMessageId()
	{
		var context = new MessageContextBuilder().Build();
		context.MessageId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(context.MessageId, out _).ShouldBeTrue();
	}

	[Fact]
	public void GenerateDefaultCorrelationId()
	{
		var context = new MessageContextBuilder().Build();
		context.CorrelationId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(context.CorrelationId, out _).ShouldBeTrue();
	}

	[Fact]
	public void SetReceivedTimestampToUtcNow()
	{
		var before = DateTimeOffset.UtcNow;
		var context = new MessageContextBuilder().Build();
		var after = DateTimeOffset.UtcNow;

		context.ReceivedTimestampUtc.ShouldBeGreaterThanOrEqualTo(before);
		context.ReceivedTimestampUtc.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void OverrideMessageId()
	{
		var context = new MessageContextBuilder()
			.WithMessageId("custom-id")
			.Build();

		context.MessageId.ShouldBe("custom-id");
	}

	[Fact]
	public void OverrideCorrelationId()
	{
		var context = new MessageContextBuilder()
			.WithCorrelationId("corr-123")
			.Build();

		context.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void SetCausationId()
	{
		var context = new MessageContextBuilder()
			.WithCausationId("cause-456")
			.Build();

		context.CausationId.ShouldBe("cause-456");
	}

	[Fact]
	public void SetTenantId()
	{
		var context = new MessageContextBuilder()
			.WithTenantId("tenant-A")
			.Build();

		context.TenantId.ShouldBe("tenant-A");
	}

	[Fact]
	public void SetUserId()
	{
		var context = new MessageContextBuilder()
			.WithUserId("user-1")
			.Build();

		context.UserId.ShouldBe("user-1");
	}

	[Fact]
	public void SetSessionId()
	{
		var context = new MessageContextBuilder()
			.WithSessionId("sess-abc")
			.Build();

		context.SessionId.ShouldBe("sess-abc");
	}

	[Fact]
	public void SetWorkflowId()
	{
		var context = new MessageContextBuilder()
			.WithWorkflowId("wf-789")
			.Build();

		context.WorkflowId.ShouldBe("wf-789");
	}

	[Fact]
	public void SetPartitionKey()
	{
		var context = new MessageContextBuilder()
			.WithPartitionKey("pk-1")
			.Build();

		context.PartitionKey.ShouldBe("pk-1");
	}

	[Fact]
	public void SetSource()
	{
		var context = new MessageContextBuilder()
			.WithSource("my-service")
			.Build();

		context.Source.ShouldBe("my-service");
	}

	[Fact]
	public void SetMessageType()
	{
		var context = new MessageContextBuilder()
			.WithMessageType("OrderCreated")
			.Build();

		context.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void SetContentType()
	{
		var context = new MessageContextBuilder()
			.WithContentType("application/json")
			.Build();

		context.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void SetTraceParent()
	{
		var context = new MessageContextBuilder()
			.WithTraceParent("00-trace-span-01")
			.Build();

		context.TraceParent.ShouldBe("00-trace-span-01");
	}

	[Fact]
	public void SetExternalId()
	{
		var context = new MessageContextBuilder()
			.WithExternalId("ext-999")
			.Build();

		context.ExternalId.ShouldBe("ext-999");
	}

	[Fact]
	public void SetDeliveryCount()
	{
		var context = new MessageContextBuilder()
			.WithDeliveryCount(3)
			.Build();

		context.DeliveryCount.ShouldBe(3);
	}

	[Fact]
	public void SetRequestServices()
	{
		var sp = A.Fake<IServiceProvider>();
		var context = new MessageContextBuilder()
			.WithRequestServices(sp)
			.Build();

		context.RequestServices.ShouldBeSameAs(sp);
	}

	[Fact]
	public void SetMessage()
	{
		var msg = A.Fake<IDispatchMessage>();
		var context = new MessageContextBuilder()
			.WithMessage(msg)
			.Build();

		context.Message.ShouldBeSameAs(msg);
	}

	[Fact]
	public void AddCustomItems()
	{
		var context = new MessageContextBuilder()
			.WithItem("key1", "value1")
			.WithItem("key2", 42)
			.Build();

		context.GetItem<string>("key1").ShouldBe("value1");
		context.GetItem<int>("key2").ShouldBe(42);
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var builder = new MessageContextBuilder();
		var returned = builder
			.WithMessageId("id")
			.WithCorrelationId("corr")
			.WithTenantId("tenant")
			.WithUserId("user");

		returned.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ProduceDistinctContextsFromSameBuilder()
	{
		var builder = new MessageContextBuilder()
			.WithTenantId("shared");

		var ctx1 = builder.Build();
		var ctx2 = builder.Build();

		ctx1.ShouldNotBeSameAs(ctx2);
		ctx1.MessageId.ShouldNotBe(ctx2.MessageId); // auto-generated, unique each call
	}
}
