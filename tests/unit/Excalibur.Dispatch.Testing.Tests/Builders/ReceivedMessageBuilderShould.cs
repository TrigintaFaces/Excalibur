// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Testing.Builders;

namespace Excalibur.Dispatch.Testing.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class ReceivedMessageBuilderShould
{
	[Fact]
	public void BuildWithDefaults()
	{
		var msg = new ReceivedMessageBuilder().Build();
		msg.ShouldNotBeNull();
		msg.Id.ShouldNotBeNullOrEmpty();
		msg.DeliveryCount.ShouldBe(1);
		msg.EnqueuedAt.ShouldNotBe(default);
	}

	[Fact]
	public void SetId()
	{
		var msg = new ReceivedMessageBuilder()
			.WithId("recv-123")
			.Build();

		msg.Id.ShouldBe("recv-123");
	}

	[Fact]
	public void SetBodyFromBytes()
	{
		var body = new byte[] { 4, 5, 6 };
		var msg = new ReceivedMessageBuilder()
			.WithBody(body)
			.Build();

		msg.Body.ToArray().ShouldBe(body);
	}

	[Fact]
	public void SetBodyFromString()
	{
		var msg = new ReceivedMessageBuilder()
			.WithBody("test-payload")
			.Build();

		Encoding.UTF8.GetString(msg.Body.Span).ShouldBe("test-payload");
		msg.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void NotOverrideExplicitContentTypeWhenSettingStringBody()
	{
		var msg = new ReceivedMessageBuilder()
			.WithContentType("text/plain")
			.WithBody("test")
			.Build();

		msg.ContentType.ShouldBe("text/plain");
	}

	[Fact]
	public void SetContentType()
	{
		var msg = new ReceivedMessageBuilder()
			.WithContentType("application/xml")
			.Build();

		msg.ContentType.ShouldBe("application/xml");
	}

	[Fact]
	public void SetMessageType()
	{
		var msg = new ReceivedMessageBuilder()
			.WithMessageType("OrderPlaced")
			.Build();

		msg.MessageType.ShouldBe("OrderPlaced");
	}

	[Fact]
	public void SetCorrelationId()
	{
		var msg = new ReceivedMessageBuilder()
			.WithCorrelationId("corr-123")
			.Build();

		msg.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void SetSubject()
	{
		var msg = new ReceivedMessageBuilder()
			.WithSubject("test-subject")
			.Build();

		msg.Subject.ShouldBe("test-subject");
	}

	[Fact]
	public void SetDeliveryCount()
	{
		var msg = new ReceivedMessageBuilder()
			.WithDeliveryCount(5)
			.Build();

		msg.DeliveryCount.ShouldBe(5);
	}

	[Fact]
	public void SetEnqueuedAt()
	{
		var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var msg = new ReceivedMessageBuilder()
			.WithEnqueuedAt(ts)
			.Build();

		msg.EnqueuedAt.ShouldBe(ts);
	}

	[Fact]
	public void SetSource()
	{
		var msg = new ReceivedMessageBuilder()
			.WithSource("queue-1")
			.Build();

		msg.Source.ShouldBe("queue-1");
	}

	[Fact]
	public void SetPartitionKey()
	{
		var msg = new ReceivedMessageBuilder()
			.WithPartitionKey("pk-abc")
			.Build();

		msg.PartitionKey.ShouldBe("pk-abc");
	}

	[Fact]
	public void SetMessageGroupId()
	{
		var msg = new ReceivedMessageBuilder()
			.WithMessageGroupId("group-1")
			.Build();

		msg.MessageGroupId.ShouldBe("group-1");
	}

	[Fact]
	public void SetLockExpiresAt()
	{
		var ts = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
		var msg = new ReceivedMessageBuilder()
			.WithLockExpiresAt(ts)
			.Build();

		msg.LockExpiresAt.ShouldBe(ts);
	}

	[Fact]
	public void SetProperty()
	{
		var msg = new ReceivedMessageBuilder()
			.WithProperty("key1", "value1")
			.Build();

		msg.Properties["key1"].ShouldBe("value1");
	}

	[Fact]
	public void SetProviderData()
	{
		var msg = new ReceivedMessageBuilder()
			.WithProviderData("provider-key", "provider-value")
			.Build();

		msg.ProviderData["provider-key"].ShouldBe("provider-value");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var msg = new ReceivedMessageBuilder()
			.WithId("msg-1")
			.WithBody("payload")
			.WithMessageType("Test")
			.WithCorrelationId("corr")
			.WithSubject("subj")
			.WithDeliveryCount(2)
			.WithSource("queue")
			.WithPartitionKey("pk")
			.WithMessageGroupId("grp")
			.WithProperty("k", "v")
			.WithProviderData("pk", "pv")
			.Build();

		msg.Id.ShouldBe("msg-1");
		msg.MessageType.ShouldBe("Test");
		msg.DeliveryCount.ShouldBe(2);
	}

	[Fact]
	public void BuildManyWithUniqueIds()
	{
		var messages = new ReceivedMessageBuilder()
			.WithBody("shared")
			.WithDeliveryCount(3)
			.BuildMany(5);

		messages.Count.ShouldBe(5);
		messages.Select(m => m.Id).Distinct().Count().ShouldBe(5);
	}

	[Fact]
	public void BuildManySharesProperties()
	{
		var messages = new ReceivedMessageBuilder()
			.WithMessageType("SharedType")
			.WithDeliveryCount(2)
			.WithSource("shared-source")
			.BuildMany(3);

		messages.ShouldAllBe(m => m.MessageType == "SharedType");
		messages.ShouldAllBe(m => m.DeliveryCount == 2);
		messages.ShouldAllBe(m => m.Source == "shared-source");
	}
}
