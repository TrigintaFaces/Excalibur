// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

using Confluent.Kafka;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// JSON converter for Kafka Timestamp.
/// </summary>
internal sealed class KafkaTimestampConverter : JsonConverter<Timestamp>
{
	/// <inheritdoc/>
	public override Timestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
		{
			var unixMs = reader.GetInt64();
			return new Timestamp(DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime);
		}

		if (reader.TokenType == JsonTokenType.String)
		{
			var str = reader.GetString();
			if (long.TryParse(str, out var unixMs))
			{
				return new Timestamp(DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime);
			}

			var dateTime = DateTimeOffset.Parse(str, System.Globalization.CultureInfo.InvariantCulture);
			return new Timestamp(dateTime.UtcDateTime);
		}

		throw new JsonException($"Unable to convert {reader.TokenType} to Timestamp");
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, Timestamp value, JsonSerializerOptions options) =>
		writer.WriteNumberValue(value.UnixTimestampMs);
}
