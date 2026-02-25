// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Amazon.SimpleNotificationService.Model;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SNS implementation of <see cref="ICloudEventMapper{TTransportMessage}" />.
/// </summary>
public sealed class AwsSnsCloudEventAdapter : ICloudEventMapper<PublishRequest>
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
	private readonly ILogger<AwsSnsCloudEventAdapter> _logger;
	private readonly AwsSnsCloudEventOptions _snsOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSnsCloudEventAdapter" /> class.
	/// </summary>
	public AwsSnsCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		ILogger<AwsSnsCloudEventAdapter> logger,
		IOptions<AwsSnsCloudEventOptions>? snsOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger;
		_snsOptions = snsOptions?.Value ?? new AwsSnsCloudEventOptions();
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

	/// <inheritdoc />
	public static ValueTask<CloudEventMode?> TryDetectMode(
		PublishRequest transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		if (transportMessage.MessageAttributes.TryGetValue(StructuredContentTypeAttribute, out var contentTypeAttr) &&
			contentTypeAttr.StringValue?.Contains("cloudevents", StringComparison.OrdinalIgnoreCase) == true)
		{
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Structured);
		}

		if (transportMessage.MessageAttributes.ContainsKey(CeSpecVersionAttribute) &&
			transportMessage.MessageAttributes.ContainsKey(CeTypeAttribute))
		{
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Binary);
		}

		if (!string.IsNullOrWhiteSpace(transportMessage.Message))
		{
			try
			{
				using var document = JsonDocument.Parse(transportMessage.Message);
				var root = document.RootElement;
				if (root.TryGetProperty("specversion", out _) &&
					root.TryGetProperty("type", out _) &&
					root.TryGetProperty("source", out _) &&
					root.TryGetProperty("id", out _))
				{
					return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Structured);
				}
			}
			catch
			{
				// Ignore parse errors; fall through.
			}
		}

		return ValueTask.FromResult<CloudEventMode?>(null);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task<PublishRequest> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var request = new PublishRequest
		{
			TopicArn = string.Empty,
			MessageAttributes = new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal),
		};

		ApplyCloudEventAttributes(request.MessageAttributes, cloudEvent);
		ApplyDispatchAttributes(request.MessageAttributes, cloudEvent);

		switch (mode)
		{
			case CloudEventMode.Structured:
				var payload = _jsonFormatter.EncodeStructuredModeMessage(cloudEvent, out _);
				request.Message = Encoding.UTF8.GetString(payload.Span);
				request.MessageAttributes[StructuredContentTypeAttribute] =
					CreateStringAttribute(CloudEventsStructuredContentType);
				break;

			case CloudEventMode.Binary:
				request.Message = EncodeBinaryBody(cloudEvent);
				break;

			default:
				throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for AWS SNS.");
		}

		ApplyProviderOptions(request, cloudEvent);

		_logger.LogDebug(
			"Converted CloudEvent {EventId} to SNS transport message using {Mode} mode",
			cloudEvent.Id,
			mode);

		return await Task.FromResult(request).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<CloudEvent> FromTransportMessageAsync(
		PublishRequest transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		var mode = await TryDetectMode(transportMessage, cancellationToken).ConfigureAwait(false)
				   ?? Options.DefaultMode;

		var cloudEvent = mode switch
		{
			CloudEventMode.Structured => await ParseStructuredMessageAsync(transportMessage.Message).ConfigureAwait(false),
			CloudEventMode.Binary => ParseBinaryMessage(transportMessage),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for AWS SNS."),
		};

		EnrichFromAttributes(cloudEvent, transportMessage.MessageAttributes);

		if (!string.IsNullOrWhiteSpace(transportMessage.Subject) && string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			cloudEvent.Subject = transportMessage.Subject;
		}

		_logger.LogDebug(
			"Converted SNS transport message to CloudEvent {EventId} using {Mode} mode",
			cloudEvent.Id,
			mode);

		return cloudEvent;
	}

	/// <summary>
	/// Convenience API to convert a CloudEvent to an SNS <see cref="PublishRequest" /> for a specific topic.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task<PublishRequest> ToSnsMessageAsync(
		CloudEvent cloudEvent,
		string topicArn,
		CancellationToken cancellationToken,
		CloudEventMode mode = CloudEventMode.Structured)
	{
		ArgumentException.ThrowIfNullOrEmpty(topicArn);

		var request = await ToTransportMessageAsync(cloudEvent, mode, cancellationToken).ConfigureAwait(false);
		request.TopicArn = topicArn;
		return request;
	}

	/// <summary>
	/// Converts a raw SNS JSON payload into a CloudEvent, preserving CloudEvent attributes when present.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public async Task<CloudEvent?> FromSnsMessageAsync(
		string snsMessageJson,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(snsMessageJson);
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			using var document = JsonDocument.Parse(snsMessageJson);
			var root = document.RootElement;

			if (root.TryGetProperty("Message", out var messageElement))
			{
				var publishRequest = new PublishRequest
				{
					Message = messageElement.GetString() ?? string.Empty,
					MessageAttributes = new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal),
				};

				if (root.TryGetProperty("Subject", out var subjectElement))
				{
					publishRequest.Subject = subjectElement.GetString();
				}

				if (root.TryGetProperty("MessageAttributes", out var attributesElement) &&
					attributesElement.ValueKind == JsonValueKind.Object)
				{
					foreach (var attribute in attributesElement.EnumerateObject())
					{
						if (!attribute.Value.TryGetProperty("Value", out var valueElement))
						{
							continue;
						}

						var type = attribute.Value.TryGetProperty("Type", out var typeElement)
							? typeElement.GetString() ?? StringAttributeType
							: StringAttributeType;

						publishRequest.MessageAttributes[attribute.Name] = new MessageAttributeValue
						{
							DataType = type,
							StringValue = valueElement.GetString(),
						};
					}
				}

				return await FromTransportMessageAsync(publishRequest, cancellationToken).ConfigureAwait(false);
			}

			// Structured CloudEvent payload without SNS envelope.
			var fallbackRequest = new PublishRequest
			{
				Message = snsMessageJson,
				MessageAttributes = new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal),
			};

			return await FromTransportMessageAsync(fallbackRequest, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to convert SNS message to CloudEvent");
			return null;
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static string EncodeBinaryBody(CloudEvent cloudEvent) => cloudEvent.Data switch
	{
		null => string.Empty,
		string text => text,
		byte[] bytes => Encoding.UTF8.GetString(bytes),
		ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.ToArray()),
		_ => JsonSerializer.Serialize(cloudEvent.Data),
	};

	private static MessageAttributeValue CreateStringAttribute(string value) =>
		new() { DataType = StringAttributeType, StringValue = value };

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
		IDictionary<string, MessageAttributeValue> attributes,
		CloudEvent cloudEvent)
	{
		var specVersion = cloudEvent.SpecVersion?.VersionId ?? Options.SpecVersion.VersionId;
		attributes[CeSpecVersionAttribute] = CreateStringAttribute(specVersion);

		if (!string.IsNullOrWhiteSpace(cloudEvent.Type))
		{
			attributes[CeTypeAttribute] = CreateStringAttribute(cloudEvent.Type);
		}

		if (cloudEvent.Source is not null)
		{
			attributes[CeSourceAttribute] = CreateStringAttribute(cloudEvent.Source.ToString());
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Id))
		{
			attributes[CeIdAttribute] = CreateStringAttribute(cloudEvent.Id);
		}

		if (cloudEvent.Time.HasValue)
		{
			attributes[CeTimeAttribute] = CreateStringAttribute(cloudEvent.Time.Value.ToString("O"));
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
		{
			attributes[CeDataContentTypeAttribute] = CreateStringAttribute(cloudEvent.DataContentType);
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			attributes[CeSubjectAttribute] = CreateStringAttribute(cloudEvent.Subject);
		}

		if (cloudEvent.DataSchema is not null)
		{
			attributes[CeDataSchemaAttribute] = CreateStringAttribute(cloudEvent.DataSchema.ToString());
		}

		var timeout = GetStringAttribute(cloudEvent, "timeout");
		if (!string.IsNullOrWhiteSpace(timeout))
		{
			attributes[CeTimeoutAttribute] = CreateStringAttribute(timeout);
		}

		var traceParent = GetStringAttribute(cloudEvent, TraceParentAttribute);
		if (!string.IsNullOrWhiteSpace(traceParent))
		{
			attributes[TraceParentAttribute] = CreateStringAttribute(traceParent);
		}

		foreach (var extension in cloudEvent.ExtensionAttributes)
		{
			var value = cloudEvent[extension.Name];
			if (value is null)
			{
				continue;
			}

			var attributeName = $"ce-{extension.Name}";
			if (attributes.ContainsKey(attributeName))
			{
				continue;
			}

			attributes[attributeName] = CreateStringAttribute(value.ToString());
		}
	}

	private void ApplyDispatchAttributes(
		IDictionary<string, MessageAttributeValue> attributes,
		CloudEvent cloudEvent)
	{
		AddDispatchAttribute(attributes, cloudEvent, "correlationid");
		AddDispatchAttribute(attributes, cloudEvent, "tenantid");
		AddDispatchAttribute(attributes, cloudEvent, "userid");
		AddDispatchAttribute(attributes, cloudEvent, TraceParentAttribute);
		AddDispatchAttribute(attributes, cloudEvent, "deliverycount");
		AddDispatchAttribute(attributes, cloudEvent, "scheduledtime");
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

	private void ApplyProviderOptions(PublishRequest request, CloudEvent cloudEvent)
	{
		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			request.Subject = cloudEvent.Subject;
		}
		else if (!string.IsNullOrWhiteSpace(_snsOptions.DefaultSubject))
		{
			request.Subject = _snsOptions.DefaultSubject;
		}

		if (_snsOptions.UseFifoFeatures)
		{
			request.MessageGroupId ??= GetStringAttribute(cloudEvent, "messagegroupid")
									   ?? _snsOptions.DefaultMessageGroupId;

			if (_snsOptions.EnableContentBasedDeduplication)
			{
				request.MessageDeduplicationId ??= GetStringAttribute(cloudEvent, "deduplicationid")
												   ?? cloudEvent.Id
												   ?? Guid.NewGuid().ToString();
			}
		}

		if (!_snsOptions.IncludeMessageAttributes)
		{
			request.MessageAttributes.Clear();
		}
	}

	private async Task<CloudEvent> ParseStructuredMessageAsync(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			throw new InvalidOperationException("Structured CloudEvent payload is empty.");
		}

		await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(message));
		var cloudEvent = await _jsonFormatter.DecodeStructuredModeMessageAsync(stream, contentType: null, extensionAttributes: null)
			.ConfigureAwait(false);
		return cloudEvent;
	}

	private CloudEvent ParseBinaryMessage(PublishRequest request)
	{
		if (!request.MessageAttributes.TryGetValue(CeTypeAttribute, out var typeAttr) ||
			!request.MessageAttributes.TryGetValue(CeSourceAttribute, out var sourceAttr) ||
			!request.MessageAttributes.TryGetValue(CeIdAttribute, out var idAttr))
		{
			throw new InvalidOperationException("SNS message is missing required CloudEvent attributes.");
		}

		var cloudEvent = new CloudEvent(Options.SpecVersion)
		{
			Type = typeAttr.StringValue,
			Id = idAttr.StringValue,
			Source = Uri.TryCreate(sourceAttr.StringValue, UriKind.RelativeOrAbsolute, out var sourceUri)
				? sourceUri
				: Options.DefaultSource,
		};

		if (request.MessageAttributes.TryGetValue(CeTimeAttribute, out var timeAttr) &&
			DateTimeOffset.TryParse(timeAttr.StringValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
		{
			cloudEvent.Time = timestamp;
		}

		if (request.MessageAttributes.TryGetValue(CeDataContentTypeAttribute, out var contentTypeAttr))
		{
			cloudEvent.DataContentType = contentTypeAttr.StringValue;
		}

		if (request.MessageAttributes.TryGetValue(CeSubjectAttribute, out var subjectAttr))
		{
			cloudEvent.Subject = subjectAttr.StringValue;
		}

		if (request.MessageAttributes.TryGetValue(CeDataSchemaAttribute, out var schemaAttr) &&
			Uri.TryCreate(schemaAttr.StringValue, UriKind.RelativeOrAbsolute, out var schemaUri))
		{
			cloudEvent.DataSchema = schemaUri;
		}

		if (!string.IsNullOrWhiteSpace(request.Message))
		{
			cloudEvent.Data = request.Message;
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

		if (attributes.TryGetValue(CeDataContentTypeAttribute, out var contentTypeAttr) &&
			string.IsNullOrEmpty(cloudEvent.DataContentType))
		{
			cloudEvent.DataContentType = contentTypeAttr.StringValue;
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

		RestoreDispatchAttributes(cloudEvent, attributes);
	}

	private void RestoreDispatchAttributes(
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
