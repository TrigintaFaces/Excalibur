// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Confluent.Kafka;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Default implementation of Kafka CloudEvent adapter that maps <see cref="CloudEvent" /> instances to <see cref="Message{TKey, TValue}" />
/// payloads and restores them back while preserving Dispatch envelope metadata.
/// </summary>
public sealed partial class KafkaCloudEventAdapter : IKafkaCloudEventAdapter
{
	private const string StructuredContentType = "application/cloudevents+json";
	private const string ContentTypeHeader = "Content-Type";
	private const string CePrefix = "ce-";
	private const string CeSpecVersionHeader = "ce-specversion";
	private const string CeTypeHeader = "ce-type";
	private const string CeSourceHeader = "ce-source";
	private const string CeIdHeader = "ce-id";
	private const string CeTimeHeader = "ce-time";
	private const string CeSubjectHeader = "ce-subject";
	private const string CeDataContentTypeHeader = "ce-datacontenttype";
	private const string CeDataSchemaHeader = "ce-dataschema";
	private const string CeTimeoutHeader = "ce-timeout";
	private const string TraceParentHeader = "traceparent";

	private readonly JsonEventFormatter _jsonFormatter = new();
	private readonly ILogger<KafkaCloudEventAdapter> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaCloudEventAdapter" /> class.
	/// </summary>
	/// <param name="options"> The CloudEvent configuration options. </param>
	/// <param name="kafkaOptions"> Kafka specific CloudEvent options. </param>
	/// <param name="logger"> Logger used for diagnostics. </param>
	public KafkaCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		KafkaCloudEventOptions kafkaOptions,
		ILogger<KafkaCloudEventAdapter> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(kafkaOptions);
		ArgumentNullException.ThrowIfNull(logger);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options));
		KafkaOptions = kafkaOptions;
		_logger = logger;
	}

	/// <inheritdoc />
	public CloudEventOptions Options { get; }

	private KafkaCloudEventOptions KafkaOptions { get; }

	private string DispatchPrefix => string.IsNullOrWhiteSpace(Options.DispatchExtensionPrefix)
		? "dispatch-"
		: Options.DispatchExtensionPrefix.EndsWith('-')
			? Options.DispatchExtensionPrefix
			: Options.DispatchExtensionPrefix + "-";

	private string DispatchPrefixWithoutSeparator => string.IsNullOrWhiteSpace(Options.DispatchExtensionPrefix)
		? "dispatch"
		: Options.DispatchExtensionPrefix.Replace("-", string.Empty, StringComparison.Ordinal);

	/// <inheritdoc />
	public static ValueTask<CloudEventMode?> TryDetectModeAsync(
		Message<string, string> transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		if (DetectBinaryMode(transportMessage))
		{
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Binary);
		}

		if (DetectStructuredMode(transportMessage))
		{
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Structured);
		}

		return ValueTask.FromResult<CloudEventMode?>(null);
	}

	/// <inheritdoc />
	public static ValueTask<CloudEventMode?> TryDetectModeAsync(
		global::Confluent.Kafka.ConsumeResult<string, string> consumeResult,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(consumeResult);
		return TryDetectModeAsync(consumeResult.Message, cancellationToken);
	}

	/// <inheritdoc />
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task<Message<string, string>> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var effectiveMode = mode is CloudEventMode.Structured or CloudEventMode.Binary
			? mode
			: Options.DefaultMode;

		var message = new Message<string, string> { Headers = new Headers() };

		try
		{
			switch (effectiveMode)
			{
				case CloudEventMode.Structured:
					EncodeStructuredMessage(cloudEvent, message);
					break;

				case CloudEventMode.Binary:
					EncodeBinaryMessage(cloudEvent, message);
					break;

				default:
					throw new NotSupportedException($"CloudEvent mode '{effectiveMode}' is not supported for Kafka.");
			}

			ApplyDispatchEnvelopeHeaders(message.Headers, cloudEvent);

			message.Key = DeterminePartitionKey(cloudEvent) ?? string.Empty;

			return await Task.FromResult(message).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogToTransportMessageError(cloudEvent.Id ?? string.Empty, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<CloudEvent> FromTransportMessageAsync(
		Message<string, string> transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var detectedMode = await TryDetectModeAsync(transportMessage, cancellationToken).ConfigureAwait(false)
							   ?? Options.DefaultMode;

			var cloudEvent = detectedMode switch
			{
				CloudEventMode.Structured => DecodeStructuredMessage(transportMessage),
				CloudEventMode.Binary => DecodeBinaryMessage(transportMessage),
				_ => throw new NotSupportedException($"CloudEvent mode '{detectedMode}' is not supported for Kafka."),
			};

			RestoreDispatchEnvelopeProperties(cloudEvent, transportMessage.Headers);

			if (!string.IsNullOrWhiteSpace(transportMessage.Key))
			{
				cloudEvent["partitionkey"] ??= transportMessage.Key;
				cloudEvent[$"{DispatchPrefix}partitionkey"] ??= transportMessage.Key;
				cloudEvent[$"{DispatchPrefixWithoutSeparator}partitionkey"] ??= transportMessage.Key;
			}

			return cloudEvent;
		}
		catch (Exception ex)
		{
			LogFromTransportMessageError(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<CloudEvent> FromKafkaMessageAsync(
		global::Confluent.Kafka.ConsumeResult<string, string> consumeResult,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(consumeResult);
		cancellationToken.ThrowIfCancellationRequested();

		var cloudEvent = await FromTransportMessageAsync(consumeResult.Message, cancellationToken).ConfigureAwait(false);

		RestorePartitionMetadata(cloudEvent, consumeResult);

		return cloudEvent;
	}

	/// <inheritdoc />
	public Task<CloudEvent> FromKafkaAsync(
		global::Confluent.Kafka.ConsumeResult<string, string> consumeResult,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(consumeResult);
		return FromKafkaMessageAsync(consumeResult, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<CloudEventMode?> DetectModeAsync(
		global::Confluent.Kafka.ConsumeResult<string, string> consumeResult,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(consumeResult);
		return TryDetectModeAsync(consumeResult.Message, cancellationToken);
	}

	private static bool IsRequiredCloudEventHeader(string attributeName) =>
		attributeName.Equals("specversion", StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals("type", StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals("source", StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals("time", StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals("datacontenttype", StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals("subject", StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals("dataschema", StringComparison.OrdinalIgnoreCase);

	private static void AddHeader(Headers headers, string headerName, string value)
	{
		headers.Remove(headerName);
		headers.Add(headerName, Encoding.UTF8.GetBytes(value));
	}

	private static bool TryGetHeader(Headers? headers, string headerName, out string value)
	{
		value = string.Empty;
		if (headers is null)
		{
			return false;
		}

		if (!headers.TryGetLastBytes(headerName, out var bytes) || bytes is null)
		{
			return false;
		}

		value = Encoding.UTF8.GetString(bytes);
		return true;
	}

	private static string ConvertToString(object value) => value switch
	{
		JsonElement element => element.ValueKind == JsonValueKind.String
			? element.GetString() ?? string.Empty
			: element.GetRawText(),
		DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
		DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
		_ => value.ToString() ?? string.Empty,
	};

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static string EncodeBinaryBody(CloudEvent cloudEvent)
	{
		if (cloudEvent.Data is null)
		{
			return string.Empty;
		}

		return cloudEvent.Data switch
		{
			string text => text,
			byte[] binary => Convert.ToBase64String(binary),
			ReadOnlyMemory<byte> memory => Convert.ToBase64String(memory.ToArray()),
			JsonElement element => element.GetRawText(),
			_ => JsonSerializer.Serialize(cloudEvent.Data),
		};
	}

	private static object? DecodeMessageValue(string? value, string? contentType)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(contentType))
		{
			return value;
		}

		var normalized = contentType.ToUpperInvariant();

		return normalized switch
		{
			"APPLICATION/JSON" => JsonDocument.Parse(value).RootElement.Clone(),
			"APPLICATION/CLOUDEVENTS+JSON" => JsonDocument.Parse(value).RootElement.Clone(),
			"APPLICATION/OCTET-STREAM" => Convert.FromBase64String(value),
			"APPLICATION/X-BASE64" => Convert.FromBase64String(value),
			_ when normalized.Contains("json", StringComparison.OrdinalIgnoreCase) =>
				JsonDocument.Parse(value).RootElement.Clone(),
			_ => value,
		};
	}

	private static bool DetectStructuredMode(Message<string, string> transportMessage)
	{
		if (transportMessage.Headers is not null &&
			transportMessage.Headers.TryGetLastBytes(ContentTypeHeader, out var contentTypeBytes) &&
			contentTypeBytes is not null)
		{
			var contentType = Encoding.UTF8.GetString(contentTypeBytes);
			if (contentType.Contains(StructuredContentType, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		if (string.IsNullOrWhiteSpace(transportMessage.Value))
		{
			return false;
		}

		try
		{
			using var document = JsonDocument.Parse(transportMessage.Value);
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

	private static bool DetectBinaryMode(Message<string, string> transportMessage)
	{
		var headers = transportMessage.Headers;
		if (headers is null)
		{
			return false;
		}

		return headers.Any(static header => header.Key.Equals(CeSpecVersionHeader, StringComparison.OrdinalIgnoreCase)) &&
			   headers.Any(static header => header.Key.Equals(CeTypeHeader, StringComparison.OrdinalIgnoreCase)) &&
			   headers.Any(static header => header.Key.Equals(CeSourceHeader, StringComparison.OrdinalIgnoreCase)) &&
			   headers.Any(static header => header.Key.Equals(CeIdHeader, StringComparison.OrdinalIgnoreCase));
	}

	private static CloudEvent DecodeBinaryMessage(Message<string, string> transportMessage)
	{
		var headers = transportMessage.Headers ?? throw new InvalidOperationException(
			"Binary CloudEvent transport messages must include headers.");

		if (!TryGetHeader(headers, CeSpecVersionHeader, out var specVersionValue))
		{
			throw new InvalidOperationException("Missing CloudEvent specversion header.");
		}

		var specVersion = string.Equals(specVersionValue, CloudEventsSpecVersion.V1_0.VersionId,
			StringComparison.Ordinal)
			? CloudEventsSpecVersion.V1_0
			: CloudEventsSpecVersion.V1_0;

		if (!TryGetHeader(headers, CeTypeHeader, out var typeValue))
		{
			throw new InvalidOperationException("Missing CloudEvent type header.");
		}

		if (!TryGetHeader(headers, CeSourceHeader, out var sourceValue))
		{
			throw new InvalidOperationException("Missing CloudEvent source header.");
		}

		if (!TryGetHeader(headers, CeIdHeader, out var idValue))
		{
			throw new InvalidOperationException("Missing CloudEvent id header.");
		}

		var cloudEvent = new CloudEvent(specVersion)
		{
			Type = typeValue,
			Source = new Uri(sourceValue, UriKind.RelativeOrAbsolute),
			Id = idValue,
		};

		if (TryGetHeader(headers, CeTimeHeader, out var timeValue) &&
			DateTimeOffset.TryParse(timeValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
		{
			cloudEvent.Time = time;
		}

		if (TryGetHeader(headers, CeDataContentTypeHeader, out var dataContentTypeValue))
		{
			cloudEvent.DataContentType = dataContentTypeValue;
		}

		if (TryGetHeader(headers, CeSubjectHeader, out var subjectValue))
		{
			cloudEvent.Subject = subjectValue;
		}

		if (TryGetHeader(headers, CeDataSchemaHeader, out var schemaValue) &&
			Uri.TryCreate(schemaValue, UriKind.RelativeOrAbsolute, out var schemaUri))
		{
			cloudEvent.DataSchema = schemaUri;
		}

		foreach (var header in headers)
		{
			if (!header.Key.StartsWith(CePrefix, StringComparison.OrdinalIgnoreCase) ||
				IsRequiredCloudEventHeader(header.Key[CePrefix.Length..]))
			{
				continue;
			}

			var value = Encoding.UTF8.GetString(header.GetValueBytes());
			var attributeName = header.Key[CePrefix.Length..];
			cloudEvent[attributeName] = value;
		}

		cloudEvent.Data = DecodeMessageValue(transportMessage.Value, cloudEvent.DataContentType);

		return cloudEvent;
	}

	private void EncodeStructuredMessage(CloudEvent cloudEvent, Message<string, string> message)
	{
		var payload = _jsonFormatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
		message.Value = Encoding.UTF8.GetString(payload.Span);

		var mediaType = contentType?.MediaType ?? StructuredContentType;
		AddHeader(message.Headers, ContentTypeHeader, mediaType);
	}

	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Transport.Kafka.CloudEvents.KafkaCloudEventAdapter.EncodeBinaryBody(CloudEvent)")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Transport.Kafka.CloudEvents.KafkaCloudEventAdapter.EncodeBinaryBody(CloudEvent)")]
	private void EncodeBinaryMessage(CloudEvent cloudEvent, Message<string, string> message)
	{
		var headers = message.Headers;

		AddHeader(headers, CeSpecVersionHeader, cloudEvent.SpecVersion.VersionId);

		if (!string.IsNullOrWhiteSpace(cloudEvent.Type))
		{
			AddHeader(headers, CeTypeHeader, cloudEvent.Type);
		}

		if (cloudEvent.Source is not null)
		{
			AddHeader(headers, CeSourceHeader, cloudEvent.Source.ToString());
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Id))
		{
			AddHeader(headers, CeIdHeader, cloudEvent.Id);
		}

		if (cloudEvent.Time.HasValue)
		{
			AddHeader(headers, CeTimeHeader, cloudEvent.Time.Value.ToString("O", CultureInfo.InvariantCulture));
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			AddHeader(headers, CeSubjectHeader, cloudEvent.Subject);
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
		{
			AddHeader(headers, CeDataContentTypeHeader, cloudEvent.DataContentType);
		}

		if (cloudEvent.DataSchema is not null)
		{
			AddHeader(headers, CeDataSchemaHeader, cloudEvent.DataSchema.ToString());
		}

		foreach (var attribute in cloudEvent.GetPopulatedAttributes())
		{
			var attributeName = attribute.Key.Name;
			if (IsRequiredCloudEventHeader(attributeName))
			{
				continue;
			}

			var value = cloudEvent[attributeName];
			if (value is null)
			{
				continue;
			}

			var normalizedName = attributeName.StartsWith(CePrefix, StringComparison.OrdinalIgnoreCase)
				? attributeName[CePrefix.Length..]
				: attributeName;

			AddHeader(headers, $"{CePrefix}{normalizedName}", ConvertToString(value));
		}

		var timeout = GetStringAttribute(cloudEvent, "ce-timeout") ?? GetStringAttribute(cloudEvent, "deadlineutc");
		if (!string.IsNullOrWhiteSpace(timeout))
		{
			AddHeader(headers, CeTimeoutHeader, timeout);
		}

		message.Value = EncodeBinaryBody(cloudEvent);
	}

	private CloudEvent DecodeStructuredMessage(Message<string, string> transportMessage)
	{
		if (string.IsNullOrWhiteSpace(transportMessage.Value))
		{
			throw new InvalidOperationException("Structured CloudEvent message body cannot be empty.");
		}

		var contentTypeHeader =
			transportMessage.Headers?.FirstOrDefault(static header =>
				header.Key.Equals(ContentTypeHeader, StringComparison.OrdinalIgnoreCase));

		ContentType contentType;
		if (contentTypeHeader is null)
		{
			contentType = new ContentType(StructuredContentType);
		}
		else
		{
			var mediaType = Encoding.UTF8.GetString(contentTypeHeader.GetValueBytes());
			contentType = new ContentType(mediaType);
		}

		var payload = Encoding.UTF8.GetBytes(transportMessage.Value);
		return _jsonFormatter.DecodeStructuredModeMessage(payload, contentType, extensionAttributes: null);
	}

	private void ApplyDispatchEnvelopeHeaders(Headers headers, CloudEvent cloudEvent)
	{
		AddDispatchHeader(headers, cloudEvent, "correlationid");
		AddDispatchHeader(headers, cloudEvent, "tenantid");
		AddDispatchHeader(headers, cloudEvent, "userid");
		AddDispatchHeader(headers, cloudEvent, "partitionkey");
		AddDispatchHeader(headers, cloudEvent, "deadlineutc");

		var traceParent = GetStringAttribute(cloudEvent, "traceparent")
						  ?? GetStringAttribute(cloudEvent, "correlationid");
		if (!string.IsNullOrWhiteSpace(traceParent))
		{
			AddHeader(headers, $"{DispatchPrefix}traceparent", traceParent);
			AddHeader(headers, TraceParentHeader, traceParent);
		}

		var timeout = GetStringAttribute(cloudEvent, "ce-timeout")
					  ?? GetStringAttribute(cloudEvent, "deadlineutc");
		if (!string.IsNullOrWhiteSpace(timeout))
		{
			AddHeader(headers, CeTimeoutHeader, timeout);
		}
	}

	private string? DeterminePartitionKey(CloudEvent cloudEvent)
	{
		string? Resolve(string attributeName)
		{
			return GetStringAttribute(cloudEvent, attributeName);
		}

		return KafkaOptions.PartitioningStrategy switch
		{
			KafkaPartitioningStrategy.CorrelationId => Resolve("correlationid"),
			KafkaPartitioningStrategy.TenantId => Resolve("tenantid"),
			KafkaPartitioningStrategy.UserId => Resolve("userid"),
			KafkaPartitioningStrategy.Source => cloudEvent.Source?.ToString(),
			KafkaPartitioningStrategy.Type => cloudEvent.Type,
			KafkaPartitioningStrategy.EventId => cloudEvent.Id,
			KafkaPartitioningStrategy.Custom => Resolve("partitionkey"),
			KafkaPartitioningStrategy.RoundRobin => null,
			_ => Resolve("partitionkey") ?? Resolve("correlationid") ?? cloudEvent.Id,
		};
	}

	private string? GetStringAttribute(CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[attributeName];
		if (value is not null)
		{
			return value switch
			{
				string text => text,
				_ => value.ToString(),
			};
		}

		value = cloudEvent[DispatchPrefix + attributeName];
		if (value is not null)
		{
			return value switch
			{
				string text => text,
				_ => value.ToString(),
			};
		}

		value = cloudEvent[DispatchPrefixWithoutSeparator + attributeName];
		return value switch
		{
			null => null,
			string text => text,
			_ => value.ToString(),
		};
	}

	private void AddDispatchHeader(Headers headers, CloudEvent cloudEvent, string attributeName)
	{
		var value = GetStringAttribute(cloudEvent, attributeName);
		if (!string.IsNullOrWhiteSpace(value))
		{
			AddHeader(headers, DispatchPrefix + attributeName, value);
		}
	}

	private void RestoreDispatchEnvelopeProperties(CloudEvent cloudEvent, Headers? headers)
	{
		if (headers is null)
		{
			return;
		}

		foreach (var header in headers)
		{
			if (!header.Key.StartsWith(DispatchPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var attributeName = header.Key[DispatchPrefix.Length..];
			var value = Encoding.UTF8.GetString(header.GetValueBytes());
			if (string.IsNullOrEmpty(value))
			{
				continue;
			}

			cloudEvent[DispatchPrefix + attributeName] = value;
			cloudEvent[DispatchPrefixWithoutSeparator + attributeName] ??= value;
			cloudEvent[attributeName] ??= value;
		}

		if (TryGetHeader(headers, TraceParentHeader, out var traceParent))
		{
			cloudEvent["traceparent"] ??= traceParent;
		}

		if (TryGetHeader(headers, CeTimeoutHeader, out var timeout))
		{
			cloudEvent["ce-timeout"] = timeout;
			cloudEvent[$"{DispatchPrefix}deadlineutc"] ??= timeout;
			cloudEvent[$"{DispatchPrefixWithoutSeparator}deadlineutc"] ??= timeout;
			cloudEvent["deadlineutc"] ??= timeout;
		}
	}

	private void RestorePartitionMetadata(CloudEvent cloudEvent, global::Confluent.Kafka.ConsumeResult<string, string> consumeResult)
	{
		var partition = consumeResult.Partition.Value.ToString(CultureInfo.InvariantCulture);
		var offset = consumeResult.Offset.Value.ToString(CultureInfo.InvariantCulture);

		cloudEvent[$"{DispatchPrefix}partitionid"] ??= partition;
		cloudEvent[$"{DispatchPrefixWithoutSeparator}partitionid"] ??= partition;
		cloudEvent["partitionid"] ??= partition;

		cloudEvent[$"{DispatchPrefix}offset"] ??= offset;
		cloudEvent[$"{DispatchPrefixWithoutSeparator}offset"] ??= offset;
		cloudEvent["offset"] ??= offset;

		cloudEvent[$"{DispatchPrefix}topic"] ??= consumeResult.Topic;
		cloudEvent[$"{DispatchPrefixWithoutSeparator}topic"] ??= consumeResult.Topic;
		cloudEvent["topic"] ??= consumeResult.Topic;

		if (!string.IsNullOrWhiteSpace(consumeResult.Message.Key))
		{
			var partitionKey = consumeResult.Message.Key;
			cloudEvent["partitionkey"] ??= partitionKey;
			cloudEvent[$"{DispatchPrefix}partitionkey"] ??= partitionKey;
			cloudEvent[$"{DispatchPrefixWithoutSeparator}partitionkey"] ??= partitionKey;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(KafkaEventId.CloudEventToTransportError, LogLevel.Error,
		"Failed to convert CloudEvent {EventId} to Kafka transport message")]
	private partial void LogToTransportMessageError(string eventId, Exception ex);

	[LoggerMessage(KafkaEventId.CloudEventFromTransportError, LogLevel.Error,
		"Failed to parse CloudEvent from Kafka transport message")]
	private partial void LogFromTransportMessageError(Exception ex);
}
