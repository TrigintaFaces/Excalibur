// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

using Amazon.SQS.Model;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SQS implementation of <see cref="ICloudEventMapper{TTransportMessage}" /> that supports both structured and binary CloudEvents encodings.
/// </summary>
public sealed class AwsSqsCloudEventAdapter : ICloudEventMapper<SendMessageRequest>
{
	private const string CloudEventsStructuredContentType = "application/cloudevents+json";
	private const string StructuredContentTypeAttribute = "contentType";

	private const string CeSpecVersionAttribute = "ce-specversion";
	private const string CeTypeAttribute = "ce-type";
	private const string CeSourceAttribute = "ce-source";
	private const string CeIdAttribute = "ce-id";
	private const string CeTimeAttribute = "ce-time";
	private const string CeDataContentTypeAttribute = "ce-datacontenttype";
	private const string CeSubjectAttribute = "ce-subject";
	private const string CeDataSchemaAttribute = "ce-dataschema";
	private const string CeTimeoutAttribute = "ce-timeout";

	private const string TraceParentAttribute = "traceparent";
	private const string StringAttributeType = "String";

	private readonly JsonEventFormatter _jsonFormatter = new();
	private readonly ILogger<AwsSqsCloudEventAdapter> _logger;
	private readonly AwsSqsCloudEventOptions _sqsOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsCloudEventAdapter" /> class.
	/// </summary>
	/// <param name="options"> The CloudEvents configuration options. </param>
	/// <param name="logger"> Logger used for diagnostics. </param>
	/// <param name="sqsOptions"> Provider specific CloudEvent options. </param>
	public AwsSqsCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		ILogger<AwsSqsCloudEventAdapter> logger,
		IOptions<AwsSqsCloudEventOptions>? sqsOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger;
		_sqsOptions = sqsOptions?.Value ?? new AwsSqsCloudEventOptions();
	}

	/// <inheritdoc />
	public CloudEventOptions Options { get; }

	private string DispatchPrefix => string.IsNullOrWhiteSpace(Options.DispatchExtensionPrefix)
		? "dispatch-"
		: Options.DispatchExtensionPrefix.EndsWith('-')
			? Options.DispatchExtensionPrefix
			: Options.DispatchExtensionPrefix + "-";

	private string DispatchPrefixWithoutSeparator => string.IsNullOrWhiteSpace(Options.DispatchExtensionPrefix)
		? "dispatch"
		: Options.DispatchExtensionPrefix.Replace("-", string.Empty, StringComparison.Ordinal);

	/// <summary>
	/// Attempts to detect the CloudEvents mode from an SQS <see cref="Message" /> instance.
	/// </summary>
	public static ValueTask<CloudEventMode?> TryDetectMode(
		Message transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

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
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task<SendMessageRequest> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var request = new SendMessageRequest
		{
			QueueUrl = string.Empty,
			MessageAttributes = new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal),
		};

		ApplyCloudEventAttributes(request.MessageAttributes, cloudEvent);
		ApplyDispatchEnvelopeAttributes(request.MessageAttributes, cloudEvent);

		switch (mode)
		{
			case CloudEventMode.Structured:
				var payload = _jsonFormatter.EncodeStructuredModeMessage(cloudEvent, out _);
				request.MessageBody = Encoding.UTF8.GetString(payload.Span);
				request.MessageAttributes[StructuredContentTypeAttribute] =
					CreateStringAttribute(CloudEventsStructuredContentType);
				break;

			case CloudEventMode.Binary:
				request.MessageBody = EncodeBinaryBody(cloudEvent);
				break;

			default:
				throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for AWS SQS.");
		}

		ApplyProviderOptions(request, cloudEvent);

		_logger.LogDebug(
			"Converted CloudEvent {EventId} to SQS transport message using {Mode} mode",
			cloudEvent.Id,
			mode);

		return await Task.FromResult(request).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<CloudEvent> FromTransportMessageAsync(
		SendMessageRequest transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		var sqsMessage = ConvertToSqsMessage(transportMessage);
		var mode = await TryDetectMode(sqsMessage, cancellationToken).ConfigureAwait(false)
				   ?? Options.DefaultMode;

		var cloudEvent = mode switch
		{
			CloudEventMode.Structured => await ParseStructuredModeAsync(sqsMessage).ConfigureAwait(false),
			CloudEventMode.Binary => ParseBinaryMode(sqsMessage),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for AWS SQS."),
		};

		EnrichFromAttributes(cloudEvent, sqsMessage.MessageAttributes);

		_logger.LogDebug(
			"Converted SQS transport message to CloudEvent {EventId} using {Mode} mode",
			cloudEvent.Id,
			mode);

		return cloudEvent;
	}

	/// <inheritdoc />
	public ValueTask<CloudEventMode?> TryDetectMode(
		SendMessageRequest transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);

		var message = ConvertToSqsMessage(transportMessage);
		return TryDetectMode(message, cancellationToken);
	}

	/// <summary>
	/// Converts a CloudEvent into an SQS <see cref="SendMessageRequest" /> targeted at the provided queue URL.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task<SendMessageRequest> ToSqsMessageAsync(
		CloudEvent cloudEvent,
		string queueUrl,
		CancellationToken cancellationToken,
		CloudEventMode mode = CloudEventMode.Structured)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);

		var request = await ToTransportMessageAsync(cloudEvent, mode, cancellationToken).ConfigureAwait(false);
		request.QueueUrl = queueUrl;
		return request;
	}

	/// <summary>
	/// Converts an SQS <see cref="Message" /> into a CloudEvent.
	/// </summary>
	/// <exception cref="NotSupportedException"> </exception>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public async Task<CloudEvent> FromSqsMessageAsync(
		Message sqsMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(sqsMessage);
		cancellationToken.ThrowIfCancellationRequested();

		var mode = await TryDetectMode(sqsMessage, cancellationToken).ConfigureAwait(false)
				   ?? Options.DefaultMode;

		var cloudEvent = mode switch
		{
			CloudEventMode.Structured => await ParseStructuredModeAsync(sqsMessage).ConfigureAwait(false),
			CloudEventMode.Binary => ParseBinaryMode(sqsMessage),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for AWS SQS."),
		};

		EnrichFromAttributes(cloudEvent, sqsMessage.MessageAttributes);
		return cloudEvent;
	}

	/// <summary>
	/// Converts a batch of CloudEvents into an SQS batch request respecting provider options.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task<SendMessageBatchRequest> ToBatchSqsMessageAsync(
		IEnumerable<CloudEvent> cloudEvents,
		string queueUrl,
		CancellationToken cancellationToken,
		CloudEventMode mode = CloudEventMode.Structured)
	{
		ArgumentNullException.ThrowIfNull(cloudEvents);
		ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);

		var batch = new SendMessageBatchRequest { QueueUrl = queueUrl, Entries = new List<SendMessageBatchRequestEntry>() };

		var entryId = 0;
		foreach (var cloudEvent in cloudEvents.Take(_sqsOptions.MaxBatchSize))
		{
			var singleMessage = await ToTransportMessageAsync(cloudEvent, mode, cancellationToken)
				.ConfigureAwait(false);

			batch.Entries.Add(new SendMessageBatchRequestEntry
			{
				Id = entryId.ToString(CultureInfo.InvariantCulture),
				MessageBody = singleMessage.MessageBody,
				MessageAttributes = CloneAttributes(singleMessage.MessageAttributes),
				DelaySeconds = singleMessage.DelaySeconds,
				MessageDeduplicationId = singleMessage.MessageDeduplicationId,
				MessageGroupId = singleMessage.MessageGroupId,
			});

			entryId++;
		}

		_logger.LogDebug(
			"Created SQS batch request containing {Count} CloudEvent entries",
			batch.Entries.Count);

		return batch;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static string EncodeBinaryBody(CloudEvent cloudEvent) => cloudEvent.Data switch
	{
		null => string.Empty,
		byte[] binary => Convert.ToBase64String(binary),
		ReadOnlyMemory<byte> rom => Convert.ToBase64String(rom.ToArray()),
		string text => text,
		_ => JsonSerializer.Serialize(cloudEvent.Data),
	};

	private static object DeserializeMessageBody(string body, string? contentType)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return body;
		}

		return contentType?.ToUpperInvariant() switch
		{
			"APPLICATION/JSON" => JsonDocument.Parse(body).RootElement,
			"APPLICATION/X-BASE64" => Convert.FromBase64String(body),
			_ => body,
		};
	}

	private static MessageAttributeValue CreateStringAttribute(string value) =>
		new() { DataType = StringAttributeType, StringValue = value };

	private static Message ConvertToSqsMessage(SendMessageRequest request)
	{
		var message = new Message
		{
			Body = request.MessageBody,
			MessageAttributes = CloneAttributes(request.MessageAttributes),
			Attributes = new Dictionary<string, string>(StringComparer.Ordinal),
		};

		if (!string.IsNullOrEmpty(request.MessageGroupId))
		{
			message.Attributes[nameof(request.MessageGroupId)] = request.MessageGroupId;
		}

		if (!string.IsNullOrEmpty(request.MessageDeduplicationId))
		{
			message.Attributes[nameof(request.MessageDeduplicationId)] = request.MessageDeduplicationId;
		}

		if (request.DelaySeconds.HasValue && request.DelaySeconds.Value > 0)
		{
			message.Attributes[nameof(request.DelaySeconds)] = request.DelaySeconds.Value.ToString(CultureInfo.InvariantCulture);
		}

		return message;
	}

	private static Dictionary<string, MessageAttributeValue> CloneAttributes(
		IDictionary<string, MessageAttributeValue>? source)
	{
		if (source is null || source.Count == 0)
		{
			return new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);
		}

		var clone = new Dictionary<string, MessageAttributeValue>(source.Count, StringComparer.Ordinal);
		foreach (var attribute in source)
		{
			clone[attribute.Key] = new MessageAttributeValue
			{
				DataType = attribute.Value.DataType,
				BinaryListValues = attribute.Value.BinaryListValues,
				BinaryValue = attribute.Value.BinaryValue,
				StringListValues = attribute.Value.StringListValues,
				StringValue = attribute.Value.StringValue,
			};
		}

		return clone;
	}

	private static bool IsStructuredMode(Message sqsMessage)
	{
		if (sqsMessage.MessageAttributes.TryGetValue(StructuredContentTypeAttribute, out var contentTypeAttr) &&
			contentTypeAttr.StringValue?.Contains("cloudevents", StringComparison.OrdinalIgnoreCase) == true)
		{
			return true;
		}

		if (string.IsNullOrWhiteSpace(sqsMessage.Body))
		{
			return false;
		}

		try
		{
			using var document = JsonDocument.Parse(sqsMessage.Body);
			var root = document.RootElement;

			return root.TryGetProperty("specversion", out _) &&
				   root.TryGetProperty("type", out _) &&
				   root.TryGetProperty("source", out _) &&
				   root.TryGetProperty("id", out _);
		}
		catch
		{
			return false;
		}
	}

	private static bool IsBinaryMode(Message sqsMessage) =>
		sqsMessage.MessageAttributes.ContainsKey(CeSpecVersionAttribute) &&
		sqsMessage.MessageAttributes.ContainsKey(CeTypeAttribute) &&
		sqsMessage.MessageAttributes.ContainsKey(CeSourceAttribute) &&
		sqsMessage.MessageAttributes.ContainsKey(CeIdAttribute);

	private static bool IsRequiredCloudEventAttribute(string attributeName) =>
		attributeName.Equals(CeSpecVersionAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTypeAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeSourceAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeIdAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTimeAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeDataContentTypeAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeSubjectAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeDataSchemaAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTimeoutAttribute, StringComparison.OrdinalIgnoreCase);

	private void ApplyCloudEventAttributes(
		IDictionary<string, MessageAttributeValue> messageAttributes,
		CloudEvent cloudEvent)
	{
		var specVersion = cloudEvent.SpecVersion?.VersionId ?? Options.SpecVersion.VersionId;
		messageAttributes[CeSpecVersionAttribute] = CreateStringAttribute(specVersion);

		if (!string.IsNullOrWhiteSpace(cloudEvent.Type))
		{
			messageAttributes[CeTypeAttribute] = CreateStringAttribute(cloudEvent.Type);
		}

		if (cloudEvent.Source is not null)
		{
			messageAttributes[CeSourceAttribute] = CreateStringAttribute(cloudEvent.Source.ToString());
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Id))
		{
			messageAttributes[CeIdAttribute] = CreateStringAttribute(cloudEvent.Id);
		}

		if (cloudEvent.Time.HasValue)
		{
			messageAttributes[CeTimeAttribute] = CreateStringAttribute(cloudEvent.Time.Value.ToString("O"));
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
		{
			messageAttributes[CeDataContentTypeAttribute] = CreateStringAttribute(cloudEvent.DataContentType);
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			messageAttributes[CeSubjectAttribute] = CreateStringAttribute(cloudEvent.Subject);
		}

		if (cloudEvent.DataSchema is not null)
		{
			messageAttributes[CeDataSchemaAttribute] = CreateStringAttribute(cloudEvent.DataSchema.ToString());
		}

		var timeout = GetStringAttribute(cloudEvent, "timeout");
		if (!string.IsNullOrWhiteSpace(timeout))
		{
			messageAttributes[CeTimeoutAttribute] = CreateStringAttribute(timeout);
		}

		var traceParent = GetStringAttribute(cloudEvent, TraceParentAttribute);
		if (!string.IsNullOrWhiteSpace(traceParent))
		{
			messageAttributes[TraceParentAttribute] = CreateStringAttribute(traceParent);
		}

		foreach (var extension in cloudEvent.ExtensionAttributes)
		{
			var value = cloudEvent[extension.Name];
			if (value is null)
			{
				continue;
			}

			var attributeName = $"ce-{extension.Name}";
			if (messageAttributes.ContainsKey(attributeName))
			{
				continue;
			}

			messageAttributes[attributeName] = CreateStringAttribute(value.ToString());
		}
	}

	private void ApplyDispatchEnvelopeAttributes(
		IDictionary<string, MessageAttributeValue> messageAttributes,
		CloudEvent cloudEvent)
	{
		AddDispatchAttribute(messageAttributes, cloudEvent, "correlationid");
		AddDispatchAttribute(messageAttributes, cloudEvent, "tenantid");
		AddDispatchAttribute(messageAttributes, cloudEvent, "userid");
		AddDispatchAttribute(messageAttributes, cloudEvent, TraceParentAttribute);
		AddDispatchAttribute(messageAttributes, cloudEvent, "deliverycount");
		AddDispatchAttribute(messageAttributes, cloudEvent, "scheduledtime");
	}

	private void AddDispatchAttribute(
		IDictionary<string, MessageAttributeValue> attributes,
		CloudEvent cloudEvent,
		string attributeName)
	{
		var value = cloudEvent[DispatchPrefix + attributeName]
					?? cloudEvent[DispatchPrefixWithoutSeparator + attributeName]
					?? cloudEvent[attributeName];

		if (value is null)
		{
			return;
		}

		attributes[DispatchPrefix + attributeName] = CreateStringAttribute(value.ToString());
	}

	private void ApplyProviderOptions(SendMessageRequest request, CloudEvent cloudEvent)
	{
		if (_sqsOptions.DelaySeconds > 0)
		{
			request.DelaySeconds = _sqsOptions.DelaySeconds;
		}

		if (_sqsOptions.UseFifoFeatures)
		{
			request.MessageGroupId ??= GetStringAttribute(cloudEvent, "messagegroupid")
									   ?? _sqsOptions.DefaultMessageGroupId;

			if (_sqsOptions.EnableContentBasedDeduplication)
			{
				request.MessageDeduplicationId ??= GetStringAttribute(cloudEvent, "deduplicationid")
												   ?? cloudEvent.Id
												   ?? Guid.NewGuid().ToString();
			}
		}
	}

	private async Task<CloudEvent> ParseStructuredModeAsync(Message sqsMessage)
	{
		if (string.IsNullOrWhiteSpace(sqsMessage.Body))
		{
			throw new InvalidOperationException("Structured CloudEvent message body is empty.");
		}

		await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sqsMessage.Body));
		var cloudEvent = await _jsonFormatter.DecodeStructuredModeMessageAsync(
				stream,
				new ContentType("application/json"),
				extensionAttributes: null)
			.ConfigureAwait(false);

		return cloudEvent;
	}

	private CloudEvent ParseBinaryMode(Message sqsMessage)
	{
		if (!sqsMessage.MessageAttributes.TryGetValue(CeTypeAttribute, out var typeAttr) ||
			!sqsMessage.MessageAttributes.TryGetValue(CeSourceAttribute, out var sourceAttr) ||
			!sqsMessage.MessageAttributes.TryGetValue(CeIdAttribute, out var idAttr))
		{
			throw new InvalidOperationException("SQS message is missing required CloudEvent attributes.");
		}

		var cloudEvent = new CloudEvent(Options.SpecVersion)
		{
			Type = typeAttr.StringValue,
			Id = idAttr.StringValue,
			Source = Uri.TryCreate(sourceAttr.StringValue, UriKind.RelativeOrAbsolute, out var sourceUri)
				? sourceUri
				: Options.DefaultSource,
		};

		if (sqsMessage.MessageAttributes.TryGetValue(CeTimeAttribute, out var timeAttr) &&
			DateTimeOffset.TryParse(timeAttr.StringValue, CultureInfo.InvariantCulture,
				DateTimeStyles.RoundtripKind, out var timestamp))
		{
			cloudEvent.Time = timestamp;
		}

		if (sqsMessage.MessageAttributes.TryGetValue(CeDataContentTypeAttribute, out var contentTypeAttr))
		{
			cloudEvent.DataContentType = contentTypeAttr.StringValue;
		}

		if (sqsMessage.MessageAttributes.TryGetValue(CeSubjectAttribute, out var subjectAttr))
		{
			cloudEvent.Subject = subjectAttr.StringValue;
		}

		if (sqsMessage.MessageAttributes.TryGetValue(CeDataSchemaAttribute, out var schemaAttr) &&
			Uri.TryCreate(schemaAttr.StringValue, UriKind.RelativeOrAbsolute, out var schemaUri))
		{
			cloudEvent.DataSchema = schemaUri;
		}

		if (!string.IsNullOrWhiteSpace(sqsMessage.Body))
		{
			cloudEvent.Data = DeserializeMessageBody(sqsMessage.Body, cloudEvent.DataContentType);
		}

		return cloudEvent;
	}

	private void EnrichFromAttributes(
		CloudEvent cloudEvent,
		IDictionary<string, MessageAttributeValue> attributes)
	{
		if (attributes.TryGetValue(CeSubjectAttribute, out var subjectAttr) && string.IsNullOrEmpty(cloudEvent.Subject))
		{
			cloudEvent.Subject = subjectAttr.StringValue;
		}

		if (attributes.TryGetValue(CeTimeAttribute, out var timeAttr) && !cloudEvent.Time.HasValue &&
			DateTimeOffset.TryParse(timeAttr.StringValue, CultureInfo.InvariantCulture,
				DateTimeStyles.RoundtripKind, out var timestamp))
		{
			cloudEvent.Time = timestamp;
		}

		if (attributes.TryGetValue(CeDataContentTypeAttribute, out var contentTypeAttr) &&
			string.IsNullOrEmpty(cloudEvent.DataContentType))
		{
			cloudEvent.DataContentType = contentTypeAttr.StringValue;
		}

		if (attributes.TryGetValue(CeDataSchemaAttribute, out var schemaAttr) &&
			cloudEvent.DataSchema is null &&
			Uri.TryCreate(schemaAttr.StringValue, UriKind.RelativeOrAbsolute, out var schemaUri))
		{
			cloudEvent.DataSchema = schemaUri;
		}

		if (attributes.TryGetValue(CeTimeoutAttribute, out var timeoutAttr))
		{
			cloudEvent["timeout"] = timeoutAttr.StringValue;
		}

		if (attributes.TryGetValue(TraceParentAttribute, out var traceAttr))
		{
			cloudEvent[TraceParentAttribute] = traceAttr.StringValue;
		}

		foreach (var attribute in attributes)
		{
			if (!attribute.Key.StartsWith("ce-", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (IsRequiredCloudEventAttribute(attribute.Key))
			{
				continue;
			}

			var extensionName = attribute.Key[3..];
			cloudEvent[extensionName] ??= attribute.Value.StringValue;
		}

		RestoreDispatchEnvelopeProperties(cloudEvent, attributes);
	}

	private void RestoreDispatchEnvelopeProperties(
		CloudEvent cloudEvent,
		IDictionary<string, MessageAttributeValue> attributes)
	{
		foreach (var attribute in attributes)
		{
			if (!attribute.Key.StartsWith(DispatchPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var extensionName = attribute.Key[DispatchPrefix.Length..];
			var value = attribute.Value.StringValue;
			if (string.IsNullOrEmpty(value))
			{
				continue;
			}

			cloudEvent[DispatchPrefixWithoutSeparator + extensionName] = value;
			cloudEvent[DispatchPrefix + extensionName] ??= value;
		}
	}

	private string? GetStringAttribute(CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[DispatchPrefix + attributeName]
					?? cloudEvent[DispatchPrefixWithoutSeparator + attributeName]
					?? cloudEvent[attributeName];

		return value switch
		{
			null => null,
			string text => text,
			_ => value.ToString(),
		};
	}
}
