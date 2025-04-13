using Excalibur.Core.Domain.Events;
using Excalibur.Core.Extensions;
using Excalibur.Data.Outbox;

using Shouldly;

namespace Excalibur.Tests.Unit.Data.Outbox;

public class OutboxMessageShould
{
	[Fact]
	public void InitializeWithDefaultValues()
	{
		// Act
		var message = new OutboxMessage();

		// Assert
		message.MessageId.ShouldNotBeNullOrWhiteSpace();
		message.MessageBody.ShouldBeNull();
		message.MessageHeaders.ShouldBeNull();
	}

	[Fact]
	public void AllowPropertySetting()
	{
		// Arrange
		var messageId = Uuid7Extensions.GenerateString();
		var messageBody = new TestDomainEvent { Id = 1, Name = "Test Event" };
		var messageHeaders = new Dictionary<string, string> { ["header1"] = "value1", ["header2"] = "value2" };

		// Act
		var message = new OutboxMessage { MessageId = messageId, MessageBody = messageBody, MessageHeaders = messageHeaders };

		// Assert
		message.MessageId.ShouldBe(messageId);
		message.MessageBody.ShouldBeSameAs(messageBody);
		message.MessageHeaders.ShouldBeSameAs(messageHeaders);
	}

	[Fact]
	public void NotModifyOriginalHeadersWhenHeadersPropertyIsChanged()
	{
		// Arrange
		var originalHeaders = new Dictionary<string, string> { ["header1"] = "value1" };

		var message = new OutboxMessage
		{
			MessageId = Uuid7Extensions.GenerateString(),
			MessageBody = new TestDomainEvent { Id = 1, Name = "Test Event" },
			MessageHeaders = originalHeaders
		};

		var newHeaders = new Dictionary<string, string> { ["header2"] = "value2" };

		// Act
		message.MessageHeaders = newHeaders;

		// Assert
		message.MessageHeaders.ShouldBeSameAs(newHeaders);
		originalHeaders.Count.ShouldBe(1);
		originalHeaders.ShouldContainKey("header1");
		originalHeaders.ShouldNotContainKey("header2");
	}

	private sealed class TestDomainEvent : IDomainEvent
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
	}
}
