using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Data.Outbox.Serialization;

/// <summary>
///     A custom JSON converter for serializing and deserializing <see cref="OutboxMessage" /> objects.
/// </summary>
public class OutboxMessageJsonConverter : JsonConverter<OutboxMessage>
{
	/// <inheritdoc />
	public override bool CanConvert(Type typeToConvert) => typeof(OutboxMessage).IsAssignableFrom(typeToConvert);

	/// <inheritdoc />
	public override OutboxMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		Ensure(reader, JsonTokenType.StartObject);

		var outboxMessage = new OutboxMessage();

		var messageBodyType = typeof(object);

		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			Ensure(reader, JsonTokenType.PropertyName);
			var propertyName = reader.GetString();
			_ = reader.Read();

			switch (propertyName)
			{
				case "$messageBodyType":
					var typeName = reader.GetString();
					messageBodyType = Type.GetType(typeName) ?? throw new JsonException($"Could not load type {typeName}");
					break;

				case nameof(outboxMessage.MessageHeaders):
					outboxMessage.MessageHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options);
					break;

				case nameof(outboxMessage.MessageId):
					outboxMessage.MessageId = reader.GetString();
					break;

				case nameof(outboxMessage.MessageBody):
					outboxMessage.MessageBody = JsonSerializer.Deserialize(ref reader, messageBodyType, options);
					break;

				case nameof(outboxMessage.Destination):
					outboxMessage.Destination = reader.GetString();
					break;

				default:
					throw new JsonException($"Unexpected property name {propertyName}");
			}
		}

		return outboxMessage;
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, OutboxMessage value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(value);

		writer.WriteStartObject();

		writer.WritePropertyName("$messageBodyType");

		if (value.MessageBody is null)
		{
			throw new JsonException("MessageBody cannot be null during serialization.");
		}

		var messageBodyType = value.MessageBody.GetType();
		var fullNameAndAssembly = $"{messageBodyType.FullName}, {messageBodyType.Assembly.GetName().Name}";
		writer.WriteStringValue(fullNameAndAssembly);

		writer.WriteString(nameof(value.MessageId), value.MessageId);
		writer.WriteString(nameof(value.Destination), value.Destination);

		writer.WritePropertyName(nameof(value.MessageHeaders));
		JsonSerializer.Serialize(writer, value.MessageHeaders, options);

		writer.WritePropertyName(nameof(value.MessageBody));
		JsonSerializer.Serialize(writer, value.MessageBody, messageBodyType, options);

		writer.WriteEndObject();
	}

	/// <summary>
	///     Ensures the reader's current token type matches the expected token type.
	/// </summary>
	/// <param name="reader"> The JSON reader to validate. </param>
	/// <param name="expected"> The expected token type. </param>
	/// <exception cref="JsonException"> Thrown when the token type does not match the expected type. </exception>
	private static void Ensure(Utf8JsonReader reader, JsonTokenType expected)
	{
		if (reader.TokenType != expected)
		{
			throw new JsonException(
				$"Expected {Enum.GetName(typeof(JsonTokenType), expected)}, but found {reader.TokenType} at {reader.Position}.");
		}
	}
}
