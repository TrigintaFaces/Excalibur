// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Globalization;
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
/// a flat set of string attributes, so it needs no reflection.
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
	/// The event's <see cref="CloudEvent.Data"/> is neither bytes nor a string.
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

		var buffer = new ArrayBufferWriter<byte>();
		using (var writer = new Utf8JsonWriter(buffer))
		{
			writer.WriteStartObject();
			writer.WriteString("specversion", cloudEvent.SpecVersion.VersionId);
			writer.WriteString("id", cloudEvent.Id);
			writer.WriteString("type", cloudEvent.Type);
			writer.WriteString("source", cloudEvent.Source.ToString());

			WriteOptional(writer, "datacontenttype", cloudEvent.DataContentType);
			WriteOptional(writer, "subject", cloudEvent.Subject);
			WriteOptional(writer, "dataschema", cloudEvent.DataSchema?.ToString());

			if (cloudEvent.Time is { } time)
			{
				writer.WriteString("time", time.ToString("O", CultureInfo.InvariantCulture));
			}

			WriteExtensions(writer, cloudEvent);
			WriteData(writer, cloudEvent.Data);

			writer.WriteEndObject();
		}

		return buffer.WrittenMemory;
	}

	private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
	{
		if (value is not null)
		{
			writer.WriteString(name, value);
		}
	}

	/// <summary>
	/// Writes any extension attributes the event carries.
	/// </summary>
	/// <remarks>
	/// Enumerated through <see cref="CloudEvent.GetPopulatedAttributes"/>, which reports the attributes the
	/// event actually holds — no reflection over the payload type. The spec-defined attributes are written
	/// above from their typed accessors, so they are skipped here to avoid emitting a duplicate JSON member.
	/// </remarks>
	private static void WriteExtensions(Utf8JsonWriter writer, CloudEvent cloudEvent)
	{
		foreach (var (attribute, value) in cloudEvent.GetPopulatedAttributes())
		{
			if (attribute.IsRequired || IsSpecDefined(attribute.Name) || value is null)
			{
				continue;
			}

			writer.WriteString(attribute.Name, attribute.Format(value));
		}
	}

	private static bool IsSpecDefined(string name) =>
		name is "specversion" or "id" or "type" or "source"
			or "datacontenttype" or "subject" or "dataschema" or "time";

	/// <summary>
	/// Writes the payload, preferring the spec's base64 member for binary data.
	/// </summary>
	/// <remarks>
	/// Only bytes and strings are accepted. An arbitrary object would have to be serialized by reflecting
	/// over its type, which is exactly the requirement this encoder exists to keep off
	/// <see cref="ITransportSender"/> — so an unsupported payload is refused loudly here rather than
	/// silently annotating every send path in the framework. Callers serialize their payload first, which
	/// is also what the inbound decoder produces, so a decoded event re-encodes without conversion.
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

			case byte[] bytes:
				writer.WriteBase64String("data_base64", bytes);
				return;

			case ReadOnlyMemory<byte> memory:
				writer.WriteBase64String("data_base64", memory.Span);
				return;

			default:
				throw new NotSupportedException(
					$"A CloudEvent payload of type '{data.GetType()}' cannot be encoded for transport. "
					+ "Serialize the payload to a string or to bytes before publishing: encoding an arbitrary "
					+ "object requires reflection over its type, which would make every send in the framework "
					+ "unsafe for trimming and ahead-of-time compilation, including sends that carry no "
					+ "CloudEvent.");
		}
	}
}
