// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Azure.Messaging.ServiceBus;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Maps CloudEvents to Azure Service Bus messages and vice versa.
/// </summary>
public sealed class AzureServiceBusCloudEventAdapter : ICloudEventMapper<ServiceBusMessage>
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
	private readonly ILogger<AzureServiceBusCloudEventAdapter> _logger;
	private readonly AzureServiceBusCloudEventOptions _serviceBusOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureServiceBusCloudEventAdapter" /> class.
	/// </summary>
	/// <param name="options"> CloudEvent serialization options. </param>
	/// <param name="serviceBusOptions"> Azure Service Bus specific options. </param>
	/// <param name="logger"> Logger for diagnostics. </param>
	public AzureServiceBusCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		IOptions<AzureServiceBusCloudEventOptions>? serviceBusOptions,
		ILogger<AzureServiceBusCloudEventAdapter> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger;
		_serviceBusOptions = serviceBusOptions?.Value ?? new AzureServiceBusCloudEventOptions();
	}

	/// <inheritdoc />
	public CloudEventOptions Options { get; }

	/// <inheritdoc />
	public static ValueTask<CloudEventMode?> TryDetectMode(
		ServiceBusMessage transportMessage,
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
	public Task<ServiceBusMessage> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var message = mode switch
		{
			CloudEventMode.Structured => CreateStructuredMessage(cloudEvent),
			CloudEventMode.Binary => CreateBinaryMessage(cloudEvent),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for Service Bus."),
		};

		ApplyStandardServiceBusProperties(message, cloudEvent);
		ApplyDispatchEnvelopeExtensions(message.ApplicationProperties, cloudEvent);

		if (mode == CloudEventMode.Binary)
		{
			ApplyBinaryModeAttributes(message.ApplicationProperties, cloudEvent);
		}

		_logger.LogDebug(
			"Converted CloudEvent {EventId} to Service Bus message {MessageId} using {Mode} mode",
			cloudEvent.Id,
			message.MessageId,
			mode);

		return Task.FromResult(message);
	}

	/// <inheritdoc />
	public async Task<CloudEvent> FromTransportMessageAsync(
		ServiceBusMessage transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		var mode = await TryDetectMode(transportMessage, cancellationToken).ConfigureAwait(false)
				   ?? Options.DefaultMode;

		var cloudEvent = mode switch
		{
			CloudEventMode.Structured => await DecodeStructuredMessageAsync(transportMessage).ConfigureAwait(false),
			CloudEventMode.Binary => DecodeBinaryMessage(transportMessage),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for Service Bus."),
		};

		MapServiceBusPropertiesToCloudEvent(transportMessage, cloudEvent);
		RestoreDispatchEnvelopeProperties(cloudEvent, (IReadOnlyDictionary<string, object>)transportMessage.ApplicationProperties);

		_logger.LogDebug(
			"Converted Service Bus message {MessageId} to CloudEvent {EventId} using {Mode} mode",
			transportMessage.MessageId,
			cloudEvent.Id,
			mode);

		return cloudEvent;
	}

	private static void RestoreDispatchEnvelopeProperties(
		CloudEvent cloudEvent,
		IReadOnlyDictionary<string, object> applicationProperties)
	{
		foreach (var property in applicationProperties)
		{
			if (!property.Key.StartsWith(DispatchPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var attributeName = property.Key[DispatchPrefix.Length..];
			var value = property.Value?.ToString();
			if (value is null)
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

	[RequiresUnreferencedCode("Calls System.BinaryData.FromObjectAsJson<T>(T, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.BinaryData.FromObjectAsJson<T>(T, JsonSerializerOptions)")]
	private static BinaryData ConvertToBinaryData(object? data) => data switch
	{
		null => BinaryData.FromBytes([]),
		BinaryData binaryData => binaryData,
		byte[] bytes => BinaryData.FromBytes(bytes),
		string text => BinaryData.FromString(text),
		_ => BinaryData.FromObjectAsJson(data),
	};

	private static object DeserializeMessageBody(BinaryData body, string? contentType)
	{
		if (body.ToMemory().IsEmpty)
		{
			return string.Empty;
		}

		return contentType?.ToUpperInvariant() switch
		{
			"APPLICATION/JSON" => JsonDocument.Parse(body).RootElement.Clone(),
			_ => body.ToString(),
		};
	}

	private static bool IsStructuredMode(ServiceBusMessage message) =>
		!string.IsNullOrWhiteSpace(message.ContentType) &&
		message.ContentType.Contains("application/cloudevents", StringComparison.OrdinalIgnoreCase);

	private static bool IsBinaryMode(ServiceBusMessage message) =>
		message.ApplicationProperties.ContainsKey(CeSpecVersionProperty) &&
		message.ApplicationProperties.ContainsKey(CeTypeProperty) &&
		message.ApplicationProperties.ContainsKey(CeSourceProperty) &&
		message.ApplicationProperties.ContainsKey(CeIdProperty);

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

	private static void ApplyDispatchEnvelopeExtensions(IDictionary<string, object> applicationProperties, CloudEvent cloudEvent)
	{
		AddDispatchExtension(applicationProperties, cloudEvent, "correlationid");
		AddDispatchExtension(applicationProperties, cloudEvent, "tenantid");
		AddDispatchExtension(applicationProperties, cloudEvent, "userid");
		AddDispatchExtension(applicationProperties, cloudEvent, "traceparent");
		AddDispatchExtension(applicationProperties, cloudEvent, "deliverycount");
		AddDispatchExtension(applicationProperties, cloudEvent, "scheduledtime");
	}

	private static void AddDispatchExtension(IDictionary<string, object> applicationProperties, CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[DispatchPrefix + attributeName]
					?? cloudEvent[DispatchPrefixWithoutSeparator + attributeName]
					?? cloudEvent[attributeName];
		if (value is null)
		{
			return;
		}

		applicationProperties[DispatchPrefix + attributeName] = value.ToString()!;
	}

	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Transport.AzureServiceBus.CloudEvents.AzureServiceBusCloudEventAdapter.ConvertToBinaryData(Object)")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Transport.AzureServiceBus.CloudEvents.AzureServiceBusCloudEventAdapter.ConvertToBinaryData(Object)")]
	private static ServiceBusMessage CreateBinaryMessage(CloudEvent cloudEvent)
	{
		var message = new ServiceBusMessage(ConvertToBinaryData(cloudEvent.Data))
		{
			ContentType = cloudEvent.DataContentType ?? "application/json",
		};

		if (!string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
		{
			message.ApplicationProperties[CeDataContentTypeProperty] = cloudEvent.DataContentType;
		}

		return message;
	}

	private static void ApplyBinaryModeAttributes(IDictionary<string, object> applicationProperties, CloudEvent cloudEvent)
	{
		applicationProperties[CeSpecVersionProperty] = cloudEvent.SpecVersion.VersionId;

		if (!string.IsNullOrWhiteSpace(cloudEvent.Type))
		{
			applicationProperties[CeTypeProperty] = cloudEvent.Type;
		}

		if (cloudEvent.Source is not null)
		{
			applicationProperties[CeSourceProperty] = cloudEvent.Source.ToString();
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Id))
		{
			applicationProperties[CeIdProperty] = cloudEvent.Id;
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			applicationProperties[CeSubjectProperty] = cloudEvent.Subject;
		}

		if (cloudEvent.DataSchema is not null)
		{
			applicationProperties[CeDataSchemaProperty] = cloudEvent.DataSchema.ToString();
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

			applicationProperties[$"ce-{normalizedName}"] = value switch
			{
				JsonElement jsonElement => jsonElement.ToString(),
				_ => value.ToString() ?? string.Empty,
			};
		}
	}

	private static void MapServiceBusPropertiesToCloudEvent(ServiceBusMessage message, CloudEvent cloudEvent)
	{
		if (!string.IsNullOrWhiteSpace(message.MessageId))
		{
			cloudEvent.Id = message.MessageId;
		}

		if (!string.IsNullOrWhiteSpace(message.Subject))
		{
			cloudEvent.Subject = message.Subject;
		}

		if (!string.IsNullOrWhiteSpace(message.CorrelationId))
		{
			cloudEvent["traceparent"] = message.CorrelationId;
		}

		if (message.ApplicationProperties.TryGetValue(CeTimeoutProperty, out var timeout))
		{
			cloudEvent[TimeoutAttributeName] = timeout?.ToString();
		}

		if (message.ApplicationProperties.TryGetValue("partitionkey", out var partitionKey))
		{
			cloudEvent["partitionkey"] = partitionKey?.ToString();
		}

		if (!string.IsNullOrWhiteSpace(message.SessionId))
		{
			cloudEvent["sessionid"] = message.SessionId;
		}
	}

	private ServiceBusMessage CreateStructuredMessage(CloudEvent cloudEvent)
	{
		var encoded = _jsonFormatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);

		// Use ArrayPool to avoid allocation when copying to BinaryData
		var length = encoded.Length;
		var buffer = ArrayPool<byte>.Shared.Rent(length);
		try
		{
			encoded.Span.CopyTo(buffer);
			var payload = BinaryData.FromBytes(new ReadOnlyMemory<byte>(buffer, 0, length));

			var message = new ServiceBusMessage(payload) { ContentType = contentType?.ToString() ?? CloudEventsStructuredContentType };

			message.ApplicationProperties["Content-Type"] = message.ContentType;

			return message;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	private void ApplyStandardServiceBusProperties(ServiceBusMessage message, CloudEvent cloudEvent)
	{
		message.MessageId = cloudEvent.Id ?? Guid.NewGuid().ToString();

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			message.Subject = cloudEvent.Subject;
		}

		var traceParent = GetStringAttribute(cloudEvent, "traceparent");
		if (!string.IsNullOrWhiteSpace(traceParent))
		{
			message.CorrelationId = traceParent;
			message.ApplicationProperties["traceparent"] = traceParent;
		}

		var timeout = GetStringAttribute(cloudEvent, TimeoutAttributeName);
		if (!string.IsNullOrWhiteSpace(timeout))
		{
			message.ApplicationProperties[CeTimeoutProperty] = timeout;
		}

		if (cloudEvent.Time.HasValue)
		{
			message.ApplicationProperties[CeTimeProperty] = cloudEvent.Time.Value.ToString("O");
		}

		if (_serviceBusOptions.EnableScheduledDelivery && cloudEvent["scheduledtime"] is string scheduled &&
			DateTimeOffset.TryParse(scheduled, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var scheduledTime))
		{
			message.ScheduledEnqueueTime = scheduledTime;
		}

		if (cloudEvent["sessionid"] is string sessionId)
		{
			message.SessionId = sessionId;
		}
		else if (_serviceBusOptions.UseSessionsForOrdering && !string.IsNullOrWhiteSpace(_serviceBusOptions.DefaultSessionId))
		{
			message.SessionId = _serviceBusOptions.DefaultSessionId;
		}

		if (cloudEvent["partitionkey"] is string partitionKey && _serviceBusOptions.UsePartitionKeys)
		{
			message.PartitionKey = partitionKey;
		}

		if (_serviceBusOptions.TimeToLive.HasValue)
		{
			message.TimeToLive = _serviceBusOptions.TimeToLive.Value;
		}
	}

	private async Task<CloudEvent> DecodeStructuredMessageAsync(ServiceBusMessage transportMessage)
	{
		var body = transportMessage.Body;
		if (body is null || body.Length == 0)
		{
			throw new InvalidOperationException("Structured CloudEvent message body cannot be empty.");
		}

		await using var stream = body.ToStream();

		return await _jsonFormatter.DecodeStructuredModeMessageAsync(
			stream,
			new System.Net.Mime.ContentType(transportMessage.ContentType),
			extensionAttributes: null).ConfigureAwait(false);
	}

	private CloudEvent DecodeBinaryMessage(ServiceBusMessage transportMessage)
	{
		if (!transportMessage.ApplicationProperties.TryGetValue(CeSpecVersionProperty, out var specVersionObj) ||
			!transportMessage.ApplicationProperties.TryGetValue(CeTypeProperty, out var typeObj) ||
			!transportMessage.ApplicationProperties.TryGetValue(CeSourceProperty, out var sourceObj) ||
			!transportMessage.ApplicationProperties.TryGetValue(CeIdProperty, out var idObj))
		{
			throw new InvalidOperationException(
				$"Service Bus message '{transportMessage.MessageId}' is missing required CloudEvent attributes.");
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

		if (transportMessage.ApplicationProperties.TryGetValue(CeTimeProperty, out var timeObj) &&
			DateTimeOffset.TryParse(timeObj?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
		{
			cloudEvent.Time = time;
		}

		if (transportMessage.ApplicationProperties.TryGetValue(CeDataContentTypeProperty, out var contentTypeObj))
		{
			cloudEvent.DataContentType = contentTypeObj?.ToString();
		}

		if (transportMessage.ApplicationProperties.TryGetValue(CeSubjectProperty, out var subjectObj))
		{
			cloudEvent.Subject = subjectObj?.ToString();
		}

		if (transportMessage.ApplicationProperties.TryGetValue(CeDataSchemaProperty, out var schemaObj) &&
			Uri.TryCreate(schemaObj?.ToString(), UriKind.Absolute, out var schema))
		{
			cloudEvent.DataSchema = schema;
		}

		foreach (var property in transportMessage.ApplicationProperties)
		{
			if (!property.Key.StartsWith("ce-", StringComparison.OrdinalIgnoreCase) ||
				IsRequiredCloudEventProperty(property.Key))
			{
				continue;
			}

			var attributeName = property.Key[3..];
			cloudEvent[attributeName] = property.Value?.ToString();
		}

		if (transportMessage.Body is not null)
		{
			cloudEvent.Data = DeserializeMessageBody(transportMessage.Body, cloudEvent.DataContentType);
		}

		return cloudEvent;
	}
}
