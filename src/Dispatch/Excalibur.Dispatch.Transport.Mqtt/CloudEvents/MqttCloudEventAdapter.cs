// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MQTTnet;

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// MQTT implementation of <see cref="ICloudEventEncoder{TOutbound}" /> that supports both structured
/// and binary CloudEvents encodings, per the CNCF CloudEvents MQTT protocol binding.
/// </summary>
/// <remarks>
/// Binary mode carries CloudEvent attributes as MQTT v5 user properties named EXACTLY as the attributes are,
/// with no prefix: the MQTT binding requires it (v1.0.2 3.1.3.1, "CloudEvents attribute names MUST be used
/// unchanged in each mapped User Property"). This differs from the sibling adapters on purpose -- the prefix
/// is assigned per protocol binding, so HTTP uses ce-, Kafka ce_, and MQTT none. Mirroring the siblings here
/// would make our events unreadable to conformant consumers and theirs unreadable to us. Structured mode
/// carries the whole CloudEvent as a single JSON payload with <c>ContentType</c> set to
/// <c>application/cloudevents+json</c>.
/// </remarks>
internal sealed class MqttCloudEventAdapter : ICloudEventEncoder<MqttApplicationMessage>
{
	private const string CloudEventsStructuredContentType = "application/cloudevents+json";

	private const string CeSpecVersionAttribute = "specversion";
	private const string CeTypeAttribute = "type";
	private const string CeSourceAttribute = "source";
	private const string CeIdAttribute = "id";
	private const string CeTimeAttribute = "time";
	private const string CeDataContentTypeAttribute = "datacontenttype";
	private const string CeSubjectAttribute = "subject";
	private const string CeDataSchemaAttribute = "dataschema";

	private const string TraceParentAttribute = "traceparent";

	private readonly JsonEventFormatter _jsonFormatter = new();
	private readonly ILogger<MqttCloudEventAdapter> _logger;
	private readonly MqttCloudEventOptions _mqttOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="MqttCloudEventAdapter" /> class.
	/// </summary>
	/// <param name="options"> The CloudEvents configuration options. </param>
	/// <param name="logger"> Logger used for diagnostics. </param>
	/// <param name="mqttOptions"> Provider specific CloudEvent options. </param>
	public MqttCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		ILogger<MqttCloudEventAdapter> logger,
		IOptions<MqttCloudEventOptions>? mqttOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger;
		_mqttOptions = mqttOptions?.Value ?? new MqttCloudEventOptions();
	}

	/// <inheritdoc />
	public CloudEventOptions Options { get; }

	private string DispatchPrefix => string.IsNullOrWhiteSpace(Options.DispatchExtensionPrefix)
		? "dispatch-"
		: Options.DispatchExtensionPrefix.EndsWith('-')
			? Options.DispatchExtensionPrefix
			: Options.DispatchExtensionPrefix + "-";

	/// <summary>
	/// Attempts to detect the CloudEvents mode from an MQTT application message.
	/// </summary>
	public static CloudEventMode? DetectMode(MqttApplicationMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (IsStructuredMode(message))
		{
			return CloudEventMode.Structured;
		}

		return IsBinaryMode(message) ? CloudEventMode.Binary : null;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public Task<MqttApplicationMessage> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var userProperties = new List<MQTTnet.Packets.MqttUserProperty>();
		ApplyCloudEventUserProperties(userProperties, cloudEvent);
		ApplyDispatchEnvelopeUserProperties(userProperties, cloudEvent);

		// Built via direct construction, not MqttApplicationMessageBuilder -- the builder validates
		// Topic is non-empty at Build(), but the mapper does not know the destination topic (that is
		// the sender's configuration, applied after this call, mirroring how the AWS/Azure/Google
		// adapters leave QueueUrl/entity name empty for their message bus to fill in).
		var message = new MqttApplicationMessage
		{
			Topic = string.Empty,
			Retain = _mqttOptions.Retain,
			UserProperties = userProperties,
		};

		switch (mode)
		{
			case CloudEventMode.Structured:
				var payload = _jsonFormatter.EncodeStructuredModeMessage(cloudEvent, out _);
				message.Payload = new ReadOnlySequence<byte>(payload);
				message.ContentType = CloudEventsStructuredContentType;
				break;

			case CloudEventMode.Binary:
				message.Payload = new ReadOnlySequence<byte>(EncodeBinaryBody(cloudEvent));
				if (!string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
				{
					message.ContentType = cloudEvent.DataContentType;
				}

				break;

			default:
				throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for MQTT.");
		}

		_logger.LogDebug(
			"Converted CloudEvent {EventId} to MQTT application message using {Mode} mode",
			cloudEvent.Id,
			mode);

		return Task.FromResult(message);
	}

	/// <inheritdoc />
	public Task<CloudEvent> FromTransportMessageAsync(
		MqttApplicationMessage transportMessage,
		CancellationToken cancellationToken) =>
		FromTransportMessageCoreAsync(transportMessage, cancellationToken);


	private async Task<CloudEvent> FromTransportMessageCoreAsync(
		MqttApplicationMessage transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		var mode = DetectMode(transportMessage) ?? Options.DefaultMode;

		var cloudEvent = mode switch
		{
			CloudEventMode.Structured => await ParseStructuredModeAsync(transportMessage).ConfigureAwait(false),
			CloudEventMode.Binary => ParseBinaryMode(transportMessage),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for MQTT."),
		};

		EnrichFromUserProperties(cloudEvent, transportMessage);

		_logger.LogDebug(
			"Converted MQTT application message to CloudEvent {EventId} using {Mode} mode",
			cloudEvent.Id,
			mode);

		return cloudEvent;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static byte[] EncodeBinaryBody(CloudEvent cloudEvent) => cloudEvent.Data switch
	{
		null => [],
		byte[] binary => binary,
		ReadOnlyMemory<byte> rom => rom.ToArray(),
		string text => Encoding.UTF8.GetBytes(text),
		_ => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cloudEvent.Data)),
	};

	private static object DeserializeMessageBody(byte[] body, string? contentType)
	{
		if (body.Length == 0)
		{
			return string.Empty;
		}

		if (IsJsonContentType(contentType))
		{
			return JsonDocument.Parse(body).RootElement;
		}

		return IsContentType(contentType, "application/x-base64")
			? body
			: Encoding.UTF8.GetString(body);
	}

	// Mirrors CloudEventContentType.Is/IsJson (Transport.Abstractions, internal to that assembly) --
	// bare-media-type comparison ignoring RFC 9110 parameters (e.g. "; charset=utf-8").
	private static bool IsContentType(string? contentType, string mediaType) =>
		string.Equals(BareMediaType(contentType), mediaType, StringComparison.OrdinalIgnoreCase);

	private static bool IsJsonContentType(string? contentType) =>
		BareMediaType(contentType)?.EndsWith("json", StringComparison.OrdinalIgnoreCase) == true;

	private static string? BareMediaType(string? contentType)
	{
		if (string.IsNullOrWhiteSpace(contentType))
		{
			return null;
		}

		var separator = contentType.IndexOf(';', StringComparison.Ordinal);
		var bare = (separator < 0 ? contentType : contentType[..separator]).Trim();
		return bare.Length == 0 ? null : bare;
	}

	private static bool IsStructuredMode(MqttApplicationMessage message)
	{
		if (message.ContentType?.Contains("cloudevents", StringComparison.OrdinalIgnoreCase) == true)
		{
			return true;
		}

		var payload = message.Payload.ToArray();
		if (payload.Length == 0)
		{
			return false;
		}

		try
		{
			using var document = JsonDocument.Parse(payload);
			var root = document.RootElement;

			return root.TryGetProperty("specversion", out _) &&
				   root.TryGetProperty("type", out _) &&
				   root.TryGetProperty("source", out _) &&
				   root.TryGetProperty("id", out _);
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static bool IsBinaryMode(MqttApplicationMessage message) =>
		HasUserProperty(message, CeSpecVersionAttribute) &&
		HasUserProperty(message, CeTypeAttribute) &&
		HasUserProperty(message, CeSourceAttribute) &&
		HasUserProperty(message, CeIdAttribute);

	private static bool HasUserProperty(MqttApplicationMessage message, string name) =>
		message.UserProperties?.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) == true;

	private static string? GetUserProperty(MqttApplicationMessage message, string name) =>
		message.UserProperties?.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

	private static bool IsRequiredCloudEventAttribute(string attributeName) =>
		attributeName.Equals(CeSpecVersionAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTypeAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeSourceAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeIdAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTimeAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeDataContentTypeAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeSubjectAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeDataSchemaAttribute, StringComparison.OrdinalIgnoreCase);

	private void ApplyCloudEventUserProperties(List<MQTTnet.Packets.MqttUserProperty> properties, CloudEvent cloudEvent)
	{
		var specVersion = cloudEvent.SpecVersion?.VersionId ?? Options.SpecVersion.VersionId;
		properties.Add(new MQTTnet.Packets.MqttUserProperty(CeSpecVersionAttribute, specVersion));

		if (!string.IsNullOrWhiteSpace(cloudEvent.Type))
		{
			properties.Add(new MQTTnet.Packets.MqttUserProperty(CeTypeAttribute, cloudEvent.Type));
		}

		if (cloudEvent.Source is not null)
		{
			properties.Add(new MQTTnet.Packets.MqttUserProperty(CeSourceAttribute, cloudEvent.Source.ToString()));
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Id))
		{
			properties.Add(new MQTTnet.Packets.MqttUserProperty(CeIdAttribute, cloudEvent.Id));
		}

		if (cloudEvent.Time.HasValue)
		{
			properties.Add(new MQTTnet.Packets.MqttUserProperty(CeTimeAttribute, cloudEvent.Time.Value.ToString("O")));
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
		{
			properties.Add(new MQTTnet.Packets.MqttUserProperty(CeDataContentTypeAttribute, cloudEvent.DataContentType));
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			properties.Add(new MQTTnet.Packets.MqttUserProperty(CeSubjectAttribute, cloudEvent.Subject));
		}

		if (cloudEvent.DataSchema is not null)
		{
			properties.Add(new MQTTnet.Packets.MqttUserProperty(CeDataSchemaAttribute, cloudEvent.DataSchema.ToString()));
		}

		var traceParent = cloudEvent[TraceParentAttribute] as string;
		if (!string.IsNullOrWhiteSpace(traceParent))
		{
			properties.Add(new MQTTnet.Packets.MqttUserProperty(TraceParentAttribute, traceParent));
		}

		foreach (var extension in cloudEvent.ExtensionAttributes)
		{
			var value = cloudEvent[extension.Name];
			if (value is null)
			{
				continue;
			}

			var attributeName = extension.Name;
			if (IsRequiredCloudEventAttribute(attributeName))
			{
				continue;
			}

			properties.Add(new MQTTnet.Packets.MqttUserProperty(attributeName, value.ToString()!));
		}
	}

	private void ApplyDispatchEnvelopeUserProperties(List<MQTTnet.Packets.MqttUserProperty> properties, CloudEvent cloudEvent)
	{
		AddDispatchUserProperty(properties, cloudEvent, "correlationid");
		AddDispatchUserProperty(properties, cloudEvent, "tenantid");
		AddDispatchUserProperty(properties, cloudEvent, "userid");
		AddDispatchUserProperty(properties, cloudEvent, TraceParentAttribute);
		AddDispatchUserProperty(properties, cloudEvent, "deliverycount");
	}

	private void AddDispatchUserProperty(List<MQTTnet.Packets.MqttUserProperty> properties, CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[DispatchPrefix + attributeName] ?? cloudEvent[attributeName];
		if (value is null)
		{
			return;
		}

		properties.Add(new MQTTnet.Packets.MqttUserProperty(DispatchPrefix + attributeName, value.ToString()!));
	}

	private async Task<CloudEvent> ParseStructuredModeAsync(MqttApplicationMessage message)
	{
		var payload = message.Payload.ToArray();
		if (payload.Length == 0)
		{
			throw new InvalidOperationException("Structured CloudEvent MQTT payload is empty.");
		}

		await using var stream = new MemoryStream(payload);
		return await _jsonFormatter.DecodeStructuredModeMessageAsync(
				stream,
				new ContentType("application/json"),
				extensionAttributes: null)
			.ConfigureAwait(false);
	}

	private CloudEvent ParseBinaryMode(MqttApplicationMessage message)
	{
		var type = GetUserProperty(message, CeTypeAttribute);
		var source = GetUserProperty(message, CeSourceAttribute);
		var id = GetUserProperty(message, CeIdAttribute);

		if (type is null || source is null || id is null)
		{
			throw new InvalidOperationException("MQTT message is missing required CloudEvent user properties.");
		}

		var cloudEvent = new CloudEvent(Options.SpecVersion)
		{
			Type = type,
			Id = id,
			Source = Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out var sourceUri) ? sourceUri : Options.DefaultSource,
		};

		var time = GetUserProperty(message, CeTimeAttribute);
		if (time is not null &&
			DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
		{
			cloudEvent.Time = timestamp;
		}

		var dataContentType = GetUserProperty(message, CeDataContentTypeAttribute);
		if (dataContentType is not null)
		{
			cloudEvent.DataContentType = dataContentType;
		}

		var subject = GetUserProperty(message, CeSubjectAttribute);
		if (subject is not null)
		{
			cloudEvent.Subject = subject;
		}

		var dataSchema = GetUserProperty(message, CeDataSchemaAttribute);
		if (dataSchema is not null && Uri.TryCreate(dataSchema, UriKind.RelativeOrAbsolute, out var schemaUri))
		{
			cloudEvent.DataSchema = schemaUri;
		}

		var payload = message.Payload.ToArray();
		if (payload.Length > 0)
		{
			cloudEvent.Data = DeserializeMessageBody(payload, cloudEvent.DataContentType);
		}

		return cloudEvent;
	}

	private void EnrichFromUserProperties(CloudEvent cloudEvent, MqttApplicationMessage message)
	{
		if (message.UserProperties is null)
		{
			return;
		}

		foreach (var property in message.UserProperties)
		{
			if (property.Name.StartsWith(DispatchPrefix, StringComparison.OrdinalIgnoreCase))
			{
				var extensionName = property.Name[DispatchPrefix.Length..];
				if (!string.IsNullOrEmpty(property.Value))
				{
					cloudEvent[extensionName] = property.Value;
				}

				continue;
			}

			// No prefix to filter on: the MQTT binding puts attribute names in User Properties unchanged, so a
			// property carrying an extension is indistinguishable from any other property the producer set.
			// That is the binding's design, not an omission here -- discrimination by name is only available
			// for the core attributes, which are read by name above. Everything else is taken as an extension,
			// except the two names this adapter already owns for other purposes.
			if (IsRequiredCloudEventAttribute(property.Name) ||
				property.Name.Equals(TraceParentAttribute, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			cloudEvent[property.Name] ??= property.Value;
		}
	}
}
