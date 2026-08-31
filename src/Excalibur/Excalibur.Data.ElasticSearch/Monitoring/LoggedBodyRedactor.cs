// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Rewrites a serialized request or response body so that only property values a consumer has explicitly declared safe
/// are emitted verbatim. Every other value is replaced, so a property this redactor has never heard of is redacted
/// rather than logged.
/// </summary>
internal static class LoggedBodyRedactor
{
	/// <summary> The placeholder written in place of a value that is not on the allow list. </summary>
	internal const string RedactedValue = "[Redacted]";

	/// <summary> The placeholder written in place of a body that could not be parsed, and so could not be redacted. </summary>
	internal const string UnparseableBody = "[Redacted: body was not valid JSON]";

	/// <summary>
	/// Redacts a serialized body, preserving its structure and property names while replacing every value that is not
	/// on the allow list.
	/// </summary>
	/// <param name="body"> The serialized body to redact. </param>
	/// <param name="allowedProperties"> The property names whose values may be emitted verbatim. </param>
	/// <returns>
	/// The redacted body, or <see cref="UnparseableBody" /> when <paramref name="body" /> is not well-formed JSON and
	/// therefore cannot be redacted structurally.
	/// </returns>
	public static string Redact(string body, IReadOnlySet<string> allowedProperties)
	{
		if (string.IsNullOrEmpty(body))
		{
			return string.Empty;
		}

		try
		{
			using var document = JsonDocument.Parse(body);

			var buffer = new ArrayBufferWriter<byte>();
			using (var writer = new Utf8JsonWriter(buffer))
			{
				WriteRedacted(document.RootElement, allowedProperties, writer);
			}

			return Encoding.UTF8.GetString(buffer.WrittenSpan);
		}
		catch (JsonException)
		{
			// The body cannot be parsed, so individual values cannot be located and masked. Withhold all of it.
			return UnparseableBody;
		}
	}

	/// <summary>
	/// Writes an element with every non-allow-listed value replaced.
	/// </summary>
	/// <param name="element"> The element to write. </param>
	/// <param name="allowedProperties"> The property names whose values may be emitted verbatim. </param>
	/// <param name="writer"> The writer receiving the redacted element. </param>
	private static void WriteRedacted(JsonElement element, IReadOnlySet<string> allowedProperties, Utf8JsonWriter writer)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				writer.WriteStartObject();
				foreach (var property in element.EnumerateObject())
				{
					WriteRedactedProperty(property, allowedProperties, writer);
				}

				writer.WriteEndObject();
				break;

			case JsonValueKind.Array:
				writer.WriteStartArray();
				foreach (var item in element.EnumerateArray())
				{
					WriteRedacted(item, allowedProperties, writer);
				}

				writer.WriteEndArray();
				break;

			default:
				// A value reached without an allow-listed property name governing it. Nothing here is known to be safe.
				writer.WriteStringValue(RedactedValue);
				break;
		}
	}

	/// <summary>
	/// Writes a single property, emitting its value verbatim only when its name is on the allow list.
	/// </summary>
	/// <param name="property"> The property to write. </param>
	/// <param name="allowedProperties"> The property names whose values may be emitted verbatim. </param>
	/// <param name="writer"> The writer receiving the redacted property. </param>
	private static void WriteRedactedProperty(
		JsonProperty property,
		IReadOnlySet<string> allowedProperties,
		Utf8JsonWriter writer)
	{
		if (allowedProperties.Contains(property.Name))
		{
			// The consumer named this property safe, so its value is emitted as-is, including any nested content.
			writer.WritePropertyName(property.Name);
			property.Value.WriteTo(writer);
			return;
		}

		writer.WritePropertyName(property.Name);
		WriteRedacted(property.Value, allowedProperties, writer);
	}
}
