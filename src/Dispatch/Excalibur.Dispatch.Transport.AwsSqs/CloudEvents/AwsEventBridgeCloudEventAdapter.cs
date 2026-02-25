// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Amazon.EventBridge.Model;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS EventBridge implementation of <see cref="ICloudEventMapper{TTransportMessage}" />.
/// </summary>
public sealed class AwsEventBridgeCloudEventAdapter : ICloudEventMapper<PutEventsRequestEntry>
{
	private const string SpecVersionProperty = "specversion";
	private const string TypeProperty = "type";
	private const string SourceProperty = "source";
	private const string IdProperty = "id";
	private const string TimeProperty = "time";
	private const string SubjectProperty = "subject";
	private const string DataContentTypeProperty = "datacontenttype";
	private const string DataSchemaProperty = "dataschema";
	private const string TimeoutProperty = "timeout";
	private const string TraceParentProperty = "traceparent";
	private const string DataProperty = "data";

	private readonly ILogger<AwsEventBridgeCloudEventAdapter> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsEventBridgeCloudEventAdapter" /> class.
	/// </summary>
	/// <param name="options"> </param>
	/// <param name="logger"> </param>
	public AwsEventBridgeCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		ILogger<AwsEventBridgeCloudEventAdapter> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger;
	}

	/// <inheritdoc />
	public CloudEventOptions Options { get; }

	/// <inheritdoc />
	public static ValueTask<CloudEventMode?> TryDetectMode(
		PutEventsRequestEntry transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(transportMessage.Detail))
		{
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Binary);
		}

		try
		{
			using var document = JsonDocument.Parse(transportMessage.Detail);
			var root = document.RootElement;
			if (root.TryGetProperty(SpecVersionProperty, out _) &&
				root.TryGetProperty(TypeProperty, out _) &&
				root.TryGetProperty(IdProperty, out _))
			{
				return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Structured);
			}
		}
		catch (JsonException)
		{
			// Treat invalid JSON as binary payload
			return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Binary);
		}

		return ValueTask.FromResult<CloudEventMode?>(CloudEventMode.Binary);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("CloudEvent serialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("CloudEvent serialization uses reflection to dynamically access and serialize types")]
	public Task<PutEventsRequestEntry> ToTransportMessageAsync(
		CloudEvent cloudEvent,
		CloudEventMode mode,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var entry = new PutEventsRequestEntry
		{
			EventBusName = string.Empty,
			DetailType = cloudEvent.Type ?? "CloudEvent",
			Source = cloudEvent.Source?.ToString() ?? Options.DefaultSource.ToString(),
			Resources = string.IsNullOrWhiteSpace(cloudEvent.Subject)
				? new List<string>()
				: new List<string> { cloudEvent.Subject },
		};

		var detailPayload = BuildDetailPayload(cloudEvent, mode);
		entry.Detail = JsonSerializer.Serialize(detailPayload, JsonSerializerOptionsProvider.Options);

		_logger.LogDebug(
			"Converted CloudEvent {EventId} to EventBridge request entry using {Mode} mode",
			cloudEvent.Id,
			mode);

		return Task.FromResult(entry);
	}

	/// <inheritdoc />
	public async Task<CloudEvent> FromTransportMessageAsync(
		PutEventsRequestEntry transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		cancellationToken.ThrowIfCancellationRequested();

		var mode = await TryDetectMode(transportMessage, cancellationToken).ConfigureAwait(false)
				   ?? CloudEventMode.Structured;

		var cloudEvent = mode switch
		{
			CloudEventMode.Structured => ParseStructuredDetail(transportMessage),
			CloudEventMode.Binary => ParseBinaryDetail(transportMessage),
			_ => throw new NotSupportedException($"CloudEvent mode '{mode}' is not supported for EventBridge."),
		};

		if (transportMessage.Resources is { Count: > 0 } && string.IsNullOrWhiteSpace(cloudEvent.Subject))
		{
			cloudEvent.Subject = transportMessage.Resources[0];
		}

		_logger.LogDebug(
			"Converted EventBridge request entry to CloudEvent {EventId} using {Mode} mode",
			cloudEvent.Id,
			mode);

		return cloudEvent;
	}

	/// <summary>
	/// Convenience API to create an EventBridge request entry for a specific event bus.
	/// </summary>
	/// <param name="cloudEvent"> </param>
	/// <param name="eventBusName"> </param>
	/// <param name="cancellationToken"> </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task<PutEventsRequestEntry> ToEventBridgeEventAsync(
		CloudEvent cloudEvent,
		string eventBusName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventBusName);

		var entry = await ToTransportMessageAsync(cloudEvent, Options.DefaultMode, cancellationToken).ConfigureAwait(false);
		entry.EventBusName = eventBusName;
		return entry;
	}

	/// <summary>
	/// Convenience API to map a transport entry back to a CloudEvent, returning <c> null </c> on failure.
	/// </summary>
	/// <param name="eventBridgeEvent"> </param>
	/// <param name="cancellationToken"> </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public async Task<CloudEvent?> FromEventBridgeEventAsync(
		PutEventsRequestEntry eventBridgeEvent,
		CancellationToken cancellationToken)
	{
		try
		{
			return await FromTransportMessageAsync(eventBridgeEvent, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to convert EventBridge request entry to CloudEvent");
			return null;
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static Dictionary<string, object?> BuildDetailPayload(CloudEvent cloudEvent, CloudEventMode mode)
	{
		var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			[SpecVersionProperty] = cloudEvent.SpecVersion?.VersionId ?? CloudEventsSpecVersion.V1_0.VersionId,
			[TypeProperty] = cloudEvent.Type ?? "CloudEvent",
			[SourceProperty] = cloudEvent.Source?.ToString(),
			[IdProperty] = cloudEvent.Id ?? Guid.NewGuid().ToString(),
			[TimeProperty] = (cloudEvent.Time ?? DateTimeOffset.UtcNow).ToString("O"),
			[SubjectProperty] = cloudEvent.Subject,
			[DataContentTypeProperty] = cloudEvent.DataContentType,
			[DataSchemaProperty] = cloudEvent.DataSchema?.ToString(),
			[TimeoutProperty] = cloudEvent[TimeoutProperty]?.ToString() ?? cloudEvent[$"dispatch-{TimeoutProperty}"]?.ToString(),
			[TraceParentProperty] = cloudEvent[TraceParentProperty]?.ToString(),
		};

		if (mode == CloudEventMode.Structured)
		{
			payload[DataProperty] = cloudEvent.Data;
		}
		else
		{
			payload[DataProperty] = cloudEvent.Data switch
			{
				null => null,
				string text => text,
				byte[] bytes => Convert.ToBase64String(bytes),
				_ => JsonSerializer.Serialize(cloudEvent.Data),
			};
		}

		return payload;
	}

	private static Uri? TryParseUri(JsonElement root, string propertyName)
	{
		if (root.TryGetProperty(propertyName, out var valueElement) &&
			Uri.TryCreate(valueElement.GetString(), UriKind.RelativeOrAbsolute, out var uri))
		{
			return uri;
		}

		return null;
	}

	private static Uri? TryParseUri(string? value, Uri fallback) =>
		Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri) ? uri : fallback;

	private CloudEvent ParseStructuredDetail(PutEventsRequestEntry entry)
	{
		if (string.IsNullOrWhiteSpace(entry.Detail))
		{
			return CreateDefaultCloudEvent(entry);
		}

		using var document = JsonDocument.Parse(entry.Detail);
		var root = document.RootElement;

		var cloudEvent = new CloudEvent
		{
			Source = TryParseUri(root, SourceProperty) ?? TryParseUri(entry.Source, Options.DefaultSource),
			Type = root.TryGetProperty(TypeProperty, out var typeElement)
				? typeElement.GetString() ?? entry.DetailType
				: entry.DetailType,
			Id = root.TryGetProperty(IdProperty, out var idElement)
				? idElement.GetString() ?? Guid.NewGuid().ToString()
				: Guid.NewGuid().ToString(),
			Time = root.TryGetProperty(TimeProperty, out var timeElement) &&
				   DateTimeOffset.TryParse(timeElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp)
				? timestamp
				: DateTimeOffset.UtcNow,
		};

		if (root.TryGetProperty(SubjectProperty, out var subjectElement))
		{
			cloudEvent.Subject = subjectElement.GetString();
		}

		if (root.TryGetProperty(DataContentTypeProperty, out var contentTypeElement))
		{
			cloudEvent.DataContentType = contentTypeElement.GetString();
		}

		if (root.TryGetProperty(DataSchemaProperty, out var schemaElement) &&
			Uri.TryCreate(schemaElement.GetString(), UriKind.RelativeOrAbsolute, out var schemaUri))
		{
			cloudEvent.DataSchema = schemaUri;
		}

		if (root.TryGetProperty(DataProperty, out var dataElement))
		{
			cloudEvent.Data = dataElement.ValueKind switch
			{
				JsonValueKind.String => dataElement.GetString(),
				_ => dataElement.GetRawText(),
			};
		}

		if (root.TryGetProperty(TimeoutProperty, out var timeoutElement))
		{
			cloudEvent[TimeoutProperty] = timeoutElement.GetString();
		}

		if (root.TryGetProperty(TraceParentProperty, out var traceParentElement))
		{
			cloudEvent[TraceParentProperty] = traceParentElement.GetString();
		}

		return cloudEvent;
	}

	private CloudEvent ParseBinaryDetail(PutEventsRequestEntry entry)
	{
		var cloudEvent = CreateDefaultCloudEvent(entry);
		if (!string.IsNullOrWhiteSpace(entry.Detail))
		{
			cloudEvent.Data = entry.Detail;
		}

		return cloudEvent;
	}

	private CloudEvent CreateDefaultCloudEvent(PutEventsRequestEntry entry) => new()
	{
		Source = TryParseUri(entry.Source, Options.DefaultSource) ?? Options.DefaultSource,
		Type = entry.DetailType ?? "aws.eventbridge.event",
		Id = Guid.NewGuid().ToString(),
		Time = DateTimeOffset.UtcNow,
	};

	private static class JsonSerializerOptionsProvider
	{
		internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		};
	}
}
