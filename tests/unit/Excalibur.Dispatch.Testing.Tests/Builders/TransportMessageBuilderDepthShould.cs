// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Testing.Builders;

namespace Excalibur.Dispatch.Testing.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class TransportMessageBuilderDepthShould
{
	[Fact]
	public void BuildWithDefaultValues()
	{
		// Act
		var message = new TransportMessageBuilder().Build();

		// Assert
		message.ShouldNotBeNull();
		message.Id.ShouldNotBeNullOrEmpty();
		Guid.TryParse(message.Id, out _).ShouldBeTrue();
		message.CreatedAt.ShouldNotBe(default);
	}

	[Fact]
	public void SetId()
	{
		// Act
		var message = new TransportMessageBuilder()
			.WithId("custom-id")
			.Build();

		// Assert
		message.Id.ShouldBe("custom-id");
	}

	[Fact]
	public void SetBodyFromByteArray()
	{
		// Arrange
		var body = new byte[] { 1, 2, 3, 4, 5 };

		// Act
		var message = new TransportMessageBuilder()
			.WithBody(body)
			.Build();

		// Assert
		message.Body.ToArray().ShouldBe(body);
	}

	[Fact]
	public void SetBodyFromString()
	{
		// Arrange
		var bodyText = "test-payload";

		// Act
		var message = new TransportMessageBuilder()
			.WithBody(bodyText)
			.Build();

		// Assert
		var decoded = Encoding.UTF8.GetString(message.Body.Span);
		decoded.ShouldBe("test-payload");
	}

	[Fact]
	public void SetContentTypeToJsonWhenBodyIsString()
	{
		// Act
		var message = new TransportMessageBuilder()
			.WithBody("json-body")
			.Build();

		// Assert
		message.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void PreserveExplicitContentTypeWhenSettingStringBody()
	{
		// Act - set content type before string body
		var message = new TransportMessageBuilder()
			.WithContentType("text/plain")
			.WithBody("text-body")
			.Build();

		// Assert - explicit content type should be preserved (not overridden to json)
		message.ContentType.ShouldBe("text/plain");
	}

	[Fact]
	public void SetContentType()
	{
		// Act
		var message = new TransportMessageBuilder()
			.WithContentType("application/xml")
			.Build();

		// Assert
		message.ContentType.ShouldBe("application/xml");
	}

	[Fact]
	public void SetMessageType()
	{
		// Act
		var message = new TransportMessageBuilder()
			.WithMessageType("OrderPlaced")
			.Build();

		// Assert
		message.MessageType.ShouldBe("OrderPlaced");
	}

	[Fact]
	public void SetCorrelationId()
	{
		// Act
		var message = new TransportMessageBuilder()
			.WithCorrelationId("corr-123")
			.Build();

		// Assert
		message.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void SetSubject()
	{
		// Act
		var message = new TransportMessageBuilder()
			.WithSubject("test-subject")
			.Build();

		// Assert
		message.Subject.ShouldBe("test-subject");
	}

	[Fact]
	public void SetTimeToLive()
	{
		// Arrange
		var ttl = TimeSpan.FromMinutes(30);

		// Act
		var message = new TransportMessageBuilder()
			.WithTimeToLive(ttl)
			.Build();

		// Assert
		message.TimeToLive.ShouldBe(ttl);
	}

	[Fact]
	public void SetCreatedAt()
	{
		// Arrange
		var ts = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		var message = new TransportMessageBuilder()
			.WithCreatedAt(ts)
			.Build();

		// Assert
		message.CreatedAt.ShouldBe(ts);
	}

	[Fact]
	public void AddCustomProperties()
	{
		// Act
		var message = new TransportMessageBuilder()
			.WithProperty("X-Custom", "value1")
			.WithProperty("X-Other", 42)
			.Build();

		// Assert
		message.Properties["X-Custom"].ShouldBe("value1");
		message.Properties["X-Other"].ShouldBe(42);
	}

	[Fact]
	public void OverwritePropertyWithSameKey()
	{
		// Act
		var message = new TransportMessageBuilder()
			.WithProperty("key", "first")
			.WithProperty("key", "second")
			.Build();

		// Assert
		message.Properties["key"].ShouldBe("second");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var builder = new TransportMessageBuilder();

		// Act
		var result = builder
			.WithId("id")
			.WithContentType("ct")
			.WithCorrelationId("corr")
			.WithMessageType("mt")
			.WithSubject("subj")
			.WithProperty("key", "val")
			.WithTimeToLive(TimeSpan.FromSeconds(1));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void BuildMultipleDistinctMessages()
	{
		// Arrange
		var builder = new TransportMessageBuilder()
			.WithContentType("text/plain");

		// Act
		var msg1 = builder.Build();
		var msg2 = builder.Build();

		// Assert
		msg1.ShouldNotBeSameAs(msg2);
		msg1.Id.ShouldNotBe(msg2.Id);
	}

	[Fact]
	public void BuildManyWithUniqueIds()
	{
		// Arrange
		var builder = new TransportMessageBuilder()
			.WithMessageType("BatchMessage");

		// Act
		var messages = builder.BuildMany(5);

		// Assert
		messages.Count.ShouldBe(5);
		messages.Select(m => m.Id).Distinct().Count().ShouldBe(5);
	}

	[Fact]
	public void BuildManyWithSharedProperties()
	{
		// Arrange
		var builder = new TransportMessageBuilder()
			.WithContentType("application/xml")
			.WithMessageType("SharedType")
			.WithCorrelationId("shared-corr")
			.WithSubject("shared-subject")
			.WithProperty("shared-prop", "shared-val");

		// Act
		var messages = builder.BuildMany(3);

		// Assert
		messages.ShouldAllBe(m => m.ContentType == "application/xml");
		messages.ShouldAllBe(m => m.MessageType == "SharedType");
		messages.ShouldAllBe(m => m.CorrelationId == "shared-corr");
		messages.ShouldAllBe(m => m.Subject == "shared-subject");
		messages.ShouldAllBe(m => m.Properties.ContainsKey("shared-prop"));
	}

	[Fact]
	public void BuildManyZeroReturnsEmptyList()
	{
		// Act
		var messages = new TransportMessageBuilder().BuildMany(0);

		// Assert
		messages.ShouldBeEmpty();
	}

	[Fact]
	public void DefaultBodyIsEmpty()
	{
		// Act
		var message = new TransportMessageBuilder().Build();

		// Assert
		message.Body.Length.ShouldBe(0);
	}

	[Fact]
	public void DefaultContentTypeIsNull()
	{
		// Act - byte body doesn't set content type
		var message = new TransportMessageBuilder()
			.WithBody(new byte[] { 1 })
			.Build();

		// Assert
		message.ContentType.ShouldBeNull();
	}

	[Fact]
	public void BuildWithAllPropertiesSet()
	{
		// Arrange
		var body = new byte[] { 10, 20 };
		var ttl = TimeSpan.FromHours(1);
		var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var message = new TransportMessageBuilder()
			.WithId("msg-full")
			.WithBody(body)
			.WithContentType("application/octet-stream")
			.WithMessageType("FullType")
			.WithCorrelationId("corr-full")
			.WithSubject("subj-full")
			.WithTimeToLive(ttl)
			.WithCreatedAt(createdAt)
			.WithProperty("p1", "v1")
			.Build();

		// Assert
		message.Id.ShouldBe("msg-full");
		message.Body.ToArray().ShouldBe(body);
		message.ContentType.ShouldBe("application/octet-stream");
		message.MessageType.ShouldBe("FullType");
		message.CorrelationId.ShouldBe("corr-full");
		message.Subject.ShouldBe("subj-full");
		message.TimeToLive.ShouldBe(ttl);
		message.CreatedAt.ShouldBe(createdAt);
		message.Properties.ShouldContainKeyAndValue("p1", (object)"v1");
	}
}
