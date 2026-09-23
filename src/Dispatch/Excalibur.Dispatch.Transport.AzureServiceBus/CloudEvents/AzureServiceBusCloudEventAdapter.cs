// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
internal sealed class AzureServiceBusCloudEventAdapter : ICloudEventEncoder<ServiceBusMessage>
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
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
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

	[RequiresUnreferencedCode(
		"Calls Excalibur.Dispatch.Transport.AzureServiceBus.CloudEvents.AzureServiceBusCloudEventAdapter.ConvertToBinaryData(Object)")]
	[RequiresDynamicCode(
		"Calls Excalibur.Dispatch.Transport.AzureServiceBus.CloudEvents.AzureServiceBusCloudEventAdapter.ConvertToBinaryData(Object)")]
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

			var normalizedName = StripCloudEventAttributePrefix(attributeName);

			applicationProperties[$"{CloudEventsAttributePrefix}{normalizedName}"] = value switch
			{
				JsonElement jsonElement => jsonElement.ToString(),
				_ => value.ToString() ?? string.Empty,
			};
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


	// The AMQP binding mandates "cloudEvents_" on application properties, and that is what this adapter
	// writes. It is an ENCODER only — ICloudEventEncoder<ServiceBusMessage>, and ServiceBusMessage is a
	// send-only SDK type that never arrives inbound — so there is no read path here and nothing in this
	// file decides what a consumer can still receive. The legacy spelling survives in exactly one place
	// below: an extension attribute handed to us already carrying the old prefix is normalised rather
	// than double-prefixed, so a caller migrating their own code does not emit "cloudEvents_ce-foo".
	//
	// The inbound side of this wire-format change, and the consumer drain obligation that goes with it,
	// live where messages are actually read. Do not restate them here — a comment about reading, in a
	// file that cannot read, is the kind of documentation that survives long after it stops being true.
	private static string StripCloudEventAttributePrefix(string name) =>
		name.StartsWith(CloudEventsAttributePrefix, StringComparison.OrdinalIgnoreCase)
			? name[CloudEventsAttributePrefix.Length..]
			: name.StartsWith(LegacyCloudEventsAttributePrefix, StringComparison.OrdinalIgnoreCase)
				? name[LegacyCloudEventsAttributePrefix.Length..]
				: name;

}
