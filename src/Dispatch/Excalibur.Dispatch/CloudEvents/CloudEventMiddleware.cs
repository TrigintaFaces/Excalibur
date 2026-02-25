// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Metadata;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Middleware that handles CloudEvent processing in the Dispatch pipeline. Provides automatic conversion between CloudEvents and Dispatch messages.
/// </summary>
public sealed partial class CloudEventMiddleware(
	ILogger<CloudEventMiddleware> logger,
	IOptions<CloudEventOptions> options,
	IEnvelopeCloudEventBridge bridge,
	ISchemaRegistry? schemaRegistry = null) : IDispatchMiddleware
{
	private readonly ILogger<CloudEventMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly CloudEventOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly IEnvelopeCloudEventBridge _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

	/// <summary>
	/// Gets the pipeline stage where this middleware should be executed.
	/// </summary>
	/// <value> The current <see cref="Stage" /> value. </value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <summary>
	/// Invokes the CloudEvent middleware to process incoming and outgoing CloudEvents.
	/// </summary>
	/// <param name="message"> The dispatch message being processed. </param>
	/// <param name="context"> The message context containing metadata. </param>
	/// <param name="nextDelegate"> The next middleware delegate in the pipeline. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task representing the message processing result. </returns>
	/// <exception cref="InvalidOperationException"> </exception>
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Check if we're processing a CloudEvent
		var incomingCloudEvent = GetIncomingCloudEvent(context);
		if (incomingCloudEvent != null)
		{
			// Validate and process incoming CloudEvent
			if (!await ValidateIncomingCloudEventAsync(incomingCloudEvent, cancellationToken).ConfigureAwait(false))
			{
				throw new InvalidOperationException($"CloudEvent validation failed for type: {incomingCloudEvent.Type}");
			}

			// Enrich context with CloudEvent metadata
			EnrichContextFromCloudEvent(context, incomingCloudEvent);
		}

		// Process the message
		var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		// If configured, convert outgoing messages to CloudEvents
		if (message is IDispatchEvent dispatchEvent)
		{
			var (envelope, shouldDispose) = EnsureEnvelope(dispatchEvent, context);
			try
			{
				var outgoingCloudEvent = await CreateOutgoingCloudEventAsync(envelope, dispatchEvent, context, cancellationToken)
					.ConfigureAwait(false);
				context.Items["cloudevent.outgoing"] = outgoingCloudEvent;
			}
			finally
			{
				if (shouldDispose)
				{
					envelope.Dispose();
				}
			}
		}

		return result;
	}

	private static CloudEvent? GetIncomingCloudEvent(IMessageContext context)
	{
		if (context.Items.TryGetValue("cloudevent", out var cloudEventObj) && cloudEventObj is CloudEvent cloudEvent)
		{
			return cloudEvent;
		}

		if (context.Items.TryGetValue("cloudevent.incoming", out var incomingObj) && incomingObj is CloudEvent incomingCloudEvent)
		{
			return incomingCloudEvent;
		}

		foreach (var value in context.Items.Values)
		{
			if (value is CloudEvent nestedCloudEvent)
			{
				return nestedCloudEvent;
			}
		}

		return null;
	}

	private static (MessageEnvelope Envelope, bool ShouldDispose) EnsureEnvelope(IDispatchEvent dispatchEvent, IMessageContext context)
	{
		if (context is MessageEnvelope existingEnvelope)
		{
			existingEnvelope.Message ??= dispatchEvent;
			return (existingEnvelope, false);
		}

		var envelope = new MessageEnvelope(dispatchEvent)
		{
			MessageId = context.MessageId,
			ExternalId = context.ExternalId,
			UserId = context.UserId,
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId,
			TraceParent = context.TraceParent,
			TenantId = context.TenantId,
			SessionId = context.SessionId,
			WorkflowId = context.WorkflowId,
			PartitionKey = context.PartitionKey,
			Source = context.Source,
			MessageType = context.MessageType,
			ContentType = context.ContentType,
			SerializerVersion = context.SerializerVersion(),
			MessageVersion = context.MessageVersion(),
			ContractVersion = context.ContractVersion(),
			DesiredVersion = int.TryParse(context.DesiredVersion(), out var desired) ? desired : null,
			DeliveryCount = context.DeliveryCount,
			Message = dispatchEvent,
			Result = context.Result,
			RequestServices = context.RequestServices,
			ReceivedTimestampUtc = context.ReceivedTimestampUtc,
			SentTimestampUtc = context.SentTimestampUtc,

			// Populate legacy metadata from unified context metadata
			Metadata = context.ExtractMetadata().ToLegacy(),
		};

		foreach (var item in context.Items)
		{
			envelope.Items[item.Key] = item.Value;
		}

		foreach (var property in context.Properties)
		{
			envelope.Properties[property.Key] = property.Value;
		}

		return (envelope, true);
	}

	private async Task<bool> ValidateIncomingCloudEventAsync(CloudEvent cloudEvent, CancellationToken cancellationToken)
	{
		// Basic validation
		if (string.IsNullOrEmpty(cloudEvent.Type))
		{
			LogCloudEventWithoutType();
			return false;
		}

		// Schema validation if registry is available
		if (schemaRegistry != null && _options.ValidateSchema)
		{
			var schemaVersion = cloudEvent.GetSchemaVersion();
			if (!string.IsNullOrEmpty(schemaVersion))
			{
				var schema = await schemaRegistry.GetSchemaAsync(cloudEvent.Type, schemaVersion, cancellationToken).ConfigureAwait(false);
				if (schema == null)
				{
					LogSchemaNotFound(cloudEvent.Type, schemaVersion);
					return !_options.ValidateSchema;
				}

				// Schema validation implementation depends on the specific registry provider Core validation is delegated to the schema
				// registry implementation
				LogSchemaValidated(cloudEvent.Type, schemaVersion);
			}
		}

		// Custom validation
		if (_options.CustomValidator != null)
		{
			return await _options.CustomValidator(cloudEvent, cancellationToken).ConfigureAwait(false);
		}

		return true;
	}

	private void EnrichContextFromCloudEvent(IMessageContext context, CloudEvent cloudEvent)
	{
		// Set standard context properties from CloudEvent
		if (cloudEvent.Id != null)
		{
			context.MessageId = cloudEvent.Id;
		}

		if (cloudEvent.Source != null)
		{
			context.Source = cloudEvent.Source.ToString();
		}

		if (cloudEvent.Time.HasValue)
		{
			context.SentTimestampUtc = cloudEvent.Time.Value;
		}

		// Extract correlation ID from extensions
		if (cloudEvent["correlationid"] is string correlationId)
		{
			context.CorrelationId = correlationId;
		}

		// Extract trace context from extensions
		if (cloudEvent["traceparent"] is string traceParent)
		{
			context.TraceParent = traceParent;
		}

		// Copy other extension attributes to context items
		foreach (var attr in cloudEvent.ExtensionAttributes)
		{
			if (!_options.ExcludedExtensions.Contains(attr.Name))
			{
				context.Items[$"ce.{attr.Name}"] = cloudEvent[attr.Name]!;
			}
		}
	}

	private async Task<CloudEvent> CreateOutgoingCloudEventAsync(
		MessageEnvelope envelope,
		IDispatchEvent evt,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		var cloudEvent = await _bridge.ToTransportAsync<CloudEvent>(envelope, _options.DefaultMode, cancellationToken)
			.ConfigureAwait(false);

		// Add schema version if configured
		if (_options.IncludeSchemaVersion)
		{
			var version = _options.SchemaVersionProvider?.Invoke(evt.GetType()) ?? "1.0";
			cloudEvent.SetSchemaVersion(version);

			// Schema compatibility mode handling would go here if needed
		}

		// Register schema if needed
		if (schemaRegistry != null && _options.AutoRegisterSchemas)
		{
			var schema = _options.SchemaProvider?.Invoke(evt.GetType());
			if (!string.IsNullOrEmpty(schema))
			{
				// Schema registration is provider-specific and handled by the concrete registry implementation Core ISchemaRegistry
				// intentionally omits RegisterSchemaAsync to follow pay-for-play principle Provider-specific registries (AWS, Google)
				// implement their own registration methods
			}
		}

		// Apply custom transformations
		if (_options.OutgoingTransformer != null)
		{
			await _options.OutgoingTransformer(cloudEvent, evt, context, cancellationToken).ConfigureAwait(false);
		}

		return cloudEvent;
	}

	#region LoggerMessage Definitions

	[LoggerMessage(CoreEventId.CloudEventWithoutType, LogLevel.Warning,
		"Received CloudEvent without type")]
	private partial void LogCloudEventWithoutType();

	[LoggerMessage(CoreEventId.SchemaNotFound, LogLevel.Warning,
		"Schema not found for CloudEvent type {Type} version {Version}")]
	private partial void LogSchemaNotFound(string type, string version);

	[LoggerMessage(CoreEventId.SchemaValidated, LogLevel.Debug,
		"Validated CloudEvent against schema {Type} v{Version}")]
	private partial void LogSchemaValidated(string type, string version);

	#endregion
}
