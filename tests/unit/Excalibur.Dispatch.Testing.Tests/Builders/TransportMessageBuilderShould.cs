// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Testing.Builders;

namespace Excalibur.Dispatch.Testing.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class TransportMessageBuilderShould
{
	[Fact]
	public void BuildWithDefaults()
	{
		var msg = new TransportMessageBuilder().Build();
		msg.ShouldNotBeNull();
		msg.Id.ShouldNotBeNullOrEmpty();
		msg.CreatedAt.ShouldNotBe(default);
	}

	[Fact]
	public void SetId()
	{
		var msg = new TransportMessageBuilder()
			.WithId("msg-123")
			.Build();

		msg.Id.ShouldBe("msg-123");
	}

	[Fact]
	public void SetBodyFromBytes()
	{
		var body = new byte[] { 1, 2, 3 };
		var msg = new TransportMessageBuilder()
			.WithBody(body)
			.Build();

		msg.Body.ToArray().ShouldBe(body);
	}

	[Fact]
	public void SetBodyFromStringAndDefaultContentType()
	{
		var msg = new TransportMessageBuilder()
			.WithBody("test-payload")
			.Build();

		Encoding.UTF8.GetString(msg.Body.Span).ShouldBe("test-payload");
		msg.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void NotOverrideExplicitContentTypeWhenSettingStringBody()
	{
		var msg = new TransportMessageBuilder()
			.WithContentType("text/plain")
			.WithBody("test")
			.Build();

		msg.ContentType.ShouldBe("text/plain");
	}

	[Fact]
	public void SetContentType()
	{
		var msg = new TransportMessageBuilder()
			.WithContentType("application/xml")
			.Build();

		msg.ContentType.ShouldBe("application/xml");
	}

	[Fact]
	public void SetMessageType()
	{
		var msg = new TransportMessageBuilder()
			.WithMessageType("OrderPlaced")
			.Build();

		msg.MessageType.ShouldBe("OrderPlaced");
	}

	[Fact]
	public void SetCorrelationId()
	{
		var msg = new TransportMessageBuilder()
			.WithCorrelationId("corr-123")
			.Build();

		msg.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void SetSubject()
	{
		var msg = new TransportMessageBuilder()
			.WithSubject("test-subject")
			.Build();

		msg.Subject.ShouldBe("test-subject");
	}

	[Fact]
	public void SetTimeToLive()
	{
		var ttl = TimeSpan.FromMinutes(5);
		var msg = new TransportMessageBuilder()
			.WithTimeToLive(ttl)
			.Build();

		msg.TimeToLive.ShouldBe(ttl);
	}

	[Fact]
	public void SetCreatedAt()
	{
		var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var msg = new TransportMessageBuilder()
			.WithCreatedAt(ts)
			.Build();

		msg.CreatedAt.ShouldBe(ts);
	}

	[Fact]
	public void SetProperty()
	{
		var msg = new TransportMessageBuilder()
			.WithProperty("key1", "value1")
			.Build();

		msg.Properties["key1"].ShouldBe("value1");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var msg = new TransportMessageBuilder()
			.WithId("msg-1")
			.WithBody("payload")
			.WithMessageType("Test")
			.WithCorrelationId("corr")
			.WithSubject("subj")
			.WithTimeToLive(TimeSpan.FromSeconds(30))
			.WithProperty("k", "v")
			.Build();

		msg.Id.ShouldBe("msg-1");
		msg.MessageType.ShouldBe("Test");
		msg.CorrelationId.ShouldBe("corr");
	}

	[Fact]
	public void BuildManyWithUniqueIds()
	{
		var messages = new TransportMessageBuilder()
			.WithBody("shared-body")
			.WithMessageType("Test")
			.BuildMany(5);

		messages.Count.ShouldBe(5);
		messages.Select(m => m.Id).Distinct().Count().ShouldBe(5);
	}

	[Fact]
	public void BuildManySharesProperties()
	{
		var messages = new TransportMessageBuilder()
			.WithMessageType("SharedType")
			.WithCorrelationId("shared-corr")
			.WithProperty("shared-key", "shared-val")
			.BuildMany(3);

		messages.ShouldAllBe(m => m.MessageType == "SharedType");
		messages.ShouldAllBe(m => m.CorrelationId == "shared-corr");
	}
}
