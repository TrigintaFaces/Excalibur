// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// JSON converter for TimeSpan using ISO 8601 duration format.
/// </summary>
internal sealed class TimeSpanConverter : JsonConverter<TimeSpan>
{
	/// <inheritdoc/>
	public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
		{
			var str = reader.GetString();
			if (string.IsNullOrEmpty(str))
			{
				return TimeSpan.Zero;
			}

			// Try ISO 8601 duration format (PT1H30M)
			if (str.StartsWith('P') || str.StartsWith("PT", StringComparison.Ordinal))
			{
				return XmlConvert.ToTimeSpan(str);
			}

			// Try standard .NET format
			return TimeSpan.Parse(str);
		}

		if (reader.TokenType == JsonTokenType.Number)
		{
			// Assume milliseconds
			return TimeSpan.FromMilliseconds(reader.GetDouble());
		}

		throw new JsonException($"Unable to convert {reader.TokenType} to TimeSpan");
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>

		// Write as ISO 8601 duration
		writer.WriteStringValue(XmlConvert.ToString(value));
}
