// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Default implementation of RabbitMQ CloudEvent adapter.
/// </summary>
public sealed class RabbitMqCloudEventAdapter : IRabbitMqCloudEventAdapter
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
	private const string TimeoutAttributeName = "timeout";
	private const string DispatchDeadlineAttribute = "deadlineutc";
	private const string DispatchHeaderSuffix = "header";

	private readonly JsonEventFormatter _jsonFormatter = new();
	private readonly ILogger<RabbitMqCloudEventAdapter> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqCloudEventAdapter" /> class.
	/// </summary>
	/// <param name="options"> CloudEvent configuration options. </param>
	/// <param name="rabbitMqOptions"> RabbitMQ-specific CloudEvent options. </param>
	/// <param name="logger"> Logger for diagnostics. </param>
	public RabbitMqCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		IOptions<RabbitMqCloudEventOptions>? rabbitMqOptions,
		ILogger<RabbitMqCloudEventAdapter> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger;
		RabbitMqOptions = rabbitMqOptions?.Value ?? new RabbitMqCloudEventOptions();
	}

	/// <inheritdoc />
	public CloudEventOptions Options { get; }

	private RabbitMqCloudEventOptions RabbitMqOptions { get; }

	private string DispatchPrefix => string.IsNullOrWhiteSpace(Options.DispatchExtensionPrefix)
		? "dispatch-"
		: Options.DispatchExtensionPrefix.EndsWith('-')
			? Options.DispatchExtensionPrefix
			: Options.DispatchExtensionPrefix + "-";

	private string DispatchPrefixWithoutSeparator => string.IsNullOrWhiteSpace(Options.DispatchExtensionPrefix)
		? "dispatch"
		: Options.DispatchExtensionPrefix.Replace("-", string.Empty, StringComparison.Ordinal);

	private string DispatchHeaderPrefix => DispatchPrefixWithoutSeparator + DispatchHeaderSuffix;

	/// <inheritdoc />
	public static ValueTask<CloudEventMode?> TryDetectMode(
		(IBasicProperties properties, ReadOnlyMemory<byte> body) transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage.properties);
		cancellationToken.ThrowIfCancellationRequested();

		if (IsStructuredMode(transportMessage.properties))
		{
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Structured);
		}

		if (IsBinaryMode(transportMessage.properties))
		{
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Binary);
		}

		return ValueTask.FromResult<CloudEventMode?>(null);
	}

	/// <inheritdoc />
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public Task<(IBasicProperties properties, ReadOnlyMemory<byte> body)> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var effectiveMode = mode is CloudEventMode.Binary or CloudEventMode.Structured
			? mode
			: Options.DefaultMode;

		var properties = new CloudEventBasicProperties
		{
			Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
			DeliveryMode = (DeliveryModes)(RabbitMqOptions.Persistence == RabbitMqPersistence.Persistent ? (byte)2 : (byte)1),
		};

		ApplyStandardProperties(properties, cloudEvent);

		var body = effectiveMode switch
		{
			CloudEventMode.Structured => EncodeStructuredMessage(cloudEvent, properties),
			CloudEventMode.Binary => EncodeBinaryMessage(cloudEvent, properties),
			_ => throw new NotSupportedException($"CloudEvent mode '{effectiveMode}' is not supported for RabbitMQ."),
		};

		ApplyTimeoutHeader(properties.Headers, cloudEvent);
		ApplyDispatchEnvelopeHeaders(properties, cloudEvent);
		ApplyDispatchHeaderAttributes(properties.Headers, cloudEvent);

		_logger.LogDebug(
			"Converted CloudEvent {EventId} to RabbitMQ transport message using {Mode} mode",
			cloudEvent.Id,
			effectiveMode);

		return Task.FromResult(((IBasicProperties)properties, body));
	}

	/// <inheritdoc />
	public async Task<CloudEvent> FromTransportMessageAsync(
		(IBasicProperties properties, ReadOnlyMemory<byte> body) transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage.properties);
		cancellationToken.ThrowIfCancellationRequested();

		var detectedMode = await TryDetectMode(transportMessage, cancellationToken).ConfigureAwait(false)
						   ?? Options.DefaultMode;

		var cloudEvent = detectedMode switch
		{
			CloudEventMode.Structured => DecodeStructuredMessage(transportMessage.properties, transportMessage.body),
			CloudEventMode.Binary => DecodeBinaryMessage(transportMessage.properties, transportMessage.body),
			_ => throw new NotSupportedException($"CloudEvent mode '{detectedMode}' is not supported for RabbitMQ."),
		};

		MapRabbitPropertiesToCloudEvent(transportMessage.properties, cloudEvent);
		RestoreDispatchEnvelopeProperties(cloudEvent, transportMessage.properties.Headers);
		RestoreDispatchHeaderAttributes(cloudEvent, transportMessage.properties.Headers);

		_logger.LogDebug(
			"Converted RabbitMQ transport message {MessageId} to CloudEvent {EventId} using {Mode} mode",
			transportMessage.properties.MessageId,
			cloudEvent.Id,
			detectedMode);

		return cloudEvent;
	}

	/// <inheritdoc />
	public ValueTask<CloudEventMode?> TryDetectMode(
		IBasicProperties properties,
		ReadOnlyMemory<byte> body,
		CancellationToken cancellationToken) => TryDetectMode((properties, body), cancellationToken);

	/// <inheritdoc />
	public bool IsValidCloudEventMessage(IBasicProperties properties, ReadOnlyMemory<byte> body)
	{
		ArgumentNullException.ThrowIfNull(properties);

		return IsStructuredMode(properties) || IsBinaryMode(properties);
	}

	private static string? GetAttributeValue(CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[attributeName];
		return value switch
		{
			null => null,
			string text => text,
			JsonElement json => json.ValueKind == JsonValueKind.String ? json.GetString() : json.ToString(),
			_ => value.ToString(),
		};
	}

	private static object ConvertValueToHeader(object value) => value switch
	{
		JsonElement jsonElement => jsonElement.ValueKind == JsonValueKind.String
			? jsonElement.GetString() ?? string.Empty
			: jsonElement.ToString(),
		byte[] or ReadOnlyMemory<byte> => value,
		_ => value.ToString() ?? string.Empty,
	};

	private static string? ConvertHeaderToString(object? headerValue) => headerValue switch
	{
		null => null,
		byte[] bytes => Encoding.UTF8.GetString(bytes),
		ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
		string text => text,
		IConvertible convertible => convertible.ToString(CultureInfo.InvariantCulture),
		_ => headerValue.ToString(),
	};

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static ReadOnlyMemory<byte> EncodeBody(object? data, string? contentType)
	{
		if (data is null)
		{
			return ReadOnlyMemory<byte>.Empty;
		}

		return data switch
		{
			byte[] bytes => bytes,
			ReadOnlyMemory<byte> memory => memory,
			string text => Encoding.UTF8.GetBytes(text),
			JsonElement jsonElement => Encoding.UTF8.GetBytes(jsonElement.GetRawText()),
			_ when string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
				=> Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)),
			_ => Encoding.UTF8.GetBytes(data.ToString() ?? string.Empty),
		};
	}

	private static object DecodeBody(ReadOnlyMemory<byte> body, string? contentType)
	{
		if (body.IsEmpty)
		{
			return string.Empty;
		}

		if (string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
		{
			return JsonDocument.Parse(body).RootElement.Clone();
		}

		return Encoding.UTF8.GetString(body.Span);
	}

	private static bool IsStructuredMode(IBasicProperties properties) =>
		!string.IsNullOrWhiteSpace(properties.ContentType) &&
		properties.ContentType.Contains("application/cloudevents", StringComparison.OrdinalIgnoreCase);

	private static bool IsBinaryMode(IBasicProperties properties)
	{
		if (properties.Headers is null)
		{
			return false;
		}

		return properties.Headers.ContainsKey(CeSpecVersionHeader) &&
			   properties.Headers.ContainsKey(CeTypeHeader) &&
			   properties.Headers.ContainsKey(CeSourceHeader) &&
			   properties.Headers.ContainsKey(CeIdHeader);
	}

	private static bool IsStandardCloudEventAttribute(string attributeName) =>
		attributeName.Equals(CeSpecVersionHeader, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTypeHeader, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeSourceHeader, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeIdHeader, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTimeHeader, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeDataContentTypeHeader, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeSubjectHeader, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeDataSchemaHeader, StringComparison.OrdinalIgnoreCase);

	private static void ApplyStandardProperties(CloudEventBasicProperties properties, CloudEvent cloudEvent)
	{
		properties.MessageId = cloudEvent.Id ?? Guid.NewGuid().ToString();
		properties.Timestamp = new AmqpTimestamp((cloudEvent.Time ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds());

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			properties.Type = cloudEvent.Subject;
		}

		var correlationId = GetAttributeValue(cloudEvent, "correlationid");
		if (!string.IsNullOrWhiteSpace(correlationId))
		{
			properties.CorrelationId = correlationId;
		}

		var traceParent = GetAttributeValue(cloudEvent, TraceParentHeader);
		if (!string.IsNullOrWhiteSpace(traceParent))
		{
			properties.Headers ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
			properties.Headers[TraceParentHeader] = traceParent;
			properties.CorrelationId ??= traceParent;
		}

		var replyTo = GetAttributeValue(cloudEvent, "replyto");
		if (!string.IsNullOrWhiteSpace(replyTo))
		{
			properties.ReplyTo = replyTo;
		}
	}

	private ReadOnlyMemory<byte> EncodeStructuredMessage(CloudEvent cloudEvent, CloudEventBasicProperties properties)
	{
		var encoded = _jsonFormatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
		var payload = encoded.ToArray();
		properties.ContentType = contentType?.ToString() ?? StructuredContentType;
		properties.Headers[ContentTypeHeader] = properties.ContentType;
		return payload;
	}

	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Transport.RabbitMQ.CloudEvents.RabbitMqCloudEventAdapter.EncodeBody(Object, String)")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Transport.RabbitMQ.CloudEvents.RabbitMqCloudEventAdapter.EncodeBody(Object, String)")]
	private ReadOnlyMemory<byte> EncodeBinaryMessage(CloudEvent cloudEvent, CloudEventBasicProperties properties)
	{
		var headers = properties.Headers;

		headers[CeSpecVersionHeader] = cloudEvent.SpecVersion.VersionId;

		if (!string.IsNullOrWhiteSpace(cloudEvent.Type))
		{
			headers[CeTypeHeader] = cloudEvent.Type;
		}

		if (cloudEvent.Source is not null)
		{
			headers[CeSourceHeader] = cloudEvent.Source.ToString();
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Id))
		{
			headers[CeIdHeader] = cloudEvent.Id;
		}

		if (cloudEvent.Time.HasValue)
		{
			headers[CeTimeHeader] = cloudEvent.Time.Value.ToString("O", CultureInfo.InvariantCulture);
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			headers[CeSubjectHeader] = cloudEvent.Subject;
		}

		if (cloudEvent.DataSchema is not null)
		{
			headers[CeDataSchemaHeader] = cloudEvent.DataSchema.ToString();
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
		{
			headers[CeDataContentTypeHeader] = cloudEvent.DataContentType;
			properties.ContentType = cloudEvent.DataContentType;
		}
		else if (string.IsNullOrWhiteSpace(properties.ContentType))
		{
			properties.ContentType = "application/json";
		}

		foreach (var attribute in cloudEvent.GetPopulatedAttributes())
		{
			var attributeName = attribute.Key.Name;
			if (IsStandardCloudEventAttribute(attributeName) || attributeName.StartsWith(
					DispatchHeaderPrefix,
					StringComparison.Ordinal))
			{
				continue;
			}

			var value = cloudEvent[attributeName];
			if (value is null)
			{
				continue;
			}

			var headerName = attributeName.StartsWith(CePrefix, StringComparison.OrdinalIgnoreCase)
				? attributeName
				: $"{CePrefix}{attributeName}";

			if (!headers.ContainsKey(headerName))
			{
				headers[headerName] = ConvertValueToHeader(value);
			}
		}

		return EncodeBody(cloudEvent.Data, properties.ContentType);
	}

	private CloudEvent DecodeStructuredMessage(IBasicProperties properties, ReadOnlyMemory<byte> body)
	{
		if (body.IsEmpty)
		{
			throw new InvalidOperationException("Structured CloudEvent message body cannot be empty.");
		}

		var contentType = string.IsNullOrWhiteSpace(properties.ContentType)
			? new ContentType(StructuredContentType)
			: new ContentType(properties.ContentType);

		return _jsonFormatter.DecodeStructuredModeMessage(body.ToArray(), contentType, extensionAttributes: null);
	}

	private CloudEvent DecodeBinaryMessage(IBasicProperties properties, ReadOnlyMemory<byte> body)
	{
		var headers = properties.Headers ?? new Dictionary<string, object?>(StringComparer.Ordinal);

		if (!headers.TryGetValue(CeSpecVersionHeader, out var specVersionObj) ||
			!headers.TryGetValue(CeTypeHeader, out var typeObj) ||
			!headers.TryGetValue(CeSourceHeader, out var sourceObj) ||
			!headers.TryGetValue(CeIdHeader, out var idObj))
		{
			throw new InvalidOperationException(
				$"RabbitMQ message '{properties.MessageId}' is missing required CloudEvent headers.");
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

		if (headers.TryGetValue(CeTimeHeader, out var timeObj) &&
			DateTimeOffset.TryParse(ConvertHeaderToString(timeObj), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
		{
			cloudEvent.Time = time;
		}

		if (headers.TryGetValue(CeSubjectHeader, out var subjectObj))
		{
			cloudEvent.Subject = ConvertHeaderToString(subjectObj);
		}

		var contentType = properties.ContentType;
		if (headers.TryGetValue(CeDataContentTypeHeader, out var dataContentTypeObj))
		{
			contentType = ConvertHeaderToString(dataContentTypeObj);
		}

		if (!string.IsNullOrWhiteSpace(contentType))
		{
			cloudEvent.DataContentType = contentType;
		}

		if (headers.TryGetValue(CeDataSchemaHeader, out var schemaObj) &&
			Uri.TryCreate(ConvertHeaderToString(schemaObj), UriKind.Absolute, out var schema))
		{
			cloudEvent.DataSchema = schema;
		}

		foreach (var header in headers)
		{
			if (!header.Key.StartsWith(CePrefix, StringComparison.OrdinalIgnoreCase) ||
				IsStandardCloudEventAttribute(header.Key))
			{
				continue;
			}

			var attributeName = header.Key[CePrefix.Length..];
			cloudEvent[attributeName] = ConvertHeaderToString(header.Value);
		}

		if (!body.IsEmpty)
		{
			cloudEvent.Data = DecodeBody(body, cloudEvent.DataContentType);
		}

		return cloudEvent;
	}

	private void ApplyDispatchEnvelopeHeaders(CloudEventBasicProperties properties, CloudEvent cloudEvent)
	{
		var headers = properties.Headers ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

		AddDispatchHeader(headers, cloudEvent, "correlationid");
		AddDispatchHeader(headers, cloudEvent, "tenantid");
		AddDispatchHeader(headers, cloudEvent, "userid");
		AddDispatchHeader(headers, cloudEvent, "traceparent");
		AddDispatchHeader(headers, cloudEvent, "deliverycount");
		AddDispatchHeader(headers, cloudEvent, "scheduledtime");
	}

	private void ApplyDispatchHeaderAttributes(IDictionary<string, object?> headers, CloudEvent cloudEvent)
	{
		foreach (var attribute in cloudEvent.GetPopulatedAttributes())
		{
			var attributeName = attribute.Key.Name;
			if (!attributeName.StartsWith(DispatchHeaderPrefix, StringComparison.Ordinal))
			{
				continue;
			}

			var headerName = attributeName[DispatchHeaderPrefix.Length..];
			if (string.IsNullOrWhiteSpace(headerName) || headers.ContainsKey(headerName))
			{
				continue;
			}

			var value = cloudEvent[attributeName]?.ToString();
			if (string.IsNullOrWhiteSpace(value))
			{
				continue;
			}

			headers[headerName] = value;
		}
	}

	private void MapRabbitPropertiesToCloudEvent(IBasicProperties properties, CloudEvent cloudEvent)
	{
		if (string.IsNullOrWhiteSpace(cloudEvent.Subject) && !string.IsNullOrWhiteSpace(properties.Type))
		{
			cloudEvent.Subject = properties.Type;
		}

		if (!cloudEvent.Time.HasValue && properties.Timestamp.UnixTime > 0)
		{
			cloudEvent.Time = DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime);
		}

		if (!string.IsNullOrWhiteSpace(properties.CorrelationId))
		{
			cloudEvent[$"{DispatchPrefix}correlationid"] ??= properties.CorrelationId;
			cloudEvent["correlationid"] ??= properties.CorrelationId;
		}

		if (!string.IsNullOrWhiteSpace(properties.ReplyTo))
		{
			cloudEvent[$"{DispatchPrefix}replyto"] ??= properties.ReplyTo;
			cloudEvent["replyto"] ??= properties.ReplyTo;
		}

		if (!string.IsNullOrWhiteSpace(properties.ContentType) && string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
		{
			cloudEvent.DataContentType = properties.ContentType;
		}

		if (properties.Headers != null && properties.Headers.TryGetValue(TraceParentHeader, out var traceParentObj))
		{
			var traceParent = ConvertHeaderToString(traceParentObj);
			if (!string.IsNullOrWhiteSpace(traceParent))
			{
				cloudEvent[$"{DispatchPrefix}traceparent"] ??= traceParent;
				cloudEvent[TraceParentHeader] ??= traceParent;
			}
		}
	}

	private void RestoreDispatchEnvelopeProperties(CloudEvent cloudEvent, IDictionary<string, object?>? headers)
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

			var value = ConvertHeaderToString(header.Value);
			if (value is null)
			{
				continue;
			}

			cloudEvent[header.Key] = value;

			var alternateName = header.Key.Replace("-", string.Empty, StringComparison.Ordinal);
			cloudEvent[alternateName] ??= value;
		}
	}

	private void RestoreDispatchHeaderAttributes(CloudEvent cloudEvent, IDictionary<string, object?>? headers)
	{
		if (headers is null)
		{
			return;
		}

		foreach (var header in headers)
		{
			if (header.Key.StartsWith(CePrefix, StringComparison.OrdinalIgnoreCase) ||
				header.Key.StartsWith(DispatchPrefix, StringComparison.OrdinalIgnoreCase))
			{
				if (header.Key.Equals(CeTimeoutHeader, StringComparison.OrdinalIgnoreCase))
				{
					var timeoutValue = ConvertHeaderToString(header.Value);
					if (!string.IsNullOrWhiteSpace(timeoutValue))
					{
						cloudEvent[CeTimeoutHeader] = timeoutValue;
						cloudEvent[TimeoutAttributeName] ??= timeoutValue;
						cloudEvent[DispatchPrefix + DispatchDeadlineAttribute] ??= timeoutValue;
					}
				}

				continue;
			}

			var value = ConvertHeaderToString(header.Value);
			if (string.IsNullOrWhiteSpace(value))
			{
				continue;
			}

			var attributeName = DispatchHeaderPrefix + header.Key.ToUpperInvariant();
			cloudEvent[attributeName] = value;
		}
	}

	private void ApplyTimeoutHeader(IDictionary<string, object?> headers, CloudEvent cloudEvent)
	{
		var timeout = GetAttributeValue(cloudEvent, CeTimeoutHeader)
					  ?? GetAttributeValue(cloudEvent, TimeoutAttributeName)
					  ?? GetAttributeValue(cloudEvent, DispatchDeadlineAttribute)
					  ?? GetAttributeValue(cloudEvent, DispatchPrefix + DispatchDeadlineAttribute);

		if (!string.IsNullOrWhiteSpace(timeout))
		{
			headers[CeTimeoutHeader] = timeout;
		}
	}

	private void AddDispatchHeader(IDictionary<string, object?> headers, CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[DispatchPrefix + attributeName]
					?? cloudEvent[DispatchPrefixWithoutSeparator + attributeName]
					?? cloudEvent[attributeName];
		if (value is null)
		{
			return;
		}

		headers[DispatchPrefix + attributeName] = value.ToString() ?? string.Empty;
	}
}
