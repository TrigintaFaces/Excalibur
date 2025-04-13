using System.Text.Json;

using Excalibur.Core.Domain.Events;
using Excalibur.Data.Outbox;
using Excalibur.Data.Outbox.Serialization;

using Shouldly;

namespace Excalibur.Tests.Unit.Data.Outbox.Serialization;

public class OutboxMessageJsonConverterShould
{
	private readonly JsonSerializerOptions _options = new()
	{
		Converters = { new OutboxMessageJsonConverter() },
		PropertyNameCaseInsensitive = true
	};

	[Fact]
	public void CanConvertReturnTrueForOutboxMessageType()
	{
		// Arrange
		var converter = new OutboxMessageJsonConverter();

		// Act
		var result = converter.CanConvert(typeof(OutboxMessage));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanConvertReturnFalseForOtherTypes()
	{
		// Arrange
		var converter = new OutboxMessageJsonConverter();

		// Act
		var result = converter.CanConvert(typeof(object));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void SerializeOutboxMessageWithAllProperties()
	{
		// Arrange
		var message = new OutboxMessage
		{
			MessageId = "test-message-id",
			MessageBody = new TestDomainEvent { Id = 1, Name = "Test Event" },
			MessageHeaders = new Dictionary<string, string> { ["test-header"] = "test-value" },
			Destination = "test-destination"
		};

		// Act
		var json = JsonSerializer.Serialize(message, _options);

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();

		// Deserialize into JsonDocument to inspect values
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		var typeElement = root.GetProperty("$messageBodyType").GetString();
		var expectedType = $"{typeof(TestDomainEvent).FullName}, {typeof(TestDomainEvent).Assembly.GetName().Name}";
		typeElement.ShouldBe(expectedType);

		root.GetProperty("MessageId").GetString().ShouldBe("test-message-id");
		root.GetProperty("Destination").GetString().ShouldBe("test-destination");

		var headers = root.GetProperty("MessageHeaders");
		headers.GetProperty("test-header").GetString().ShouldBe("test-value");

		var body = root.GetProperty("MessageBody");
		body.GetProperty("Id").GetInt32().ShouldBe(1);
		body.GetProperty("Name").GetString().ShouldBe("Test Event");
	}

	[Fact]
	public void SerializeThrowsWhenMessageBodyIsNull()
	{
		var message = new OutboxMessage
		{
			MessageId = "null-body-id",
			MessageBody = null!,
			MessageHeaders = new Dictionary<string, string>(),
			Destination = "fail-destination"
		};

		Should.Throw<JsonException>(() => JsonSerializer.Serialize(message, _options))
			.Message.ShouldContain("MessageBody cannot be null");
	}

	[Fact]
	public void DeserializeOutboxMessageWithAllProperties()
	{
		// Arrange
		var typeName = $"{typeof(TestDomainEvent).FullName}, {typeof(TestDomainEvent).Assembly.GetName().Name}";
		var json = $@"{{
			""$messageBodyType"": ""{typeName}"",
			""MessageId"": ""test-message-id"",
			""Destination"": ""test-destination"",
			""MessageHeaders"": {{
				""test-header"": ""test-value""
			}},
			""MessageBody"": {{
				""Id"": 1,
				""Name"": ""Test Event""
			}}
		}}";

		// Act
		var message = JsonSerializer.Deserialize<OutboxMessage>(json, _options);

		// Assert
		_ = message.ShouldNotBeNull();
		message.MessageId.ShouldBe("test-message-id");
		message.Destination.ShouldBe("test-destination");
		_ = message.MessageHeaders.ShouldNotBeNull();
		message.MessageHeaders!.Count.ShouldBe(1);
		message.MessageHeaders["test-header"].ShouldBe("test-value");
		_ = message.MessageBody.ShouldNotBeNull();
		_ = message.MessageBody.ShouldBeOfType<TestDomainEvent>();

		var eventBody = message.MessageBody as TestDomainEvent;
		eventBody!.Id.ShouldBe(1);
		eventBody.Name.ShouldBe("Test Event");
	}

	[Fact]
	public void DeserializeThrowJsonExceptionWhenMessageBodyTypeIsInvalid()
	{
		// Arrange
		var json = $@"{{
			""$messageBodyType"": ""NonExistentType, NonExistentAssembly"",
			""MessageId"": ""test-message-id"",
			""MessageBody"": {{}}
		}}";

		// Act & Assert
		_ = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<OutboxMessage>(json, _options));
	}

	[Fact]
	public void DeserializeThrowJsonExceptionWhenJsonFormatIsInvalid()
	{
		// Arrange
		var json = $@"{{
			""$messageBodyType"": ""{typeof(TestDomainEvent).FullName}, {typeof(TestDomainEvent).Assembly.GetName().Name}"",
			""MessageId"": ""test-message-id"",
			""MessageBody"":  // Missing value
		}}";

		// Act & Assert
		_ = Should.Throw<System.Text.Json.JsonException>(() => JsonSerializer.Deserialize<OutboxMessage>(json, _options));
	}

	[Fact]
	public void RoundTripOutboxMessageSerializeAndDeserializeCorrectly()
	{
		// Arrange
		var originalMessage = new OutboxMessage
		{
			MessageId = "roundtrip-id",
			MessageBody = new TestDomainEvent { Id = 42, Name = "Roundtrip Event" },
			MessageHeaders = new Dictionary<string, string> { ["roundtrip-header"] = "roundtrip-value" },
			Destination = "roundtrip-destination"
		};

		// Act
		var json = JsonSerializer.Serialize(originalMessage, _options);
		var deserializedMessage = JsonSerializer.Deserialize<OutboxMessage>(json, _options);

		// Assert
		_ = deserializedMessage.ShouldNotBeNull();
		deserializedMessage.MessageId.ShouldBe(originalMessage.MessageId);
		deserializedMessage.Destination.ShouldBe(originalMessage.Destination);

		_ = deserializedMessage.MessageHeaders.ShouldNotBeNull();
		deserializedMessage.MessageHeaders!.Count.ShouldBe(1);
		deserializedMessage.MessageHeaders["roundtrip-header"].ShouldBe("roundtrip-value");

		_ = deserializedMessage.MessageBody.ShouldNotBeNull();
		_ = deserializedMessage.MessageBody.ShouldBeOfType<TestDomainEvent>();

		var eventBody = deserializedMessage.MessageBody as TestDomainEvent;
		eventBody!.Id.ShouldBe(42);
		eventBody.Name.ShouldBe("Roundtrip Event");
	}

	private sealed class TestDomainEvent : IDomainEvent
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
	}
}
