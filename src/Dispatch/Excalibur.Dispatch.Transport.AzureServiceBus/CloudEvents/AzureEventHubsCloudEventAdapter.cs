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
public sealed class AzureEventHubsCloudEventAdapter : IAzureEventHubsCloudEventAdapter
{
	private const string CloudEventsStructuredContentType = "application/cloudevents+json";

	private const string CeSpecVersionProperty = "ce-specversion";
	private const string CeTypeProperty = "ce-type";
	private const string CeSourceProperty = "ce-source";
	private const string CeIdProperty = "ce-id";
	private const string CeTimeProperty = "ce-time";
	private const string CeDataContentTypeProperty = "ce-datacontenttype";
	private const string CeSubjectProperty = "ce-subject";
	private const string CeDataSchemaProperty = "ce-dataschema";
	private const string CeTimeoutProperty = "ce-timeout";
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

		return contentType?.ToUpperInvariant() switch
		{
			"APPLICATION/JSON" => JsonDocument.Parse(body).RootElement.Clone(),
			CloudEventsStructuredContentType => JsonDocument.Parse(body).RootElement.Clone(),
			_ => body.ToString(),
		};
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
			if (!property.Key.StartsWith("ce-", StringComparison.OrdinalIgnoreCase) || IsRequiredCloudEventProperty(property.Key))
			{
				continue;
			}

			var attributeName = property.Key[3..];
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

			var normalizedName = attributeName.StartsWith("ce-", StringComparison.OrdinalIgnoreCase)
				? attributeName[3..]
				: attributeName;

			properties[$"ce-{normalizedName}"] = value switch
			{
				JsonElement jsonElement => jsonElement.ToString(),
				_ => value.ToString() ?? string.Empty,
			};
		}
	}

	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Transport.AzureServiceBus.CloudEvents.AzureEventHubsCloudEventAdapter.ConvertToBinaryData(Object)")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Transport.AzureServiceBus.CloudEvents.AzureEventHubsCloudEventAdapter.ConvertToBinaryData(Object)")]
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

		if (message.Offset != long.MinValue)
		{
			var offset = message.Offset.ToString(CultureInfo.InvariantCulture);
			cloudEvent[$"{DispatchPrefix}offset"] = offset;
			cloudEvent[$"{DispatchPrefixWithoutSeparator}offset"] ??= offset;
		}

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
			if (!property.Key.StartsWith("ce-", StringComparison.OrdinalIgnoreCase) ||
				IsRequiredCloudEventProperty(property.Key))
			{
				continue;
			}

			var attributeName = property.Key[3..];
			cloudEvent[attributeName] = property.Value?.ToString();
		}

		cloudEvent.Data = DeserializeMessageBody(transportMessage.EventBody, cloudEvent.DataContentType);

		return cloudEvent;
	}
}
