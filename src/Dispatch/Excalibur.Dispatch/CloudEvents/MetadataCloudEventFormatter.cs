// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// Copyright (c) Cloud Native Foundation.
// Envelope/decoder portions adapted from the Apache-2.0 CloudEvents C# SDK v2.8.0,
// commit 54cbd3ca8576ba92fe441eb86e0c85f0e67d1eb7. Those portions retain Apache-2.0
// terms; the repository SPDX header applies to Excalibur contributions only.
// Modifications: direct abstract formatter boundary, explicit metadata payloads,
// split sync/async IO with preserved SDK2.8 ownership, and owned internal buffers.
// The per-method adaptation provenance ships in THIRD-PARTY-NOTICES.md under "License notice for
// CloudEvents C# SDK v2.8.0"; the verbatim licence is licenses/LICENSE-APACHE-2.0.txt. Apache-2.0
// is NOT one of the licences this software is offered under and cannot be elected -- see LICENSE.
using System.Globalization;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Core;

using CloudEventValidation = CloudNative.CloudEvents.Core.Validation;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Preserves SDK2.8 JSON formatting while resolving payload metadata explicitly,
/// without constructing its concrete JSON formatter on the metadata route.
/// </summary>
internal sealed class MetadataCloudEventFormatter : CloudEventFormatter
{
	private const string JsonMediaType = "application/json";
	private const string DataPropertyName = "data";
	private const string DataBase64PropertyName = "data_base64";
	private static readonly string StructuredMediaType = MimeUtilities.MediaType + "+json";
	private static readonly string BatchMediaType = MimeUtilities.BatchMediaType + "+json";
	private readonly JsonSerializerOptions _options;

	internal MetadataCloudEventFormatter(JsonSerializerOptions options)
	{
		_options = options;
	}

	private static JsonDocumentOptions DocumentOptions => default;

	/// <inheritdoc />
	public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
	{
		using var stream = BinaryDataUtilities.AsStream(body);
		return DecodeStructuredModeMessage(stream, contentType, extensionAttributes);
	}

	/// <inheritdoc />
	public override CloudEvent DecodeStructuredModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
	{
		var data = CloudEventValidation.CheckNotNull(body, "data");
		using var document = ReadDocument(data, contentType);
		return DecodeJsonElement(document.RootElement, extensionAttributes, nameof(data));
	}

	/// <inheritdoc />
	public override async Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
	{
		var data = CloudEventValidation.CheckNotNull(body, "data");
		using var document = await ReadDocumentAsync(data, contentType).ConfigureAwait(false);
		return DecodeJsonElement(document.RootElement, extensionAttributes, nameof(data));
	}

	/// <inheritdoc />
	public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
	{
		using var stream = BinaryDataUtilities.AsStream(body);
		return DecodeBatchModeMessage(stream, contentType, extensionAttributes);
	}

	/// <inheritdoc />
	public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
	{
		var data = CloudEventValidation.CheckNotNull(body, "data");
		using var document = ReadDocument(data, contentType);
		return DecodeBatchDocument(document, extensionAttributes, nameof(data));
	}

	/// <inheritdoc />
	public override async Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
	{
		var data = CloudEventValidation.CheckNotNull(body, "data");
		using var document = await ReadDocumentAsync(data, contentType).ConfigureAwait(false);
		return DecodeBatchDocument(document, extensionAttributes, nameof(data));
	}

	/// <inheritdoc />
	public override ReadOnlyMemory<byte> EncodeBatchModeMessage(IEnumerable<CloudEvent> cloudEvents, out ContentType contentType)
	{
		_ = CloudEventValidation.CheckNotNull(cloudEvents, nameof(cloudEvents));

		contentType = new ContentType(BatchMediaType)
		{
			CharSet = Encoding.UTF8.WebName
		};

		using var stream = new MemoryStream();
		using var writer = CreateUtf8JsonWriter(stream);
		writer.WriteStartArray();
		foreach (var cloudEvent in cloudEvents)
		{
			WriteCloudEventForBatchOrStructuredMode(writer, cloudEvent);
		}
		writer.WriteEndArray();
		writer.Flush();
		return stream.ToArray();
	}

	/// <inheritdoc />
	public override ReadOnlyMemory<byte> EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType)
	{
		contentType = new ContentType(StructuredMediaType)
		{
			CharSet = Encoding.UTF8.WebName
		};

		using var stream = new MemoryStream();
		using var writer = CreateUtf8JsonWriter(stream);
		WriteCloudEventForBatchOrStructuredMode(writer, cloudEvent);
		writer.Flush();
		return stream.ToArray();
	}

	/// <inheritdoc />
	public override ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent)
	{
		_ = CloudEventValidation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
		var data = cloudEvent.Data;
		if (data is null)
		{
			return Array.Empty<byte>();
		}

		if (data is byte[] bytes)
		{
			return bytes;
		}

		var contentType = new ContentType(cloudEvent.DataContentType ?? "application/json");
		if (IsJsonMediaType(contentType.MediaType))
		{
			var encoding = MimeUtilities.GetEncoding(contentType);
			return encoding is UTF8Encoding
				? JsonSerializer.SerializeToUtf8Bytes(data, jsonTypeInfo: GetPayloadTypeInfo())
				: encoding.GetBytes(JsonSerializer.Serialize(data, jsonTypeInfo: GetPayloadTypeInfo()));
		}

		if (contentType.MediaType.StartsWith("text/", StringComparison.Ordinal) && data is string text)
		{
			return MimeUtilities.GetEncoding(contentType).GetBytes(text);
		}

		throw UnsupportedData(cloudEvent, data);
	}

	/// <inheritdoc />
	public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent)
	{
		_ = CloudEventValidation.CheckNotNull(cloudEvent, nameof(cloudEvent));

		var contentType = new ContentType(cloudEvent.DataContentType ?? JsonMediaType);

		var encoding = MimeUtilities.GetEncoding(contentType);

		if (contentType.MediaType == JsonMediaType)
		{
			if (body.Length > 0)
			{
				using var document = encoding is UTF8Encoding
					? JsonDocument.Parse(body, DocumentOptions)
					: JsonDocument.Parse(BinaryDataUtilities.GetString(body, encoding), DocumentOptions);
				// We have to clone the data so that we can dispose of the JsonDocument.
				cloudEvent.Data = document.RootElement.Clone();
			}
			else
			{
				cloudEvent.Data = null;
			}
		}
		else if (IsTextMediaType(contentType.MediaType))
		{
			cloudEvent.Data = BinaryDataUtilities.GetString(body, encoding);
		}
		else
		{
			cloudEvent.Data = body.ToArray();
		}
	}

	/// <inheritdoc />
	protected override string? InferDataContentType(object data) => data is byte[] ? null : JsonMediaType;

	private static IReadOnlyList<CloudEvent> DecodeBatchDocument(JsonDocument document, IEnumerable<CloudEventAttribute>? extensionAttributes, string paramName)
	{
		var root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Array)
		{
			throw new ArgumentException($"Cannot decode JSON element of kind '{root.ValueKind}' as batch CloudEvent");
		}

		var events = new List<CloudEvent>(root.GetArrayLength());
		foreach (var element in root.EnumerateArray())
		{
			events.Add(DecodeJsonElement(element, extensionAttributes, paramName));
		}

		return events;
	}

	private static JsonDocument ReadDocument(Stream data, ContentType? contentType)
	{
		var encoding = MimeUtilities.GetEncoding(contentType);
		if (encoding is UTF8Encoding)
		{
			return JsonDocument.Parse(data, DocumentOptions);
		}

		// Match SDK2.8 BOM detection and non-UTF8 caller-stream ownership exactly.
		using var reader = new StreamReader(data, encoding);
		return JsonDocument.Parse(reader.ReadToEnd(), DocumentOptions);
	}

	private static async Task<JsonDocument> ReadDocumentAsync(Stream data, ContentType? contentType)
	{
		var encoding = MimeUtilities.GetEncoding(contentType);
		if (encoding is UTF8Encoding)
		{
			return await JsonDocument.ParseAsync(data, DocumentOptions).ConfigureAwait(false);
		}

		using var reader = new StreamReader(data, encoding);
		var json = await reader.ReadToEndAsync().ConfigureAwait(false);
		return JsonDocument.Parse(json, DocumentOptions);
	}

	private static CloudEvent DecodeJsonElement(JsonElement element, IEnumerable<CloudEventAttribute>? extensionAttributes, string paramName)
	{
		if (element.ValueKind != JsonValueKind.Object)
		{
			throw new ArgumentException($"Cannot decode JSON element of kind '{element.ValueKind}' as CloudEvent");
		}

		if (!element.TryGetProperty(CloudEventsSpecVersion.SpecVersionAttribute.Name, out var specVersionProperty)
			|| specVersionProperty.ValueKind != JsonValueKind.String)
		{
			throw new ArgumentException("Structured mode content does not represent a CloudEvent");
		}
		var specVersion = CloudEventsSpecVersion.FromVersionId(specVersionProperty.GetString())
			?? throw new ArgumentException($"Unsupported CloudEvents spec version '{specVersionProperty.GetString()}'");

		var cloudEvent = new CloudEvent(specVersion, extensionAttributes);
		PopulateAttributesFromStructuredEvent(cloudEvent, element);
		PopulateDataFromStructuredEvent(cloudEvent, element);
		return CloudEventValidation.CheckCloudEventArgument(cloudEvent, paramName);
	}

	private static void PopulateAttributesFromStructuredEvent(CloudEvent cloudEvent, JsonElement element)
	{
		foreach (var jsonProperty in element.EnumerateObject())
		{
			var name = jsonProperty.Name;
			var value = jsonProperty.Value;

			// Skip the spec version attribute, which we've already taken account of.
			// Data is handled later, when everything else (importantly, the data content type)
			// has been populated.
			if (name == CloudEventsSpecVersion.SpecVersionAttribute.Name ||
				name == DataBase64PropertyName ||
				name == DataPropertyName)
			{
				continue;
			}

			// For non-extension attributes, validate that the token type is as expected.
			// We're more forgiving for extension attributes: if an integer-typed extension attribute
			// has a value of "10" (i.e. as a string), that's fine. (If it has a value of "garbage",
			// that will throw in SetAttributeFromString.)
			ValidateTokenTypeForAttribute(cloudEvent.GetAttribute(name), value.ValueKind);

			// TODO: This currently performs more conversions than it really should, in the cause of simplicity.
			// We basically need a matrix of "attribute type vs token type" but that's rather complicated.

			var attributeValue = value.ValueKind switch
			{
				JsonValueKind.String => value.GetString(),
				JsonValueKind.True => CloudEventAttributeType.Boolean.Format(true),
				JsonValueKind.False => CloudEventAttributeType.Boolean.Format(false),
				JsonValueKind.Null => null,
				// Note: this will fail if the value isn't an integer, or is out of range for Int32.
				JsonValueKind.Number => CloudEventAttributeType.Integer.Format(value.GetInt32()),
				_ => throw new ArgumentException($"Invalid token type '{value.ValueKind}' for CloudEvent attribute")
			};
			if (attributeValue is null)
			{
				continue;
			}
			// Note: we *could* infer an extension type of integer and Boolean, but not other extension types.
			// (We don't want to assume that everything that looks like a timestamp is a timestamp, etc.)
			// Stick to strings for consistency.
			cloudEvent.SetAttributeFromString(name, attributeValue);
		}
	}

	private static void ValidateTokenTypeForAttribute(CloudEventAttribute? attribute, JsonValueKind valueKind)
	{
		// We can't validate unknown attributes, don't check for extension attributes,
		// and null values will be ignored anyway.
		if (attribute is null || attribute.IsExtension || valueKind == JsonValueKind.Null)
		{
			return;
		}

		// This is deliberately written so that if a new attribute type is added without this being updated, we "fail valid".
		// (That should only happen in major versions anyway, but it's worth being somewhat forgiving here.)
		var valid = CloudEventAttributeTypes.GetOrdinal(attribute.Type) switch
		{
			CloudEventAttributeTypeOrdinal.Binary => valueKind == JsonValueKind.String,
			CloudEventAttributeTypeOrdinal.Boolean => valueKind is JsonValueKind.True or JsonValueKind.False,
			CloudEventAttributeTypeOrdinal.Integer => valueKind == JsonValueKind.Number,
			CloudEventAttributeTypeOrdinal.String => valueKind == JsonValueKind.String,
			CloudEventAttributeTypeOrdinal.Timestamp => valueKind == JsonValueKind.String,
			CloudEventAttributeTypeOrdinal.Uri => valueKind == JsonValueKind.String,
			CloudEventAttributeTypeOrdinal.UriReference => valueKind == JsonValueKind.String,
			_ => true
		};
		if (!valid)
		{
			throw new ArgumentException($"Invalid token type '{valueKind}' for CloudEvent attribute '{attribute.Name}' with type '{attribute.Type}'");
		}
	}

	private static void PopulateDataFromStructuredEvent(CloudEvent cloudEvent, JsonElement element)
	{
		// Fetch data and data_base64 tokens, and treat null as missing.
		_ = element.TryGetProperty(DataPropertyName, out var dataElement);
		_ = element.TryGetProperty(DataBase64PropertyName, out var dataBase64Element);

		var dataPresent = dataElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
		var dataBase64Present = dataBase64Element.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

		// If we don't have any data, we're done.
		if (!dataPresent && !dataBase64Present)
		{
			return;
		}
		// We can't handle both properties being set.
		if (dataPresent && dataBase64Present)
		{
			throw new ArgumentException($"Structured mode content cannot contain both '{DataPropertyName}' and '{DataBase64PropertyName}' properties.");
		}
		// Okay, we have exactly one non-null data/data_base64 property.
		// Decode the one non-null data representation.
		if (dataBase64Present)
		{
			DecodeStructuredModeDataBase64Property(dataBase64Element, cloudEvent);
		}
		else
		{
			// If no content type has been specified, default to application/json
			cloudEvent.DataContentType ??= JsonMediaType;

			DecodeStructuredModeDataProperty(dataElement, cloudEvent);
		}
	}

	private static void DecodeStructuredModeDataBase64Property(JsonElement dataBase64Element, CloudEvent cloudEvent)
	{
		if (dataBase64Element.ValueKind != JsonValueKind.String)
		{
			throw new ArgumentException($"Structured mode property '{DataBase64PropertyName}' must be a string, when present.");
		}
		cloudEvent.Data = dataBase64Element.GetBytesFromBase64();
	}

	private static void DecodeStructuredModeDataProperty(JsonElement dataElement, CloudEvent cloudEvent)
	{
		if (IsJsonMediaType(new ContentType(cloudEvent.DataContentType!).MediaType))
		{
			cloudEvent.Data = dataElement.Clone();
		}
		else
		{
			if (dataElement.ValueKind != JsonValueKind.String)
			{
				throw new ArgumentException("CloudEvents with a non-JSON datacontenttype can only have string data values.");
			}
			cloudEvent.Data = dataElement.GetString();
		}
	}
	// Preserve the SDK2.8 parameterless EndsWith/StartsWith comparison semantics exactly.
	// These explicit BCL operations use CurrentCulture with no case folding, not SDK2.9 rules.
	private static bool IsJsonMediaType(string mediaType) =>
		CultureInfo.CurrentCulture.CompareInfo.IsSuffix(mediaType, "/json", CompareOptions.None)
		|| CultureInfo.CurrentCulture.CompareInfo.IsSuffix(mediaType, "+json", CompareOptions.None);

	private static bool IsTextMediaType(string mediaType) =>
		CultureInfo.CurrentCulture.CompareInfo.IsPrefix(mediaType, "text/", CompareOptions.None);

	private static ArgumentException UnsupportedData(CloudEvent cloudEvent, object data) => new(
		$"JsonEventFormatter cannot serialize data of type {data.GetType()} with content type '{cloudEvent.DataContentType}'");
	

	private Utf8JsonWriter CreateUtf8JsonWriter(Stream stream)
	{
		var options = new JsonWriterOptions
		{
			Encoder = _options.Encoder,
			Indented = _options.WriteIndented,
			// TODO: Consider skipping validation in the future for the sake of performance.
			SkipValidation = false
		};
		return new Utf8JsonWriter(stream, options);
	}

	private void WriteCloudEventForBatchOrStructuredMode(Utf8JsonWriter writer, CloudEvent cloudEvent)
	{
		_ = CloudEventValidation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));

		writer.WriteStartObject();
		writer.WritePropertyName(CloudEventsSpecVersion.SpecVersionAttribute.Name);
		writer.WriteStringValue(cloudEvent.SpecVersion.VersionId);
		var attributes = cloudEvent.GetPopulatedAttributes();
		foreach (var keyValuePair in attributes)
		{
			var attribute = keyValuePair.Key;
			var value = keyValuePair.Value;
			writer.WritePropertyName(attribute.Name);
			switch (CloudEventAttributeTypes.GetOrdinal(attribute.Type))
			{
				case CloudEventAttributeTypeOrdinal.Integer:
					writer.WriteNumberValue((int)value);
					break;
				case CloudEventAttributeTypeOrdinal.Boolean:
					writer.WriteBooleanValue((bool)value);
					break;
				default:
					writer.WriteStringValue(attribute.Type.Format(value));
					break;
			}
		}

		if (cloudEvent.Data is not null)
		{
			if (cloudEvent.DataContentType is null && GetOrInferDataContentType(cloudEvent) is string inferredDataContentType)
			{
				cloudEvent.SpecVersion.DataContentTypeAttribute.Validate(inferredDataContentType);
				writer.WritePropertyName(cloudEvent.SpecVersion.DataContentTypeAttribute.Name);
				writer.WriteStringValue(inferredDataContentType);
			}
			EncodeStructuredModeData(cloudEvent, writer);
		}
		writer.WriteEndObject();
	}

	private void EncodeStructuredModeData(CloudEvent cloudEvent, Utf8JsonWriter writer)
	{
		var data = cloudEvent.Data!;
		if (data is byte[] bytes)
		{
			writer.WritePropertyName(DataBase64PropertyName);
			writer.WriteStringValue(Convert.ToBase64String(bytes));
			return;
		}

		var contentType = new ContentType(cloudEvent.DataContentType ?? "application/json");
		if (IsJsonMediaType(contentType.MediaType))
		{
			writer.WritePropertyName(DataPropertyName);
			JsonSerializer.Serialize(writer, data, jsonTypeInfo: GetPayloadTypeInfo());
			return;
		}

		if (contentType.MediaType.StartsWith("text/", StringComparison.Ordinal) && data is string text)
		{
			writer.WritePropertyName(DataPropertyName);
			writer.WriteStringValue(text);
			return;
		}

		throw UnsupportedData(cloudEvent, data);
	}

	// Preserve object-root converters, covered ancestors and serialization-only Slim fallback.
	private JsonTypeInfo<object> GetPayloadTypeInfo() => (JsonTypeInfo<object>)_options.GetTypeInfo(typeof(object));
}
