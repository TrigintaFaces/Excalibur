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
/// AWS EventBridge implementation of <see cref="ICloudEventEncoder{TOutbound}" />.
/// </summary>
internal sealed class AwsEventBridgeCloudEventAdapter : ICloudEventEncoder<PutEventsRequestEntry>
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

	private readonly AwsEventBridgeCloudEventOptions _eventBridgeOptions;
	private readonly ILogger<AwsEventBridgeCloudEventAdapter> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsEventBridgeCloudEventAdapter" /> class.
	/// </summary>
	/// <param name="options"> The general CloudEvent options (default source/mode, excluded extensions). </param>
	/// <param name="eventBridgeOptions"> The EventBridge-specific options (event bus, detail-type strategy, extension inclusion). </param>
	/// <param name="logger"> The logger instance. </param>
	public AwsEventBridgeCloudEventAdapter(
		IOptions<CloudEventOptions> options,
		IOptions<AwsEventBridgeCloudEventOptions> eventBridgeOptions,
		ILogger<AwsEventBridgeCloudEventAdapter> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(eventBridgeOptions);
		ArgumentNullException.ThrowIfNull(logger);

		Options = options.Value ?? throw new ArgumentNullException(nameof(options));
		_eventBridgeOptions = eventBridgeOptions.Value ?? throw new ArgumentNullException(nameof(eventBridgeOptions));
		_logger = logger;
	}

	/// <inheritdoc />
	public CloudEventOptions Options { get; }

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
			// Honor the configured target event bus (previously hardcoded empty, so every send defaulted
			// to the account's "default" bus regardless of configuration).
			EventBusName = _eventBridgeOptions.EventBusName,
			DetailType = _eventBridgeOptions.UseCloudEventTypeAsDetailType
				? cloudEvent.Type ?? "CloudEvent"
				: "CloudEvent",
			// EventBridge requires a source identifier with a stricter format than a CloudEvent source URI;
			// use the configured prefix as the envelope source while the full CloudEvent source round-trips
			// in the detail payload below.
			Source = string.IsNullOrWhiteSpace(_eventBridgeOptions.SourcePrefix)
				? cloudEvent.Source?.ToString() ?? Options.DefaultSource.ToString()
				: _eventBridgeOptions.SourcePrefix,
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

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private Dictionary<string, object?> BuildDetailPayload(CloudEvent cloudEvent, CloudEventMode mode)
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

		// Preserve custom CloudEvent extension attributes (the dispatch envelope's context) instead of
		// dropping everything outside the fixed attribute set. Excluded extensions and attributes already
		// mapped above are skipped so the detail stays canonical.
		if (_eventBridgeOptions.IncludeExtensionsInDetail)
		{
			foreach (var attribute in cloudEvent.ExtensionAttributes)
			{
				if (payload.ContainsKey(attribute.Name) || Options.ExcludedExtensions.Contains(attribute.Name))
				{
					continue;
				}

				payload[attribute.Name] = cloudEvent[attribute.Name];
			}
		}

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

	private static class JsonSerializerOptionsProvider
	{
		internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		};
	}
}
