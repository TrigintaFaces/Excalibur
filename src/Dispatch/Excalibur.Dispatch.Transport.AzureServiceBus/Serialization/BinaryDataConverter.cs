// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// JSON converter for Azure BinaryData.
/// </summary>
internal sealed class BinaryDataConverter : JsonConverter<BinaryData>
{
	/// <inheritdoc/>
	public override BinaryData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return null;
		}

		if (reader.TokenType == JsonTokenType.String)
		{
			var base64 = reader.GetString();
			if (string.IsNullOrEmpty(base64))
			{
				return BinaryData.Empty;
			}

			var bytes = Convert.FromBase64String(base64);
			return BinaryData.FromBytes(bytes);
		}

		// Handle JSON object/array as BinaryData
		using var doc = JsonDocument.ParseValue(ref reader);

		var json = doc.RootElement.GetRawText();
		return BinaryData.FromString(json);
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, BinaryData value, JsonSerializerOptions options)
	{
		if (value == null)
		{
			writer.WriteNullValue();
		}
		else
		{
			// Try to write as JSON if possible
			try
			{
				using var doc = JsonDocument.Parse(value.ToString());

				doc.WriteTo(writer);
			}
			catch
			{
				// Fall back to base64 for binary data
				writer.WriteStringValue(Convert.ToBase64String(value.ToArray()));
			}
		}
	}
}
