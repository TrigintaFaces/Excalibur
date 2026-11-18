// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Encodes a <see cref="CloudEvent"/> into the transport-neutral <see cref="TransportMessage"/> every
/// <see cref="ITransportSender"/> accepts, using the CloudEvents structured JSON mode.
/// </summary>
/// <remarks>
/// <para>
/// This is the send counterpart of the shared inbound decoder, and it exists for the same structural
/// reason: <see cref="ITransportSender"/> is <b>defined</b> in terms of <see cref="TransportMessage"/>, so
/// one encoder over that type serves every transport rather than one adapter per provider SDK.
/// </para>
/// <para>
/// <b>Structured mode only, and that is a conformance decision rather than a shortcut.</b> Structured mode
/// carries the entire event as a JSON document in the body, identified by a single content type — which
/// makes that content type load-bearing, and is why the encoder is applied per transport rather than to
/// every sender. Binary mode places each
/// attribute in a separate metadata entry under a per-protocol prefix that the CloudEvents spec assigns
/// individually — bare for MQTT, <c>ce_</c> for Kafka, <c>cloudEvents_</c> for AMQP — so a single shared
/// encoder emitting one spelling would be non-conformant on most transports. Binary mode belongs in a
/// per-transport encoder that knows its own binding; this one deliberately does not guess.
/// </para>
/// <para>
/// <b>Serialized with <see cref="Utf8JsonWriter"/> rather than a reflection-based formatter, and the
/// reason is structural.</b> This encoder runs inside a decorator over <see cref="ITransportSender"/>, so
/// anything it requires is inherited by the core send contract: a reflection-based encode would force
/// <c>RequiresUnreferencedCode</c> onto <see cref="ITransportSender.SendAsync"/> itself and AOT-annotate
/// every send in the framework, including the ones that never touch CloudEvents. The structured envelope is
/// a flat set of typed context attributes with direct JSON representations, so it needs no reflection.
/// </para>
/// </remarks>
internal sealed class TransportMessageCloudEventEncoder
{
	/// <summary>The media type identifying a structured-mode CloudEvent, per the CloudEvents spec.</summary>
	internal const string StructuredModeMediaType = "application/cloudevents+json";

	/// <summary>
	/// Writes the event as a structured-mode CloudEvents JSON document.
	/// </summary>
	/// <param name="cloudEvent">The event to encode.</param>
	/// <returns>The UTF-8 JSON envelope.</returns>
	/// <exception cref="NotSupportedException">
	/// The event's <see cref="CloudEvent.Data"/> is not bytes, byte memory, a string or a <see cref="JsonElement"/>.
	/// </exception>
	public static ReadOnlyMemory<byte> Encode(CloudEvent cloudEvent)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		if (cloudEvent.Id is null || cloudEvent.Type is null || cloudEvent.Source is null)
		{
			throw new InvalidOperationException(
				"A CloudEvent must carry id, type and source before it can be encoded. Refusing to emit a "
				+ "partially-attributed event, which a conformant consumer would reject after it had already "
				+ "been published.");
		}

		if (cloudEvent.Data is JsonElement && !IsJsonDataContentType(cloudEvent.DataContentType))
		{
			throw new NotSupportedException("A JsonElement payload requires a JSON data content type or an unspecified data content type.");
		}

		var buffer = new ArrayBufferWriter<byte>();
		using (var writer = new Utf8JsonWriter(buffer))
		{
			writer.WriteStartObject();
			writer.WriteString("specversion", cloudEvent.SpecVersion.VersionId);
			WriteContextAttributes(writer, cloudEvent);
			if (cloudEvent.DataContentType is null && (cloudEvent.Data is string or JsonElement))
			{
				const string inferred = "application/json";
				var attribute = cloudEvent.SpecVersion.DataContentTypeAttribute;
				_ = attribute.Validate(inferred);
				writer.WriteString(attribute.Name, inferred);
			}

			WriteData(writer, cloudEvent.Data);

			writer.WriteEndObject();
		}

		return buffer.WrittenMemory;
	}

	/// <summary>Writes context values in the same public sequence as the pinned SDK formatter.</summary>
	private static void WriteContextAttributes(Utf8JsonWriter writer, CloudEvent cloudEvent)
	{
		foreach (var (attribute, value) in cloudEvent.GetPopulatedAttributes())
		{
			if (value is null)
			{
				continue;
			}

			if (attribute.Name == "data")
			{
				throw new InvalidOperationException(
					"A CloudEvents extension cannot use the reserved JSON payload member name 'data'.");
			}

			if (attribute.Type == CloudEventAttributeType.Boolean)
			{
				writer.WriteBoolean(attribute.Name, (bool)attribute.Validate(value));
			}
			else if (attribute.Type == CloudEventAttributeType.Integer)
			{
				writer.WriteNumber(attribute.Name, (int)attribute.Validate(value));
			}
			else if (attribute.Type == CloudEventAttributeType.Binary)
			{
				WriteBinaryString(writer, attribute.Name, (byte[])attribute.Validate(value));
			}
			else
			{
				writer.WriteString(attribute.Name, attribute.Format(value));
			}
		}
	}

	/// <summary>
	/// Writes the payload, preferring the spec's base64 member for binary data.
	/// </summary>
	/// <remarks>
	/// Bytes and byte memory remain binary, strings remain strings, and a <see cref="JsonElement"/>
	/// writes its JSON value directly without reflection. Structured JSON decoded by the shared receiver
	/// can therefore be forwarded without changing its payload into binary. Arbitrary CLR objects still
	/// require explicit consumer serialization; bytes are never guessed to be JSON from their contents
	/// or the event's data content type. A caller-supplied element must remain valid until encoding.
	/// </remarks>
	private static void WriteData(Utf8JsonWriter writer, object? data)
	{
		switch (data)
		{
			case null:
				return;

			case string text:
				writer.WriteString("data", text);
				return;

			case JsonElement element:
				writer.WritePropertyName("data");
				element.WriteTo(writer);
				return;

			case byte[] bytes:
				WriteBinaryString(writer, "data_base64", bytes);
				return;

			case ReadOnlyMemory<byte> memory:
				WriteBinaryString(writer, "data_base64", memory.Span);
				return;

			default:
				throw new NotSupportedException(
					$"A CloudEvent payload of type '{data.GetType()}' cannot be encoded for transport. "
					+ "Provide a JsonElement, string or bytes before publishing: encoding an arbitrary "
					+ "object requires reflection over its type, which would make every send in the framework "
					+ "unsafe for trimming and ahead-of-time compilation, including sends that carry no "
					+ "CloudEvent.");
		}
	}

	/// <summary>Writes base64 through normal JSON string escaping without a temporary UTF-16 string.</summary>
	private static void WriteBinaryString(Utf8JsonWriter writer, string name, ReadOnlySpan<byte> bytes)
	{
		const int stackBytes = 512;
		var encodedLength = System.Buffers.Text.Base64.GetMaxEncodedToUtf8Length(bytes.Length);
		byte[]? rented = null;
		var encoded = encodedLength <= stackBytes
			? stackalloc byte[stackBytes]
			: (rented = ArrayPool<byte>.Shared.Rent(encodedLength));
		try
		{
			var status = System.Buffers.Text.Base64.EncodeToUtf8(bytes, encoded,
				out var consumed, out var written, isFinalBlock: true);
			if (status != OperationStatus.Done || consumed != bytes.Length || written != encodedLength)
			{
				throw new InvalidOperationException("Complete base64 encoding was not produced.");
			}

			writer.WriteString(name, encoded[..written]);
		}
		finally
		{
			System.Security.Cryptography.CryptographicOperations.ZeroMemory(encoded[..encodedLength]);
			if (rented is not null)
			{
				ArrayPool<byte>.Shared.Return(rented);
			}
		}
	}

	private static bool IsJsonDataContentType(string? contentType)
	{
		if (contentType is null)
		{
			return true;
		}

		if (contentType.Length == 0)
		{
			return false;
		}

		var mediaType = new System.Net.Mime.ContentType(contentType).MediaType;
		return mediaType.EndsWith("/json", StringComparison.Ordinal)
			|| mediaType.EndsWith("+json", StringComparison.Ordinal);
	}
}
