// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Decodes a <see cref="CloudEvent"/> from a <see cref="TransportReceivedMessage"/>, the transport-neutral
/// shape every <see cref="ITransportReceiver"/> normalizes into.
/// </summary>
/// <remarks>
/// <para>
/// This is the single decoder the <see cref="ICloudEventDecoder{TInbound}"/> contract calls for: one
/// implementation over the neutral inbound type rather than one per provider. It is possible because
/// <see cref="ITransportReceiver"/> is <b>defined</b> in terms of <see cref="TransportReceivedMessage"/> —
/// normalization is the contract, not a convention — so every provider's message reaches this decoder in
/// the same shape.
/// </para>
/// <para>
/// <b>The attribute names are NOT uniform across transports, and that is the one thing that could make a
/// single decoder silently wrong.</b> Binary-mode CloudEvents attributes arrive in
/// <see cref="TransportReceivedMessage.Properties"/> under several different conventions, because each
/// transport's own wire format dictates the spelling: bare (<c>type</c>), hyphen-prefixed
/// (<c>ce-type</c>), short-underscore (<c>ce_type</c>, which the CloudEvents Kafka binding assigns
/// because a Kafka header key cannot carry a hyphen), long-underscore (<c>cloudEvents_type</c>,
/// which the AMQP binding assigns and prefers because an underscore is usable in a JMS selector) and
/// long-colon (<c>cloudEvents:type</c>, which the same AMQP binding also permits and which earlier
/// revisions of it permitted <i>exclusively</i> — so a conformant AMQP producer emits it and a decoder
/// that probed only the underscore would silently fail to read half that binding's traffic).
/// A decoder that recognised only one convention would decode some transports
/// and hand the rest through undecoded — the failure would be silent, per-transport, and would look
/// exactly like a message that simply carried no CloudEvent. Every convention listed below is therefore probed for every
/// attribute.
/// </para>
/// <para>
/// <b>Total by contract.</b> Every inbound message has a defined outcome: <see langword="null"/> when the
/// message carries no marker (the caller passes it through untouched), a value when it decodes, and a
/// thrown exception when a marker is present but the message is malformed. Returning
/// <see langword="null"/> for a malformed CloudEvent would silently downgrade a corrupt message to an
/// ordinary one.
/// </para>
/// <para>
/// <b>The marker is <c>specversion</c>, and nothing else.</b> It is required in every mode, so a
/// conformant producer always sends it, and its name is specific enough not to collide with ordinary
/// message metadata. That matters because the bare, unprefixed spelling above is also probed: attributes
/// such as <c>id</c>, <c>type</c> and <c>source</c> are ordinary words that routine traffic carries for
/// its own reasons, so treating them as the marker would refuse plain messages as malformed CloudEvents.
/// A new required attribute must not be added to the detection test for the same reason.
/// </para>
/// </remarks>
internal sealed class TransportReceivedMessageCloudEventDecoder(CloudEventBinding binding)
	: ICloudEventDecoder<TransportReceivedMessage>
{
	// How THIS TRANSPORT'S binding names attributes, and nothing else. A universal list would make every
	// transport honour every other binding's spelling: an AMQP attribute name recognised on MQTT, a bare
	// name recognised everywhere.
	private readonly CloudEventBinding _binding = binding ?? throw new ArgumentNullException(nameof(binding));

	/// <summary>The media type identifying a structured-mode CloudEvent, per the CloudEvents spec.</summary>
	private const string StructuredModeMediaType = "application/cloudevents+json";

	/// <inheritdoc />
	public Task<CloudEvent?> TryDecodeAsync(
		TransportReceivedMessage transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		if (IsStructuredMode(transportMessage))
		{
			return Task.FromResult<CloudEvent?>(DecodeStructured(transportMessage));
		}

		// Binary mode is detected on specversion ALONE, and the choice of attribute is the whole safety
		// property. The spec makes specversion required in every mode, so a conformant producer always
		// sends it; it is also the one CloudEvents attribute whose name does not collide with ordinary
		// message metadata. Detecting on id/type/source instead would misread routine traffic, because
		// those names are probed unprefixed too — a plain message carrying a property called "id" would
		// be read as a partially-attributed CloudEvent and refused, turning a normal send into an error
		// on every transport.
		var specVersion = FindAttribute(transportMessage, "specversion");

		if (specVersion is null)
		{
			// Not a CloudEvent. The caller keeps the original message untouched.
			return Task.FromResult<CloudEvent?>(null);
		}

		var id = FindAttribute(transportMessage, "id");
		var type = FindAttribute(transportMessage, "type");
		var source = FindAttribute(transportMessage, "source");

		if (id is null || type is null || source is null)
		{
			throw new InvalidOperationException(
				"The received message declares a CloudEvents specversion but is missing at least one of the "
				+ "required id, type and source attributes. A partially-attributed message is malformed "
				+ "rather than absent, so it is refused instead of being passed through as an ordinary "
				+ "message.");
		}

		return Task.FromResult<CloudEvent?>(DecodeBinary(transportMessage, specVersion, id, type, source));
	}

	private static bool IsStructuredMode(TransportReceivedMessage message) =>
		message.ContentType?.StartsWith(StructuredModeMediaType, StringComparison.OrdinalIgnoreCase) == true;

	/// <summary>
	/// Decodes a structured-mode CloudEvent from the message body.
	/// </summary>
	/// <remarks>
	/// Parsed with <see cref="JsonDocument"/> rather than a reflection-based formatter, and the reason is
	/// structural rather than stylistic: this decoder runs inside a decorator over
	/// <see cref="ITransportReceiver"/>, so anything it requires is inherited by the core transport
	/// contract. A reflection-based decode would force <c>RequiresUnreferencedCode</c> onto
	/// <see cref="ITransportReceiver.ReceiveAsync"/> itself and AOT-annotate every receive call in the
	/// framework, including the ones that never touch CloudEvents. The envelope is a flat set of string
	/// attributes, so it needs no reflection; the payload is carried through as bytes and left for the
	/// consumer to interpret.
	/// </remarks>
	private static CloudEvent DecodeStructured(TransportReceivedMessage message)
	{
		if (message.Body.IsEmpty)
		{
			throw new InvalidOperationException(
				"The received message declares the structured CloudEvents content type but carries an empty "
				+ "body, so there is nothing to decode.");
		}

		using var document = JsonDocument.Parse(message.Body);
		var root = document.RootElement;

		if (root.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidOperationException(
				"The received message declares the structured CloudEvents content type but its body is not a "
				+ "JSON object.");
		}

		var id = ReadString(root, "id");
		var type = ReadString(root, "type");
		var source = ReadString(root, "source");

		if (id is null || type is null || source is null)
		{
			throw new InvalidOperationException(
				"The received message declares the structured CloudEvents content type but is missing at "
				+ "least one of the required id, type and source attributes.");
		}

		var specVersion = ReadString(root, "specversion");
		var cloudEvent = new CloudEvent(ResolveSpecVersion(specVersion))
		{
			Id = id,
			Type = type,
			Source = ToSourceUri(source),
			Data = ReadStructuredData(root),
		};

		ApplyOptionalAttributes(
			cloudEvent,
			ReadString(root, "datacontenttype"),
			ReadString(root, "subject"),
			ReadString(root, "dataschema"),
			ReadString(root, "time"));

		return cloudEvent;
	}

	private static string? ReadString(JsonElement root, string name) =>
		root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	/// <summary>
	/// Extracts the structured-mode payload, preferring the spec's base64 form for binary data.
	/// </summary>
	private static object? ReadStructuredData(JsonElement root)
	{
		if (root.TryGetProperty("data_base64", out var base64) && base64.ValueKind == JsonValueKind.String)
		{
			return base64.GetBytesFromBase64();
		}

		if (!root.TryGetProperty("data", out var data))
		{
			return null;
		}

		// Carried through as raw JSON bytes rather than a deserialized object: the decoder does not know
		// the consumer's payload type, and guessing one is what would require reflection.
		return data.ValueKind == JsonValueKind.String
			? data.GetString()
			: Encoding.UTF8.GetBytes(data.GetRawText());
	}

	private CloudEvent DecodeBinary(
		TransportReceivedMessage message,
		string specVersion,
		string id,
		string type,
		string source)
	{
		// The caller has already resolved specversion — it is what identified this message as a
		// CloudEvent at all. Taking it as a parameter rather than re-reading it keeps detection and
		// construction reading the same value, so the two can never disagree about which spelling won.
		var cloudEvent = new CloudEvent(ResolveSpecVersion(specVersion))
		{
			Id = id,
			Type = type,
			Source = ToSourceUri(source),
			Data = message.Body.IsEmpty ? null : message.Body.ToArray(),
		};

		ApplyOptionalAttributes(
			cloudEvent,
			FindAttribute(message, "datacontenttype"),
			FindAttribute(message, "subject"),
			FindAttribute(message, "dataschema"),
			FindAttribute(message, "time"));

		return cloudEvent;
	}

	/// <summary>
	/// Resolves the declared spec version, falling back to the default when absent or unrecognised.
	/// </summary>
	private static CloudEventsSpecVersion ResolveSpecVersion(string? specVersion) =>
		specVersion is null
			? CloudEventsSpecVersion.Default
			: CloudEventsSpecVersion.FromVersionId(specVersion) ?? CloudEventsSpecVersion.Default;

	/// <summary>
	/// Converts the source attribute to a URI, preserving it as a relative reference when it is not
	/// absolute — the CloudEvents spec permits either.
	/// </summary>
	private static Uri ToSourceUri(string source) =>
		Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out var uri) ? uri : new Uri(source, UriKind.Relative);

	/// <summary>
	/// Applies the optional attributes shared by both modes, skipping any the message does not carry.
	/// </summary>
	private static void ApplyOptionalAttributes(
		CloudEvent cloudEvent,
		string? dataContentType,
		string? subject,
		string? dataSchema,
		string? time)
	{
		if (dataContentType is not null)
		{
			cloudEvent.DataContentType = dataContentType;
		}

		if (subject is not null)
		{
			cloudEvent.Subject = subject;
		}

		if (dataSchema is not null && Uri.TryCreate(dataSchema, UriKind.RelativeOrAbsolute, out var schemaUri))
		{
			cloudEvent.DataSchema = schemaUri;
		}

		if (time is not null
			&& DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
		{
			cloudEvent.Time = timestamp;
		}
	}

	/// <summary>
	/// Finds a CloudEvents attribute under any of the prefixes a transport may have used.
	/// </summary>
	private string? FindAttribute(TransportReceivedMessage message, string attributeName)
	{
		switch (_binding.Match)
		{
			case CloudEventAttributeMatch.None:
				// No binding, so no binary mode. Structured decoding never reaches here.
				return null;

			case CloudEventAttributeMatch.Bare:
				return Read(message, attributeName);

			default:
				foreach (var prefix in _binding.AttributePrefixes)
				{
					if (Read(message, prefix + attributeName) is { } value)
					{
						return value;
					}
				}

				return null;
		}

		// Exact key lookup, never a scan of the property bag. That is what makes the bare-name binding
		// safe: it asks for "specversion", it does not ask which properties look like attributes. A
		// future change to prefix-scanning would make the bare binding match everything, so if this
		// becomes a search, the bare case needs its own guard first.
		static string? Read(TransportReceivedMessage message, string key) =>
			message.Properties.TryGetValue(key, out var value) && value is not null
				? value as string ?? value.ToString()
				: null;
	}
}
