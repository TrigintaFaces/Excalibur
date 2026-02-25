// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mime;
using System.Text.Json;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TransportCompressionAlgorithm = Excalibur.Dispatch.Abstractions.Serialization.CompressionAlgorithm;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Maps CloudEvents to Google Pub/Sub <see cref="PubsubMessage" /> instances and vice versa.
/// </summary>
public sealed class GooglePubSubCloudEventAdapter : ICloudEventMapper<PubsubMessage>
{
	private const string CloudEventsStructuredContentType = "application/cloudevents+json";
	private const string StructuredContentTypeAttribute = "content-type";

	private const string CeSpecVersionAttribute = "ce-specversion";
	private const string CeTypeAttribute = "ce-type";
	private const string CeSourceAttribute = "ce-source";
	private const string CeIdAttribute = "ce-id";
	private const string CeTimeAttribute = "ce-time";
	private const string CeDataContentTypeAttribute = "ce-datacontenttype";
	private const string CeSubjectAttribute = "ce-subject";
	private const string CeDataSchemaAttribute = "ce-dataschema";
	private const string CeTimeoutAttribute = "ce-timeout";
	private const string TimeoutExtensionName = "timeout";

	private readonly JsonEventFormatter _jsonFormatter = new();
	private readonly ILogger<GooglePubSubCloudEventAdapter> _logger;
	private readonly GooglePubSubCloudEventOptions _pubSubOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="GooglePubSubCloudEventAdapter" /> class.
	/// </summary>
	/// <param name="options"> CloudEvent serialization options. </param>
	/// <param name="pubSubOptions"> Google Pub/Sub specific options. </param>
	/// <param name="logger"> Logger for diagnostics. </param>
	public GooglePubSubCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		GooglePubSubCloudEventOptions pubSubOptions,
		ILogger<GooglePubSubCloudEventAdapter> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(pubSubOptions);
		ArgumentNullException.ThrowIfNull(logger);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options), "Options.Value cannot be null");
		_pubSubOptions = pubSubOptions;
		_logger = logger;
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
		PubsubMessage transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		var (_, mode) = DetectModeWithDecodedMessage(transportMessage, cancellationToken);
		return ValueTask.FromResult(mode);
	}

	/// <inheritdoc />
	public Task<PubsubMessage> ToTransportMessageAsync(
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
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for Google Pub/Sub."),
		};

		ApplyStandardPubSubProperties(message, cloudEvent);
		ApplyDispatchEnvelopeAttributes(message.Attributes, cloudEvent);
		ApplyCompressionIfConfigured(message);

		_logger.LogDebug(
			"Converted CloudEvent {EventId} to Pub/Sub message {MessageId} using {Mode} mode",
			cloudEvent.Id,
			message.MessageId,
			mode);

		return Task.FromResult(message);
	}

	/// <inheritdoc />
	public async Task<CloudEvent> FromTransportMessageAsync(
		PubsubMessage transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		var (decodedMessage, mode) = DetectModeWithDecodedMessage(
			transportMessage,
			cancellationToken);
		mode ??= Options.DefaultMode;

		var cloudEvent = mode switch
		{
			CloudEventMode.Structured => await DecodeStructuredMessageAsync(decodedMessage).ConfigureAwait(false),
			CloudEventMode.Binary => DecodeBinaryMessage(decodedMessage),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for Google Pub/Sub."),
		};

		RestoreStandardProperties(decodedMessage, cloudEvent);
		RestoreDispatchEnvelopeProperties(cloudEvent, decodedMessage.Attributes);

		_logger.LogDebug(
			"Converted Pub/Sub message {MessageId} to CloudEvent {EventId} using {Mode} mode",
			decodedMessage.MessageId,
			cloudEvent.Id,
			mode);

		return cloudEvent;
	}

	private static (PubsubMessage Message, CloudEventMode? Mode) DetectModeWithDecodedMessage(
		PubsubMessage transportMessage,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var decodedMessage = GetDecodedMessage(transportMessage);

		if (IsStructuredMode(decodedMessage))
		{
			return (decodedMessage, CloudEventMode.Structured);
		}

		if (IsBinaryMode(decodedMessage))
		{
			return (decodedMessage, CloudEventMode.Binary);
		}

		return (decodedMessage, null);
	}

	private static PubsubMessage GetDecodedMessage(PubsubMessage message)
	{
		if (!GooglePubSubMessageBodyCodec.TryDecodeBody(message, out var decodedBody, out _))
		{
			return message;
		}

		var decodedMessage = message.Clone();
		decodedMessage.Data = decodedBody;
		return decodedMessage;
	}

	private static bool IsReservedCloudEventAttribute(string attributeName) =>
		attributeName.Equals(CeSpecVersionAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTypeAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeSourceAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeIdAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTimeAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeDataContentTypeAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeSubjectAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeDataSchemaAttribute, StringComparison.OrdinalIgnoreCase) ||
		attributeName.Equals(CeTimeoutAttribute, StringComparison.OrdinalIgnoreCase);

	private static bool IsStructuredMode(PubsubMessage message)
	{
		if (message.Attributes.TryGetValue(StructuredContentTypeAttribute, out var contentType) &&
			contentType.Contains("application/cloudevents", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		try
		{
			if (message.Data.IsEmpty)
			{
				return false;
			}

			using var document = JsonDocument.Parse(message.Data.ToStringUtf8());
			var root = document.RootElement;
			return root.TryGetProperty("specversion", out _)
				   && root.TryGetProperty("type", out _)
				   && root.TryGetProperty("source", out _)
				   && root.TryGetProperty("id", out _);
		}
		catch
		{
			return false;
		}
	}

	private static bool IsBinaryMode(PubsubMessage message) => message.Attributes.ContainsKey(CeSpecVersionAttribute)
															   && message.Attributes.ContainsKey(CeTypeAttribute)
															   && message.Attributes.ContainsKey(CeSourceAttribute)
															   && message.Attributes.ContainsKey(CeIdAttribute);

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	private static ByteString ConvertToByteString(object? data) => data switch
	{
		null => ByteString.Empty,
		ByteString byteString => byteString,
		byte[] bytes => ByteString.CopyFrom(bytes),
		ReadOnlyMemory<byte> memory => ByteString.CopyFrom(memory.Span),
		string text => ByteString.CopyFromUtf8(text),
		_ => ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(data)),
	};

	private static object DeserializeMessageData(ByteString data, string? contentType)
	{
		if (data.IsEmpty)
		{
			return string.Empty;
		}

		return contentType?.ToUpperInvariant() switch
		{
			"APPLICATION/JSON" or "APPLICATION/CLOUDEVENTS+JSON" => JsonDocument.Parse(data.ToStringUtf8()).RootElement,
			_ => data.ToStringUtf8(),
		};
	}

	private static void ApplyBinaryModeAttributes(IDictionary<string, string> attributes, CloudEvent cloudEvent)
	{
		attributes[CeSpecVersionAttribute] = cloudEvent.SpecVersion.VersionId;

		if (!string.IsNullOrWhiteSpace(cloudEvent.Type))
		{
			attributes[CeTypeAttribute] = cloudEvent.Type!;
		}

		if (cloudEvent.Source is not null)
		{
			attributes[CeSourceAttribute] = cloudEvent.Source.ToString();
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Id))
		{
			attributes[CeIdAttribute] = cloudEvent.Id!;
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.DataContentType))
		{
			attributes[CeDataContentTypeAttribute] = cloudEvent.DataContentType!;
		}

		if (!string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			attributes[CeSubjectAttribute] = cloudEvent.Subject!;
		}

		if (cloudEvent.DataSchema is not null)
		{
			attributes[CeDataSchemaAttribute] = cloudEvent.DataSchema.ToString();
		}

		if (cloudEvent.Time.HasValue)
		{
			attributes[CeTimeAttribute] = cloudEvent.Time.Value.ToString("O", CultureInfo.InvariantCulture);
		}

		foreach (var extension in cloudEvent.ExtensionAttributes)
		{
			var value = cloudEvent[extension.Name];
			if (value is null)
			{
				continue;
			}

			attributes[$"ce-{extension.Name}"] = value.ToString()!;
		}
	}

	private PubsubMessage CreateStructuredMessage(CloudEvent cloudEvent)
	{
		var encoded = _jsonFormatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
		var message = new PubsubMessage { Data = ByteString.CopyFrom(encoded.ToArray()) };

		message.Attributes[StructuredContentTypeAttribute] = contentType?.ToString() ?? CloudEventsStructuredContentType;

		return message;
	}

	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Transport.Google.GooglePubSubCloudEventAdapter.ConvertToByteString(Object)")]
	private PubsubMessage CreateBinaryMessage(CloudEvent cloudEvent)
	{
		var message = new PubsubMessage { Data = ConvertToByteString(cloudEvent.Data) };

		ApplyBinaryModeAttributes(message.Attributes, cloudEvent);

		return message;
	}

	private void ApplyCompressionIfConfigured(PubsubMessage message)
	{
		if (!_pubSubOptions.EnableCompression || message.Data.IsEmpty)
		{
			return;
		}

		var threshold = _pubSubOptions.CompressionThreshold;
		if (threshold < 0)
		{
			threshold = 0;
		}

		if (message.Data.Length < threshold)
		{
			return;
		}

		var compressed = GooglePubSubCompression.Compress(
			message.Data.Span,
			TransportCompressionAlgorithm.Gzip);
		message.Data = ByteString.CopyFrom(compressed);
		message.Attributes[GooglePubSubMessageAttributes.Compression] =
			TransportCompressionAlgorithm.Gzip.ToString();
	}

	private void ApplyStandardPubSubProperties(PubsubMessage message, CloudEvent cloudEvent)
	{
		message.MessageId = cloudEvent.Id ?? Guid.NewGuid().ToString();

		if (_pubSubOptions.UseOrderingKeys)
		{
			var partitionKey = GetStringAttribute(cloudEvent, "partitionkey");
			if (!string.IsNullOrWhiteSpace(partitionKey))
			{
				message.OrderingKey = partitionKey;
			}
		}

		if (cloudEvent.Time.HasValue)
		{
			message.Attributes[CeTimeAttribute] = cloudEvent.Time.Value.ToString("O", CultureInfo.InvariantCulture);
		}

		var timeout = GetStringAttribute(cloudEvent, TimeoutExtensionName);
		if (!string.IsNullOrWhiteSpace(timeout))
		{
			message.Attributes[CeTimeoutAttribute] = timeout;
		}
	}

	private void ApplyDispatchEnvelopeAttributes(IDictionary<string, string> attributes, CloudEvent cloudEvent)
	{
		AddDispatchExtension(attributes, cloudEvent, "correlationid");
		AddDispatchExtension(attributes, cloudEvent, "tenantid");
		AddDispatchExtension(attributes, cloudEvent, "userid");
		AddDispatchExtension(attributes, cloudEvent, "traceparent");
		AddDispatchExtension(attributes, cloudEvent, "deliverycount");
		AddDispatchExtension(attributes, cloudEvent, "scheduledtime");
	}

	private void AddDispatchExtension(IDictionary<string, string> attributes, CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[DispatchPrefix + attributeName]
					?? cloudEvent[DispatchPrefixWithoutSeparator + attributeName]
					?? cloudEvent[attributeName];

		if (value is null)
		{
			return;
		}

		attributes[DispatchPrefix + attributeName] = value.ToString()!;
	}

	private async Task<CloudEvent> DecodeStructuredMessageAsync(PubsubMessage message)
	{
		if (message.Data.IsEmpty)
		{
			throw new InvalidOperationException("Structured CloudEvent message payload cannot be empty.");
		}

		using var stream = new MemoryStream(message.Data.ToByteArray());
		var contentType = new ContentType(message.Attributes.TryGetValue(StructuredContentTypeAttribute, out var value)
			? value
			: CloudEventsStructuredContentType);

		return await _jsonFormatter
			.DecodeStructuredModeMessageAsync(stream, contentType, null)
			.ConfigureAwait(false);
	}

	private CloudEvent DecodeBinaryMessage(PubsubMessage message)
	{
		if (!message.Attributes.TryGetValue(CeSpecVersionAttribute, out var specVersion) ||
			string.IsNullOrWhiteSpace(specVersion))
		{
			specVersion = Options.SpecVersion.VersionId;
		}

		if (!message.Attributes.TryGetValue(CeTypeAttribute, out var type) ||
			!message.Attributes.TryGetValue(CeSourceAttribute, out var source) ||
			!message.Attributes.TryGetValue(CeIdAttribute, out var id))
		{
			throw new InvalidOperationException("Binary CloudEvent message missing required attributes.");
		}

		var spec = specVersion switch
		{
			"1.0" => CloudEventsSpecVersion.V1_0,
			_ => Options.SpecVersion,
		};

		var cloudEvent = new CloudEvent(spec) { Type = type, Source = new Uri(source, UriKind.RelativeOrAbsolute), Id = id };

		if (message.Attributes.TryGetValue(CeTimeAttribute, out var timeValue) &&
			DateTimeOffset.TryParse(timeValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
		{
			cloudEvent.Time = time;
		}

		if (message.Attributes.TryGetValue(CeDataContentTypeAttribute, out var contentType))
		{
			cloudEvent.DataContentType = contentType;
		}

		if (message.Attributes.TryGetValue(CeSubjectAttribute, out var subject))
		{
			cloudEvent.Subject = subject;
		}

		if (message.Attributes.TryGetValue(CeDataSchemaAttribute, out var schema) &&
			Uri.TryCreate(schema, UriKind.RelativeOrAbsolute, out var schemaUri))
		{
			cloudEvent.DataSchema = schemaUri;
		}

		foreach (var attribute in message.Attributes)
		{
			if (!attribute.Key.StartsWith("ce-", StringComparison.OrdinalIgnoreCase) ||
				IsReservedCloudEventAttribute(attribute.Key))
			{
				continue;
			}

			var extensionName = attribute.Key[3..];
			cloudEvent[extensionName] = attribute.Value;
		}

		if (!message.Data.IsEmpty)
		{
			cloudEvent.Data = DeserializeMessageData(message.Data, cloudEvent.DataContentType);
		}

		return cloudEvent;
	}

	private void RestoreStandardProperties(PubsubMessage message, CloudEvent cloudEvent)
	{
		if (message.Attributes.TryGetValue(CeTimeoutAttribute, out var timeout))
		{
			cloudEvent[TimeoutExtensionName] = timeout;
		}

		if (!string.IsNullOrWhiteSpace(message.OrderingKey))
		{
			// CloudEvent extension names cannot contain '-'. Preserve dispatch metadata
			// using the normalized extension key and the plain key.
			cloudEvent[$"{DispatchPrefixWithoutSeparator}partitionkey"] ??= message.OrderingKey;
			cloudEvent["partitionkey"] ??= message.OrderingKey;
		}
	}

	private void RestoreDispatchEnvelopeProperties(CloudEvent cloudEvent, IDictionary<string, string> attributes)
	{
		foreach (var attribute in attributes)
		{
			if (!attribute.Key.StartsWith(DispatchPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var extensionName = attribute.Key[DispatchPrefix.Length..];
			cloudEvent[DispatchPrefixWithoutSeparator + extensionName] ??= attribute.Value;
			cloudEvent[extensionName] ??= attribute.Value;
		}
	}

	private string? GetStringAttribute(CloudEvent cloudEvent, string attributeName)
	{
		var value = cloudEvent[attributeName]
					?? cloudEvent[DispatchPrefix + attributeName]
					?? cloudEvent[DispatchPrefixWithoutSeparator + attributeName];

		return value switch
		{
			null => null,
			string text => text,
			_ => value.ToString(),
		};
	}
}
