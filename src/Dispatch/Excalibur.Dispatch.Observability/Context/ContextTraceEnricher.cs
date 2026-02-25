// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// OpenTelemetry integration component that adds context fields as span attributes, creates spans for context operations, links related
/// traces via correlation IDs, and propagates baggage items across service boundaries.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ContextTraceEnricher" /> class. </remarks>
/// <param name="logger"> Logger for diagnostic output. </param>
/// <param name="options"> Configuration options. </param>
/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
public sealed partial class ContextTraceEnricher(
	ILogger<ContextTraceEnricher> logger,
	IOptions<ContextObservabilityOptions> options,
	ITelemetrySanitizer sanitizer) : IContextTraceEnricher, IDisposable
{
	private readonly ILogger<ContextTraceEnricher> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ContextObservabilityOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ITelemetrySanitizer _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Observability.Context.Enricher", "1.0.0");
	private readonly TextMapPropagator _propagator = Propagators.DefaultTextMapPropagator;

	/// <summary>
	/// Enriches an activity with context information.
	/// </summary>
	/// <param name="activity"> The activity to enrich. </param>
	/// <param name="context"> The message context containing data to add. </param>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	public void EnrichActivity(Activity? activity, IMessageContext context)
	{
		if (activity == null || context == null)
		{
			return;
		}

		try
		{
			// Add standard context attributes
			AddStandardAttributes(activity, context);

			// Add custom attributes if enabled
			if (_options.Tracing.IncludeCustomItemsInTraces)
			{
				AddCustomAttributes(activity, context);
			}

			// Add baggage items for cross-service propagation
			AddBaggageItems(activity, context);

			// Link to parent trace if available
			LinkToParentTrace(activity, context);

			// Add events for significant context states
			AddContextEvents(activity, context);

			LogActivityEnriched(activity.DisplayName, context.MessageId ?? "unknown");
		}
		catch (InvalidOperationException ex)
		{
			LogFailedEnrichOperationInvalid(ex);
		}
		catch (ArgumentException ex)
		{
			LogFailedEnrichArgument(ex);
		}
	}

	/// <summary>
	/// Creates a span for a context operation.
	/// </summary>
	/// <param name="operationName"> Name of the operation. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="kind"> The activity kind. </param>
	/// <returns> The created activity/span. </returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	public Activity? CreateContextOperationSpan(
		string operationName,
		IMessageContext context,
		ActivityKind kind = ActivityKind.Internal)
	{
		var activity = _activitySource.StartActivity(
			$"Context.{operationName}",
			kind);

		if (activity != null)
		{
			EnrichActivity(activity, context);

			// Add operation-specific attributes
			_ = activity.SetTag("context.operation", operationName);
			_ = activity.SetTag("context.operation.timestamp", DateTimeOffset.UtcNow.ToString("O"));
		}

		return activity;
	}

	/// <summary>
	/// Links related traces using correlation IDs.
	/// </summary>
	/// <param name="activity"> The current activity. </param>
	/// <param name="correlationId"> The correlation ID to link. </param>
	/// <param name="linkType"> Type of link relationship. </param>
	public void LinkRelatedTrace(Activity? activity, string correlationId, string linkType = "correlation")
	{
		if (activity == null || string.IsNullOrWhiteSpace(correlationId))
		{
			return;
		}

		try
		{
			// Create a trace context from the correlation ID In a real implementation, you might store and retrieve actual trace contexts
			var traceId = GenerateTraceIdFromCorrelation(correlationId);
			var spanId = ActivitySpanId.CreateRandom();

			var linkedContext = new ActivityContext(
				traceId,
				spanId,
				ActivityTraceFlags.Recorded,
				traceState: null);

			var link = new ActivityLink(
				linkedContext,
				new ActivityTagsCollection { { "link.type", linkType }, { "correlation.id", correlationId } });

			// Note: Links must be added during activity creation in OpenTelemetry This is a limitation we document for users
			LogTraceLinkCreated(correlationId, linkType);
		}
		catch (InvalidOperationException ex)
		{
			LogFailedLinkRelatedTrace(correlationId, ex);
		}
		catch (ArgumentException ex)
		{
			LogFailedLinkRelatedTraceArgument(correlationId, ex);
		}
	}

	/// <summary>
	/// Propagates context as baggage for cross-service communication.
	/// </summary>
	/// <param name="context"> The message context to propagate. </param>
	/// <param name="carrier"> The carrier dictionary for propagation. </param>
	public void PropagateContextAsBaggage(IMessageContext context, IDictionary<string, string> carrier)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(carrier);

		foreach (var item in CollectBaggageItems(context))
		{
			carrier[item.Key] = item.Value;
		}
	}

	/// <summary>
	/// Extracts context from baggage in incoming requests.
	/// </summary>
	/// <param name="carrier"> The carrier dictionary containing baggage. </param>
	/// <param name="context"> The message context to populate. </param>
	public void ExtractContextFromBaggage(IDictionary<string, string> carrier, IMessageContext context)
	{
		if (carrier == null || context == null)
		{
			return;
		}

		try
		{
			// Extract propagation context and get baggage Process each baggage item
			foreach (var item in ExtractBaggageFromCarrier(carrier))
			{
				ProcessBaggageItem(item, context);
			}

			LogContextExtracted(context.MessageId ?? "unknown");
		}
		catch (InvalidOperationException ex)
		{
			LogFailedExtractBaggageInvalid(ex);
		}
		catch (ArgumentException ex)
		{
			LogFailedExtractBaggageArgument(ex);
		}
	}

	/// <summary>
	/// Adds a context event to the current span.
	/// </summary>
	/// <param name="eventName"> Name of the event. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="attributes"> Additional attributes for the event. </param>
	public void AddContextEvent(
		string eventName,
		IMessageContext context,
		IReadOnlyDictionary<string, object>? attributes = null)
	{
		ArgumentNullException.ThrowIfNull(eventName);
		ArgumentNullException.ThrowIfNull(context);

		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		var eventAttributes = new ActivityTagsCollection
		{
			{ "message.id", context.MessageId }, { "correlation.id", context.CorrelationId },
		};

		if (attributes != null)
		{
			foreach (var attr in attributes)
			{
				eventAttributes[attr.Key] = attr.Value;
			}
		}

		_ = activity.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow, eventAttributes));

		LogContextEventAdded(eventName, context.MessageId ?? "unknown");
	}

	/// <inheritdoc />
	public void Dispose() => _activitySource?.Dispose();

	private void AddStandardAttributes(Activity activity, IMessageContext context)
	{
		// Message identification
		_ = activity.SetTag("message.id", context.MessageId);
		_ = activity.SetTag("message.external_id", context.ExternalId);
		_ = activity.SetTag("message.type", context.MessageType);
		_ = activity.SetTag("message.version", context.MessageVersion());
		_ = activity.SetTag("message.contract_version", context.ContractVersion());

		// Correlation and causation
		_ = activity.SetTag("correlation.id", context.CorrelationId);
		_ = activity.SetTag("causation.id", context.CausationId);

		// User and tenant context — sanitize PII
		SetSanitizedTag(activity, "user.id", context.UserId);
		SetSanitizedTag(activity, "tenant.id", context.TenantId);

		// Routing and delivery
		_ = activity.SetTag("message.source", context.Source);
		_ = activity.SetTag("message.partition_key", context.PartitionKey());
		_ = activity.SetTag("message.reply_to", context.ReplyTo());
		_ = activity.SetTag("message.delivery_count", context.DeliveryCount);

		// Timestamps
		_ = activity.SetTag("message.sent_timestamp", context.SentTimestampUtc?.ToString("O"));
		_ = activity.SetTag("message.received_timestamp", context.ReceivedTimestampUtc.ToString("O"));

		// Processing state
		_ = activity.SetTag("context.validation.valid", (context.ValidationResult() as IValidationResult)?.IsValid ?? false);
		_ = activity.SetTag(
			"context.authorization.authorized",
			(context.AuthorizationResult() as IAuthorizationResult)?.IsAuthorized ?? false);
		_ = activity.SetTag("context.routing.success", context.RoutingDecision?.IsSuccess ?? true);
	}

	private void SetSanitizedTag(Activity activity, string tagName, string? rawValue)
	{
		var sanitized = _sanitizer.SanitizeTag(tagName, rawValue);
		if (sanitized is not null)
		{
			_ = activity.SetTag(tagName, sanitized);
		}
	}

	// R0.8: Unused parameter - required by signature pattern
#pragma warning disable RCS1163, IDE0060

	private void AddBaggageItems(Activity activity, IMessageContext context)
	{
		// Add standard baggage items
		if (!string.IsNullOrWhiteSpace(context.CorrelationId))
		{
			_ = Baggage.SetBaggage("correlation.id", context.CorrelationId);
		}

		if (!string.IsNullOrWhiteSpace(context.TenantId))
		{
			SetSanitizedBaggage("tenant.id", context.TenantId);
		}

		if (!string.IsNullOrWhiteSpace(context.UserId))
		{
			SetSanitizedBaggage("user.id", context.UserId);
		}

		if (!string.IsNullOrWhiteSpace(context.MessageType))
		{
			_ = Baggage.SetBaggage("message.type", context.MessageType);
		}
	}

	private void SetSanitizedBaggage(string key, string? rawValue)
	{
		var sanitized = _sanitizer.SanitizeTag(key, rawValue);
		if (sanitized is not null)
		{
			_ = Baggage.SetBaggage(key, sanitized);
		}
	}

#pragma warning restore RCS1163, IDE0060

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static void AddContextEvents(Activity activity, IMessageContext context)
	{
		// Add event for context creation if this is a new context
		if (context.GetItem<bool>("IsNewContext"))
		{
			_ = activity.AddEvent(new ActivityEvent(
				"context.created",
				DateTimeOffset.UtcNow,
				new ActivityTagsCollection { { "message.id", context.MessageId }, { "correlation.id", context.CorrelationId }, }));
		}

		// Add event for validation results
		if (context.ValidationResult() is IValidationResult { IsValid: false } validationResult)
		{
			_ = activity.AddEvent(new ActivityEvent(
				"context.validation.failed",
				DateTimeOffset.UtcNow,
				new ActivityTagsCollection { { "errors", JsonSerializer.Serialize(validationResult.Errors) } }));
		}

		// Add event for authorization results
		if (context.AuthorizationResult() is IAuthorizationResult { IsAuthorized: false } authResult)
		{
			_ = activity.AddEvent(new ActivityEvent(
				"context.authorization.failed",
				DateTimeOffset.UtcNow,
				new ActivityTagsCollection { { "reason", authResult.FailureMessage } }));
		}
	}

	private static ActivityTraceId GenerateTraceIdFromCorrelation(string correlationId)
	{
		// Generate a deterministic trace ID from correlation ID This allows linking related traces even without stored context
		var bytes = Encoding.UTF8.GetBytes(correlationId);
		var hash = SHA256.HashData(bytes);

		// Take first 16 bytes for trace ID - use stackalloc to avoid heap allocation
		Span<byte> traceIdBytes = stackalloc byte[16];
		hash.AsSpan(0, 16).CopyTo(traceIdBytes);

		return ActivityTraceId.CreateFromBytes(traceIdBytes);
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static string SerializeAttributeValue(object? value)
	{
		if (value == null)
		{
			return "null";
		}

		if (value is string str)
		{
			return str;
		}

		if (value.GetType().IsPrimitive)
		{
			return value.ToString() ?? string.Empty;
		}

		try
		{
			// Limit serialized value size
			var json = JsonSerializer.Serialize(value);
			const int maxLength = 1000;
			return json.Length > maxLength ? $"{json.AsSpan(0, maxLength)}..." : json;
		}
		catch (NotSupportedException)
		{
			return value.ToString() ?? "[serialization error]";
		}
		catch (ArgumentException)
		{
			return value.ToString() ?? "[serialization error]";
		}
	}

	private Dictionary<string, string> CollectBaggageItems(IMessageContext context)
	{
		var baggageItems = new Dictionary<string, string>(StringComparer.Ordinal);

		if (!string.IsNullOrWhiteSpace(context.CorrelationId))
		{
			baggageItems["correlation.id"] = context.CorrelationId;
		}

		if (!string.IsNullOrWhiteSpace(context.CausationId))
		{
			baggageItems["causation.id"] = context.CausationId;
		}

		AddSanitizedBaggageItem(baggageItems, "tenant.id", context.TenantId);
		AddSanitizedBaggageItem(baggageItems, "user.id", context.UserId);

		if (!string.IsNullOrWhiteSpace(context.MessageType))
		{
			baggageItems["message.type"] = context.MessageType;
		}

		var messageVersion = context.MessageVersion();
		if (!string.IsNullOrWhiteSpace(messageVersion))
		{
			baggageItems["message.version"] = messageVersion;
		}

		return baggageItems;
	}

	private void AddSanitizedBaggageItem(Dictionary<string, string> items, string key, string? rawValue)
	{
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			return;
		}

		var sanitized = _sanitizer.SanitizeTag(key, rawValue);
		if (sanitized is not null)
		{
			items[key] = sanitized;
		}
	}

	private static void ProcessStringProperty(string value, string? currentValue, Action<string> setter)
	{
		if (string.IsNullOrWhiteSpace(currentValue))
		{
			setter(value);
		}
	}

	private static void ProcessVersionProperty(string value, IMessageContext context)
	{
		if (string.IsNullOrWhiteSpace(context.MessageVersion()))
		{
			context.MessageVersion(value);
		}
	}

	private Baggage ExtractBaggageFromCarrier(IDictionary<string, string> carrier)
	{
		var propagationContext = _propagator.Extract(
			default,
			carrier,
			static (c, k) => c.TryGetValue(k, out var v) ? new[] { v } : []);

		return propagationContext.Baggage;
	}

	private void ProcessBaggageItem(KeyValuePair<string, string> item, IMessageContext context)
	{
		switch (item.Key)
		{
			case "correlation.id":
				ProcessCorrelationId(item.Value, context);
				break;

			case "causation.id":
				ProcessCausationId(item.Value, context);
				break;

			case "tenant.id":
				ProcessTenantId(item.Value, context);
				break;

			case "user.id":
				ProcessStringProperty(item.Value, context.UserId, v => context.UserId = v);
				break;

			case "message.type":
				ProcessStringProperty(item.Value, context.MessageType, v => context.MessageType = v);
				break;

			case "message.version":
				ProcessVersionProperty(item.Value, context);
				break;

			default:
				ProcessUnknownBaggageItem(item, context);
				break;
		}
	}

	private void ProcessCorrelationId(string value, IMessageContext context)
	{
		if (context.CorrelationId == null && !string.IsNullOrWhiteSpace(value))
		{
			// Would need a factory method to create ICorrelationId from string
			LogCorrelationIdExtracted(value);
		}
	}

	private void ProcessCausationId(string value, IMessageContext context)
	{
		if (context.CausationId == null && !string.IsNullOrWhiteSpace(value))
		{
			// Would need a factory method to create ICausationId from string
			LogCausationIdExtracted(value);
		}
	}

	private void ProcessTenantId(string value, IMessageContext context)
	{
		if (context.TenantId == null && !string.IsNullOrWhiteSpace(value))
		{
			// Would need a factory method to create ITenantId from string
			LogTenantIdExtracted(value);
		}
	}

	private void ProcessUnknownBaggageItem(KeyValuePair<string, string> item, IMessageContext context)
	{
		if (_options.Tracing.PreserveUnknownBaggageItems)
		{
			context.SetItem($"Baggage_{item.Key}", item.Value);
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void AddCustomAttributes(Activity activity, IMessageContext context)
	{
		var maxItems = _options.Tracing.MaxCustomItemsInTraces;
		var itemCount = 0;

		foreach (var item in context.Items)
		{
			if (itemCount >= maxItems)
			{
				break;
			}

			// Skip items that might be sensitive
			if (_options.Tracing.SensitiveFieldPatterns?.Any(pattern =>
					Regex.IsMatch(item.Key, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100))) == true)
			{
				continue;
			}

			var tagName = $"context.item.{item.Key}";
			var value = SerializeAttributeValue(item.Value);
			var sanitized = _sanitizer.SanitizeTag(tagName, value);
			if (sanitized is not null)
			{
				_ = activity.SetTag(tagName, sanitized);
			}
			itemCount++;
		}
	}

	private void LinkToParentTrace(Activity activity, IMessageContext context)
	{
		if (string.IsNullOrWhiteSpace(context.TraceParent))
		{
			return;
		}

		try
		{
			// Parse W3C trace parent
			if (ActivityContext.TryParse(context.TraceParent, traceState: null, out var parentContext))
			{
				// Note: In OpenTelemetry, parent must be set during activity creation This is documented as a limitation
				_ = activity.SetTag("parent.trace_id", parentContext.TraceId.ToString());
				_ = activity.SetTag("parent.span_id", parentContext.SpanId.ToString());
			}
		}
		catch (FormatException ex)
		{
			LogTraceParentParseFormatError(ex, context.TraceParent);
		}
		catch (ArgumentException ex)
		{
			LogTraceParentParseArgumentError(ex, context.TraceParent);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(ObservabilityEventId.ActivityEnriched, LogLevel.Debug,
		"Enriched activity {ActivityName} with context from message {MessageId}")]
	private partial void LogActivityEnriched(string activityName, string messageId);

	[LoggerMessage(ObservabilityEventId.TraceLinkCreated, LogLevel.Debug,
		"Created trace link for correlation {CorrelationId} with type {LinkType}")]
	private partial void LogTraceLinkCreated(string correlationId, string linkType);

	[LoggerMessage(ObservabilityEventId.BaggagePropagated, LogLevel.Debug,
		"Propagated {ItemCount} context items as baggage for message {MessageId}")]
	private partial void LogBaggagePropagated(int itemCount, string messageId);

	[LoggerMessage(ObservabilityEventId.CorrelationIdExtracted, LogLevel.Debug,
		"Extracted correlation.id from baggage: {Value}")]
	private partial void LogCorrelationIdExtracted(string value);

	[LoggerMessage(ObservabilityEventId.CausationIdExtracted, LogLevel.Debug,
		"Extracted causation.id from baggage: {Value}")]
	private partial void LogCausationIdExtracted(string value);

	[LoggerMessage(ObservabilityEventId.TenantIdExtracted, LogLevel.Debug,
		"Extracted tenant.id from baggage: {Value}")]
	private partial void LogTenantIdExtracted(string value);

	[LoggerMessage(ObservabilityEventId.ContextExtracted, LogLevel.Debug,
		"Extracted context from baggage for message {MessageId}")]
	private partial void LogContextExtracted(string messageId);

	[LoggerMessage(ObservabilityEventId.ContextEventAdded, LogLevel.Debug,
		"Added context event {EventName} to span for message {MessageId}")]
	private partial void LogContextEventAdded(string eventName, string messageId);

	[LoggerMessage(ObservabilityEventId.TraceParentParseFormatError, LogLevel.Debug,
		"Failed to parse trace parent: {TraceParent} - format error")]
	private partial void LogTraceParentParseFormatError(Exception ex, string traceParent);

	[LoggerMessage(ObservabilityEventId.TraceParentParseArgumentError, LogLevel.Debug,
		"Failed to parse trace parent: {TraceParent} - invalid argument")]
	private partial void LogTraceParentParseArgumentError(Exception ex, string traceParent);

	[LoggerMessage(ObservabilityEventId.FailedEnrichOperationInvalid, LogLevel.Error,
		"Failed to enrich activity with context - operation is not valid")]
	private partial void LogFailedEnrichOperationInvalid(Exception ex);

	[LoggerMessage(ObservabilityEventId.FailedEnrichArgument, LogLevel.Error,
		"Failed to enrich activity with context - invalid argument")]
	private partial void LogFailedEnrichArgument(Exception ex);

	[LoggerMessage(ObservabilityEventId.FailedLinkRelatedTrace, LogLevel.Error,
		"Failed to link related trace for correlation {CorrelationId}")]
	private partial void LogFailedLinkRelatedTrace(string correlationId, Exception ex);

	[LoggerMessage(ObservabilityEventId.FailedLinkRelatedTraceArgument, LogLevel.Error,
		"Failed to link related trace for correlation {CorrelationId} - invalid argument")]
	private partial void LogFailedLinkRelatedTraceArgument(string correlationId, Exception ex);

	[LoggerMessage(ObservabilityEventId.FailedExtractBaggageInvalid, LogLevel.Error,
		"Failed to extract context from baggage - invalid operation")]
	private partial void LogFailedExtractBaggageInvalid(Exception ex);

	[LoggerMessage(ObservabilityEventId.FailedExtractBaggageArgument, LogLevel.Error,
		"Failed to extract context from baggage - invalid argument")]
	private partial void LogFailedExtractBaggageArgument(Exception ex);
}
