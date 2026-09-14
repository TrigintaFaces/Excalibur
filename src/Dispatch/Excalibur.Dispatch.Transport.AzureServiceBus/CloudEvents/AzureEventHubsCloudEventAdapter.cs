// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Azure.Messaging.EventHubs;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Maps CloudEvents to <see cref="EventData" /> instances for Azure Event Hubs and vice versa./.
/// </summary>
internal sealed class AzureEventHubsCloudEventAdapter : IAzureEventHubsCloudEventAdapter
{
	private const string CloudEventsStructuredContentType = "application/cloudevents+json";

	// The AMQP binding requires "cloudEvents_" (or "cloudEvents:") on application properties, and a
	// single message MUST use one separator for every attribute -- so the extension path below derives
	// its prefix from this constant rather than repeating a literal that could drift from the required
	// attributes above. The strip length is taken from the constant for the same reason.
	private const string CloudEventsAttributePrefix = "cloudEvents_";
	private const string LegacyCloudEventsAttributePrefix = "ce-";

	private const string CeSpecVersionProperty = "cloudEvents_specversion";
	private const string CeTypeProperty = "cloudEvents_type";
	private const string CeSourceProperty = "cloudEvents_source";
	private const string CeIdProperty = "cloudEvents_id";
	private const string CeTimeProperty = "cloudEvents_time";
	private const string CeDataContentTypeProperty = "cloudEvents_datacontenttype";
	private const string CeSubjectProperty = "cloudEvents_subject";
	private const string CeDataSchemaProperty = "cloudEvents_dataschema";
	private const string CeTimeoutProperty = "cloudEvents_timeout";
	private const string DispatchPrefix = "dispatch-";
	private const string DispatchPrefixWithoutSeparator = "dispatch";
	private const string TimeoutAttributeName = "timeout";

	private readonly JsonEventFormatter _jsonFormatter = new();
	private readonly AzureEventHubsCloudEventOptions _eventHubsOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureEventHubsCloudEventAdapter" /> class.
	/// </summary>
	/// <param name="options"> CloudEvent serialization options. </param>
	/// <param name="eventHubsOptions"> Optional Event Hubs specific options. </param>
	public AzureEventHubsCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		IOptions<AzureEventHubsCloudEventOptions>? eventHubsOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_eventHubsOptions = eventHubsOptions?.Value ?? new AzureEventHubsCloudEventOptions();
	}

	/// <inheritdoc />
	public CloudEventOptions Options { get; }

	/// <inheritdoc />
	public static ValueTask<CloudEventMode?> TryDetectMode(
		EventData transportMessage,
		CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Method signature compatibility
		ArgumentNullException.ThrowIfNull(transportMessage);

		if (IsStructuredMode(transportMessage))
		{
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Structured);
		}

		if (IsBinaryMode(transportMessage))
		{
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Binary);
		}

		return ValueTask.FromResult<CloudEventMode?>(null);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public Task<EventData> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var eventData = mode switch
		{
			CloudEventMode.Structured => CreateStructuredMessage(cloudEvent),
			CloudEventMode.Binary => CreateBinaryMessage(cloudEvent),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for Event Hubs."),
		};

		ApplyStandardEventDataProperties(eventData, cloudEvent);
		ApplyDispatchEnvelopeExtensions(eventData.Properties, cloudEvent);
		ApplyExtensionAttributes(eventData.Properties, cloudEvent, mode == CloudEventMode.Binary);

		return Task.FromResult(eventData);
	}

	/// <inheritdoc />
	public async Task<CloudEvent> FromTransportMessageAsync(
		EventData transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		var mode = await TryDetectMode(transportMessage, cancellationToken).ConfigureAwait(false) ?? Options.DefaultMode;

		var cloudEvent = mode switch
		{
			CloudEventMode.Structured => await DecodeStructuredMessageAsync(transportMessage).ConfigureAwait(false),
			CloudEventMode.Binary => DecodeBinaryMessage(transportMessage),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for Event Hubs."),
		};

		MapEventDataPropertiesToCloudEvent(transportMessage, cloudEvent);
		RestoreDispatchEnvelopeProperties(cloudEvent, (IReadOnlyDictionary<string, object?>)transportMessage.Properties);
		RestoreExtensionAttributes(cloudEvent, (IReadOnlyDictionary<string, object?>)transportMessage.Properties);

		return cloudEvent;
	}

	[RequiresUnreferencedCode("Calls System.BinaryData.FromObjectAsJson<T>(T, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.BinaryData.FromObjectAsJson<T>(T, JsonSerializerOptions)")]
	private static BinaryData ConvertToBinaryData(object? data) => data switch
	{
		null => BinaryData.FromBytes([]),
		BinaryData binaryData => binaryData,
		byte[] bytes => BinaryData.FromBytes(bytes),
		string text => BinaryData.FromString(text),
		JsonElement json => BinaryData.FromString(json.GetRawText()),
		_ => BinaryData.FromObjectAsJson(data),
	};

	private static object? DeserializeMessageBody(BinaryData? body, string? contentType)
	{
		if (body?.ToMemory().IsEmpty != false)
		{
			return null;
		}

		return CloudEventContentType.IsJson(contentType)
			? JsonDocument.Parse(body).RootElement.Clone()
			: body.ToString();
	}

	private static bool IsStructuredMode(EventData message) =>
		!string.IsNullOrWhiteSpace(message.ContentType) &&
		message.ContentType.Contains("application/cloudevents", StringComparison.OrdinalIgnoreCase);

	private static bool IsBinaryMode(EventData message) =>
		message.Properties.ContainsKey(CeSpecVersionProperty) &&
		message.Properties.ContainsKey(CeTypeProperty) &&
		message.Properties.ContainsKey(CeSourceProperty) &&
		message.Properties.ContainsKey(CeIdProperty);

	private static bool IsRequiredCloudEventProperty(string propertyName) =>
		propertyName.Equals(CeSpecVersionProperty, StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals(CeTypeProperty, StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals(CeSourceProperty, StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals(CeIdProperty, StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals(CeTimeProperty, StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals(CeDataContentTypeProperty, StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals(CeSubjectProperty, StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals(CeDataSchemaProperty, StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals("specversion", StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals("type", StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals("source", StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals("time", StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals("datacontenttype", StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals("subject", StringComparison.OrdinalIgnoreCase) ||
		propertyName.Equals("dataschema", StringComparison.OrdinalIgnoreCase);

	private static string? GetStringAttribute(CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[attributeName];
		return value switch
		{
			null => null,
			string text => text,
			_ => value.ToString(),
		};
	}

	private static void ApplyDispatchEnvelopeExtensions(IDictionary<string, object?> properties, CloudEvent cloudEvent)
	{
		AddDispatchExtension(properties, cloudEvent, "correlationid");
		AddDispatchExtension(properties, cloudEvent, "tenantid");
		AddDispatchExtension(properties, cloudEvent, "userid");
		AddDispatchExtension(properties, cloudEvent, "traceparent");
		AddDispatchExtension(properties, cloudEvent, "deliverycount");
		AddDispatchExtension(properties, cloudEvent, "scheduledtime");
		AddDispatchExtension(properties, cloudEvent, "deadlineutc");
		AddDispatchExtension(properties, cloudEvent, "partitionkey");
		AddDispatchExtension(properties, cloudEvent, "partitionid");
		AddDispatchExtension(properties, cloudEvent, "sequencenumber");
		AddDispatchExtension(properties, cloudEvent, "offset");
	}

	private static void AddDispatchExtension(IDictionary<string, object?> properties, CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[DispatchPrefix + attributeName]
					?? cloudEvent[DispatchPrefixWithoutSeparator + attributeName]
					?? cloudEvent[attributeName];
		if (value is null)
		{
			return;
		}

		properties[DispatchPrefix + attributeName] = value.ToString();
	}

	private static void RestoreDispatchEnvelopeProperties(CloudEvent cloudEvent, IReadOnlyDictionary<string, object?> properties)
	{
		foreach (var property in properties)
		{
			if (!property.Key.StartsWith(DispatchPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var attributeName = property.Key[DispatchPrefix.Length..];
			var value = property.Value?.ToString();
			if (string.IsNullOrEmpty(value))
			{
				continue;
			}

			cloudEvent[DispatchPrefix + attributeName] = value;

			var alternateName = DispatchPrefixWithoutSeparator + attributeName;
			if (cloudEvent[alternateName] is null)
			{
				cloudEvent[alternateName] = value;
			}
		}
	}

	private static void RestoreExtensionAttributes(CloudEvent cloudEvent, IReadOnlyDictionary<string, object?> properties)
	{
		foreach (var property in properties)
		{
			if (!IsCloudEventAttributeName(property.Key) || IsRequiredCloudEventProperty(property.Key))
			{
				continue;
			}

			var attributeName = StripCloudEventAttributePrefix(property.Key);
			var value = property.Value?.ToString();
			if (!string.IsNullOrEmpty(value))
			{
				cloudEvent[attributeName] = value;
			}
		}
	}

	private static void ApplyExtensionAttributes(IDictionary<string, object?> properties, CloudEvent cloudEvent,
		bool includeCloudEventHeaders)
	{
		if (!includeCloudEventHeaders)
		{
			foreach (var attribute in cloudEvent.GetPopulatedAttributes())
			{
				var attributeName = attribute.Key.Name;
				if (IsRequiredCloudEventProperty(attributeName))
				{
					continue;
				}

				var value = cloudEvent[attributeName];
				if (value is null)
				{
					continue;
				}

				properties[attributeName] = value.ToString();
			}

			return;
		}

		properties[CeSpecVersionProperty] = cloudEvent.SpecVersion.VersionId;

		if (!string.IsNullOrWhiteSpace(cloudEvent.Type))
		{
			properties[CeTypeProperty] = cloudEvent.Type;
		}

		if (cloudEvent.Source is not null)
		{
			properties[CeSourceProperty] = cloudEvent.Source.ToString();
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Id))
		{
			properties[CeIdProperty] = cloudEvent.Id;
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			properties[CeSubjectProperty] = cloudEvent.Subject;
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
		{
			properties[CeDataContentTypeProperty] = cloudEvent.DataContentType;
		}

		if (cloudEvent.Time.HasValue)
		{
			properties[CeTimeProperty] = cloudEvent.Time.Value.ToString("O", CultureInfo.InvariantCulture);
		}

		if (cloudEvent.DataSchema is not null)
		{
			properties[CeDataSchemaProperty] = cloudEvent.DataSchema.ToString();
		}

		foreach (var attribute in cloudEvent.GetPopulatedAttributes())
		{
			var attributeName = attribute.Key.Name;
			if (IsRequiredCloudEventProperty(attributeName))
			{
				continue;
			}

			var value = cloudEvent[attributeName];
			if (value is null)
			{
				continue;
			}

			var normalizedName = StripCloudEventAttributePrefix(attributeName);

			properties[$"{CloudEventsAttributePrefix}{normalizedName}"] = value switch
			{
				JsonElement jsonElement => jsonElement.ToString(),
				_ => value.ToString() ?? string.Empty,
			};
		}
	}

	[RequiresUnreferencedCode(
		"Calls Excalibur.Dispatch.Transport.AzureServiceBus.CloudEvents.AzureEventHubsCloudEventAdapter.ConvertToBinaryData(Object)")]
	[RequiresDynamicCode(
		"Calls Excalibur.Dispatch.Transport.AzureServiceBus.CloudEvents.AzureEventHubsCloudEventAdapter.ConvertToBinaryData(Object)")]
	private static EventData CreateBinaryMessage(CloudEvent cloudEvent) => new(ConvertToBinaryData(cloudEvent.Data))
	{
		ContentType = cloudEvent.DataContentType ?? "application/json",
		MessageId = cloudEvent.Id ?? Guid.NewGuid().ToString(),
	};

	private static void MapEventDataPropertiesToCloudEvent(EventData message, CloudEvent cloudEvent)
	{
		if (!string.IsNullOrWhiteSpace(message.MessageId))
		{
			cloudEvent.Id = message.MessageId;
		}

		if (message.Properties.TryGetValue(CeSubjectProperty, out var subjectObj) && subjectObj is string subject &&
			!string.IsNullOrWhiteSpace(subject))
		{
			cloudEvent.Subject = subject;
		}

		if (!string.IsNullOrWhiteSpace(message.CorrelationId))
		{
			cloudEvent["traceparent"] = message.CorrelationId;
		}

		if (!string.IsNullOrWhiteSpace(message.ContentType))
		{
			cloudEvent.DataContentType ??= message.ContentType;
		}

		if (message.Properties.TryGetValue(CeTimeoutProperty, out var timeout))
		{
			cloudEvent[TimeoutAttributeName] = timeout?.ToString();
		}

		if (message.Properties.TryGetValue(CeTimeProperty, out var timeObj) &&
			DateTimeOffset.TryParse(timeObj?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
		{
			cloudEvent.Time ??= time;
		}

		if (message.Properties.TryGetValue("traceparent", out var traceParent) && traceParent is not null)
		{
			cloudEvent["traceparent"] ??= traceParent.ToString();
		}

		if (!string.IsNullOrWhiteSpace(message.PartitionKey))
		{
			cloudEvent[$"{DispatchPrefix}partitionkey"] = message.PartitionKey;
			cloudEvent[$"{DispatchPrefixWithoutSeparator}partitionkey"] ??= message.PartitionKey;
		}

		if (message.SequenceNumber != long.MinValue)
		{
			var sequenceNumber = message.SequenceNumber.ToString(CultureInfo.InvariantCulture);
			cloudEvent[$"{DispatchPrefix}sequencenumber"] = sequenceNumber;
			cloudEvent[$"{DispatchPrefixWithoutSeparator}sequencenumber"] ??= sequenceNumber;
		}

#pragma warning disable CS0618 // EventData.Offset is obsolete but OffsetString is not available in all SDK versions
		if (message.Offset != long.MinValue)
		{
			var offset = message.Offset.ToString(CultureInfo.InvariantCulture);
			cloudEvent[$"{DispatchPrefix}offset"] = offset;
			cloudEvent[$"{DispatchPrefixWithoutSeparator}offset"] ??= offset;
		}
#pragma warning restore CS0618

		if (message.EnqueuedTime != default)
		{
			var enqueued = message.EnqueuedTime.ToString("O", CultureInfo.InvariantCulture);
			cloudEvent[$"{DispatchPrefix}enqueuedtime"] = enqueued;
			cloudEvent[$"{DispatchPrefixWithoutSeparator}enqueuedtime"] ??= enqueued;
		}

		cloudEvent.Data ??= DeserializeMessageBody(message.EventBody, cloudEvent.DataContentType);
	}

	private EventData CreateStructuredMessage(CloudEvent cloudEvent)
	{
		var encoded = _jsonFormatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
		var payload = BinaryData.FromBytes(encoded.ToArray());

		var eventData = new EventData(payload)
		{
			ContentType = contentType?.ToString() ?? CloudEventsStructuredContentType,
			MessageId = cloudEvent.Id ?? Guid.NewGuid().ToString(),
		};

		eventData.Properties["Content-Type"] = eventData.ContentType;

		return eventData;
	}

	private void ApplyStandardEventDataProperties(EventData eventData, CloudEvent cloudEvent)
	{
		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			eventData.Properties[CeSubjectProperty] = cloudEvent.Subject;
		}

		var traceParent = GetStringAttribute(cloudEvent, "traceparent") ?? GetStringAttribute(cloudEvent, "correlationid");
		if (!string.IsNullOrWhiteSpace(traceParent))
		{
			eventData.CorrelationId = traceParent;
			eventData.Properties["traceparent"] = traceParent;
		}

		var timeout = GetStringAttribute(cloudEvent, TimeoutAttributeName);
		if (!string.IsNullOrWhiteSpace(timeout))
		{
			eventData.Properties[CeTimeoutProperty] = timeout;
		}

		if (cloudEvent.Time.HasValue)
		{
			eventData.Properties[CeTimeProperty] = cloudEvent.Time.Value.ToString("O", CultureInfo.InvariantCulture);
		}

		ApplyPartitionKey(eventData, cloudEvent);
	}

	private void ApplyPartitionKey(EventData eventData, CloudEvent cloudEvent)
	{
		if (!_eventHubsOptions.UsePartitionKeys)
		{
			return;
		}

		var partitionKey = ResolvePartitionKey(cloudEvent);
		if (string.IsNullOrWhiteSpace(partitionKey))
		{
			return;
		}

		// Note: EventData.PartitionKey is set during construction and is readonly. Store the partition key in properties for reference.
		eventData.Properties[$"{DispatchPrefix}partitionkey"] = partitionKey;
	}

	private string? ResolvePartitionKey(CloudEvent cloudEvent) => _eventHubsOptions.PartitionKeyStrategy switch
	{
		PartitionKeyStrategy.CorrelationId =>
			GetStringAttribute(cloudEvent, "traceparent") ?? GetStringAttribute(cloudEvent, "correlationid"),
		PartitionKeyStrategy.TenantId =>
			GetStringAttribute(cloudEvent, $"{DispatchPrefix}tenantid") ?? GetStringAttribute(cloudEvent, "tenantid"),
		PartitionKeyStrategy.UserId =>
			GetStringAttribute(cloudEvent, $"{DispatchPrefix}userid") ?? GetStringAttribute(cloudEvent, "userid"),
		PartitionKeyStrategy.Source => cloudEvent.Source?.ToString(),
		PartitionKeyStrategy.Type => cloudEvent.Type,
		PartitionKeyStrategy.Custom =>
			GetStringAttribute(cloudEvent, $"{DispatchPrefix}partitionkey") ?? GetStringAttribute(cloudEvent, "partitionkey"),
		_ => null,
	};

	private async Task<CloudEvent> DecodeStructuredMessageAsync(EventData transportMessage)
	{
		var body = transportMessage.EventBody;
		if (body.IsEmpty)
		{
			throw new InvalidOperationException("Structured CloudEvent message body cannot be empty.");
		}

		await using var stream = body.ToStream();
		return await _jsonFormatter.DecodeStructuredModeMessageAsync(
			stream,
			new System.Net.Mime.ContentType(transportMessage.ContentType),
			extensionAttributes: null).ConfigureAwait(false);
	}

	private CloudEvent DecodeBinaryMessage(EventData transportMessage)
	{
		if (!transportMessage.Properties.TryGetValue(CeSpecVersionProperty, out var specVersionObj) ||
			!transportMessage.Properties.TryGetValue(CeTypeProperty, out var typeObj) ||
			!transportMessage.Properties.TryGetValue(CeSourceProperty, out var sourceObj) ||
			!transportMessage.Properties.TryGetValue(CeIdProperty, out var idObj))
		{
			throw new InvalidOperationException(
				$"Event Hubs message '{transportMessage.MessageId}' is missing required CloudEvent attributes.");
		}

		var specVersion = specVersionObj?.ToString() switch
		{
			"1.0" => CloudEventsSpecVersion.V1_0,
			_ => Options.SpecVersion,
		};

		var cloudEvent = new CloudEvent(specVersion)
		{
			Type = typeObj?.ToString(),
			Source = Uri.TryCreate(sourceObj?.ToString(), UriKind.RelativeOrAbsolute, out var uri)
				? uri
				: Options.DefaultSource,
			Id = idObj?.ToString(),
		};

		if (transportMessage.Properties.TryGetValue(CeTimeProperty, out var timeObj) &&
			DateTimeOffset.TryParse(timeObj?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
		{
			cloudEvent.Time = time;
		}

		if (transportMessage.Properties.TryGetValue(CeDataContentTypeProperty, out var contentTypeObj))
		{
			cloudEvent.DataContentType = contentTypeObj?.ToString();
		}

		if (transportMessage.Properties.TryGetValue(CeSubjectProperty, out var subjectObj))
		{
			cloudEvent.Subject = subjectObj?.ToString();
		}

		if (transportMessage.Properties.TryGetValue(CeDataSchemaProperty, out var schemaObj) &&
			Uri.TryCreate(schemaObj?.ToString(), UriKind.Absolute, out var schema))
		{
			cloudEvent.DataSchema = schema;
		}

		foreach (var property in transportMessage.Properties)
		{
			if (!IsCloudEventAttributeName(property.Key) ||
				IsRequiredCloudEventProperty(property.Key))
			{
				continue;
			}

			var attributeName = StripCloudEventAttributePrefix(property.Key);
			cloudEvent[attributeName] = property.Value?.ToString();
		}

		cloudEvent.Data = DeserializeMessageBody(transportMessage.EventBody, cloudEvent.DataContentType);

		return cloudEvent;
	}

	// The AMQP binding mandates "cloudEvents_" on the wire, and that is what this adapter now WRITES.
	// It still READS the "ce-" spelling earlier versions of this adapter emitted, because the two breaks
	// are not the same: changing what we emit is an API-compatibility decision, which a pre-release line
	// covers; refusing to read what we already put on a consumer's queue is a DATA-compatibility decision,
	// which it does not. Messages sent by prior versions are sitting in real subscriptions right now, and
	// a consumer upgrading this package must still be able to drain them.
	//
	// WHEN THIS READ GOES AWAY: 11.0.0, and it is not a date anyone picks here. The published upgrade
	// policy carries a rule for exactly this — when a transport's wire format changes, the READING side of
	// the old format is kept for the remainder of the major line and removed at the next major. From the
	// release carrying this change onward, this adapter writes the spec-assigned prefix; 11.0.0 writes and
	// reads it alone.
	//
	// WHAT THE DRAIN OBLIGATION ACTUALLY COVERS, stated for the moment it matters rather than for today.
	// Every version released before this change writes the hyphenated spelling and nothing else, so a
	// message is in the old form if and only if the version that published it predates this change. By
	// the time a consumer crosses to 11.x they will have run versions on both sides, and their backlog
	// will hold a MIX — so the obligation is not "everything you have is stale", which is true only until
	// this ships, nor "a little legacy tail", which understates it. It is: any message written by a
	// version older than this one is unreadable by 11.0.0, and no consumer can tell which those are by
	// looking at the queue.
	//
	// That rule governs DATA, which is the axis this affordance sits on. The policy's other rule — that
	// minor and patch stay backward compatible within a major — is about API surface and does NOT settle
	// a question about bytes already written to a queue. Both point at 11.0.0 here; only the first one is
	// the reason, and citing the wrong one would make this comment collapse the moment someone noticed.
	//
	// CONSUMER OBLIGATION, because the other half is not observable from inside the framework: before
	// crossing to 11.x, drain or re-publish any message a 10.x version wrote with the old spelling.
	// Nothing in this library can see a consumer's backlog, so nothing here can tell them when it is safe
	// — only what they must do first, and the policy publishes it while 10.x is current so they can act.
	private static bool IsCloudEventAttributeName(string name) =>
		name.StartsWith(CloudEventsAttributePrefix, StringComparison.OrdinalIgnoreCase)
		|| name.StartsWith(LegacyCloudEventsAttributePrefix, StringComparison.OrdinalIgnoreCase);

	private static string StripCloudEventAttributePrefix(string name) =>
		name.StartsWith(CloudEventsAttributePrefix, StringComparison.OrdinalIgnoreCase)
			? name[CloudEventsAttributePrefix.Length..]
			: name.StartsWith(LegacyCloudEventsAttributePrefix, StringComparison.OrdinalIgnoreCase)
				? name[LegacyCloudEventsAttributePrefix.Length..]
				: name;

}
