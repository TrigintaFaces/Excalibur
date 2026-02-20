// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// JSON converter for AMQP timestamp.
/// </summary>
internal sealed class AmqpTimestampConverter : JsonConverter<AmqpTimestamp>
{
	/// <inheritdoc/>
	public override AmqpTimestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
		{
			var unixTime = reader.GetInt64();
			return new AmqpTimestamp(unixTime);
		}

		if (reader.TokenType == JsonTokenType.String)
		{
			var str = reader.GetString();
			if (long.TryParse(str, out var unixTime))
			{
				return new AmqpTimestamp(unixTime);
			}

			var dateTime = DateTimeOffset.Parse(str, System.Globalization.CultureInfo.InvariantCulture);
			return new AmqpTimestamp(dateTime.ToUnixTimeSeconds());
		}

		throw new JsonException($"Unable to convert {reader.TokenType} to AmqpTimestamp");
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, AmqpTimestamp value, JsonSerializerOptions options) =>
		writer.WriteNumberValue(value.UnixTime);
}
