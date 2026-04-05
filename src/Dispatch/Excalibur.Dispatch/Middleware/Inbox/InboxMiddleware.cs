// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Common namespace is deprecated - using Messaging.Abstractions instead
using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;
using InboxOptions = Excalibur.Dispatch.Options.Configuration.InboxConfigurationOptions;
using MessageKinds = Excalibur.Dispatch.Abstractions.MessageKinds;
using MR = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Middleware.Inbox;

/// <summary>
/// Middleware responsible for implementing inbox semantics to ensure at-most-once message processing.
/// </summary>
/// <remarks>
/// This middleware provides two modes of operation:
/// <list type="bullet">
/// <item> <strong> Full Inbox Mode </strong>: Uses persistent storage via IInboxStore for durability across restarts </item>
/// <item> <strong> Light Mode </strong>: Uses in-memory deduplication via IInMemoryDeduplicator for performance </item>
/// </list>
/// The inbox middleware implements the Idempotent Consumer pattern by:
/// <list type="number">
/// <item> Checking if message has already been processed (duplicate detection) </item>
/// <item> Persisting message receipt before handler execution (inbox persistence) </item>
/// <item> Marking message as processed after successful handler completion </item>
/// <item> Handling failures with appropriate retry mechanisms </item>
/// </list>
/// This ensures messages are processed at most once, even in the presence of failures and redeliveries.
/// </remarks>
[AppliesTo(MessageKinds.All)]
[RequiresFeatures(DispatchFeatures.Inbox)]
public sealed partial class InboxMiddleware : IDispatchMiddleware
{
	private const int MaxCacheEntries = 1024;
	private static readonly ActivitySource InboxActivitySource = new(DispatchTelemetryConstants.ActivitySources.Inbox, "1.0.0");
	private static readonly ConcurrentDictionary<Type, PropertyInfo?> MessageIdPropertyCache = new();
	private static readonly Func<ILogger, string, string, bool, string, IDisposable?> InboxLogScope =
		LoggerMessage.DefineScope<string, string, bool, string>(
			"MessageId:{MessageId} MessageType:{MessageType} InboxEnabled:{InboxEnabled} InboxMode:{InboxMode}");

	private readonly InboxOptions _options;
	private readonly IInboxStore? _inboxStore;
	private readonly IInMemoryDeduplicator? _deduplicator;
	private readonly ILogger<InboxMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InboxMiddleware"/> class.
	/// Creates a new inbox middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for inbox behavior. </param>
	/// <param name="inboxStore"> Optional persistent inbox store for full inbox mode. </param>
	/// <param name="deduplicator"> Optional in-memory deduplicator for light mode. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public InboxMiddleware(
		IOptions<InboxOptions> options,
		IInboxStore? inboxStore,
		IInMemoryDeduplicator? deduplicator,
		ILogger<InboxMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_inboxStore = inboxStore;
		_deduplicator = deduplicator;
		_logger = logger;

		// Validate configuration
		if (_options.Enabled && _inboxStore == null && _deduplicator == null)
		{
			throw new InvalidOperationException(
				ErrorMessages.InboxMiddlewareEnabledButNeitherStoreNorDeduplicatorRegistered);
		}
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Inbox payload serialization is only used for known message types registered at startup.")]
	[UnconditionalSuppressMessage(
		"AotAnalysis",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Inbox payload serialization relies on JSON serialization and is not supported in AOT scenarios.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Skip inbox processing if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Extract message identifier
		var messageId = GetMessageId(message, context);
		if (string.IsNullOrEmpty(messageId))
		{
			LogCannotProcessMessageWithoutIdentifier();
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Set up logging scope and activity tags
		using var logScope = CreateInboxLoggingScope(messageId, message);
		SetInboxActivityTags(messageId, message);

		LogProcessingMessage(messageId);

		// Create a tracing activity for inbox processing to enable distributed trace correlation
		using var inboxActivity = InboxActivitySource.StartActivity("inbox.process", ActivityKind.Consumer);
		_ = (inboxActivity?.SetTag("messaging.message_id", messageId));
		_ = (inboxActivity?.SetTag("messaging.handler_type", message.GetType().Name));
		_ = (inboxActivity?.SetTag("inbox.mode", _inboxStore != null ? "full" : "light"));

		// Propagate W3C trace context from incoming message headers if available
		if (context.CorrelationId is not null)
		{
			_ = (inboxActivity?.SetTag("messaging.correlation_id", context.CorrelationId));
		}

		try
		{
			// Use full inbox mode if available, otherwise fall back to light mode
			IMessageResult result;
			if (_inboxStore != null)
			{
				result = await ProcessWithFullInboxAsync(messageId, message, context, nextDelegate, cancellationToken)
					.ConfigureAwait(false);
			}
			else if (_deduplicator != null)
			{
				result = await ProcessWithLightModeAsync(messageId, message, context, nextDelegate, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				// This should not happen due to constructor validation, but handle gracefully
				LogNoInboxStoreOrDeduplicator();
				result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			}

			_ = (inboxActivity?.SetTag("inbox.result", result.Succeeded ? "success" : "failure"));
			return result;
		}
		catch (Exception ex)
		{
			_ = (inboxActivity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			LogExceptionDuringInboxProcessing(messageId, ex);
			throw;
		}
	}

	/// <summary>
	/// Extracts the message identifier from the message and context.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to target method",
		Justification = "Message types are preserved through source generation and DI registration")]
	private static string? GetMessageId(IDispatchMessage message, IMessageContext context)
	{
		// Prefer direct context property access (hot path) before dictionary lookups.
		if (!string.IsNullOrEmpty(context.MessageId))
		{
			return context.MessageId;
		}

		// Fall back to context items for compatibility with middleware that only populates Items.
		var contextMessageId = context.GetItem<object>("MessageId");
		if (contextMessageId != null)
		{
			return ToNullableString(contextMessageId);
		}

		// Try message envelope
		var envelopeObj = context.GetItem<object>("MessageEnvelope");
		if (envelopeObj is IMessageEnvelope envelope)
		{
			return envelope.MessageId;
		}

		// Reflection-based fallback for messages that expose IDs via strongly-typed properties.
		var messageType = message.GetType();

		if (MessageIdPropertyCache.TryGetValue(messageType, out var messageIdProperty))
		{
			// Cache hit
		}
		else
		{
			messageIdProperty = messageType.GetProperty("MessageId") ?? messageType.GetProperty("CorrelationId");

			// Bounded cache: skip caching when full to prevent unbounded memory growth
			if (MessageIdPropertyCache.Count < MaxCacheEntries)
			{
				MessageIdPropertyCache.TryAdd(messageType, messageIdProperty);
			}
		}

		if (messageIdProperty != null)
		{
			var messageIdPropertyValue = messageIdProperty.GetValue(message);
			return ToNullableString(messageIdPropertyValue);
		}

		return null;
	}

	private static string? ToNullableString(object? value) => value as string ?? value?.ToString();

	/// <summary>
	/// Extracts metadata from the message and context for storage.
	/// </summary>
	private static Dictionary<string, object> ExtractMetadata(
		IDispatchMessage message,
		IMessageContext context)
	{
		var metadata = new Dictionary<string, object>(capacity: 5, comparer: StringComparer.Ordinal)
		{
			["MessageType"] = message.GetType().AssemblyQualifiedName ?? message.GetType().Name,
			["ReceivedAt"] = DateTimeOffset.UtcNow,
		};

		// Add common context values to metadata
		if (context.CorrelationId != null)
		{
			metadata["CorrelationId"] = context.CorrelationId;
		}

		if (context.CausationId != null)
		{
			metadata["CausationId"] = context.CausationId;
		}

		var tenantId = context.GetTenantId();
		if (tenantId != null)
		{
			metadata["TenantId"] = tenantId;
		}

		return metadata;
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for inbox tracing.
	/// </summary>
	private static void SetInboxActivityTags(string messageId, IDispatchMessage message)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("inbox.enabled", value: true);
		_ = activity.SetTag("inbox.message_id", messageId);
		_ = activity.SetTag("inbox.message_type", message.GetType().Name);
	}

	[LoggerMessage(MiddlewareEventId.InboxDuplicateDetected, LogLevel.Information,
		"Message {MessageId} is duplicate, skipping")]
	private static partial void LogMessageIsDuplicate(ILogger<InboxMiddleware> logger, string messageId);

	[LoggerMessage(MiddlewareEventId.InboxDuplicateDetected + 30, LogLevel.Debug,
		"Marked message {MessageId} as processed in light mode")]
	private static partial void LogMarkedMessageAsProcessedInLightMode(ILogger<InboxMiddleware> logger, string messageId);

	[LoggerMessage(MiddlewareEventId.InboxDuplicateDetected + 31, LogLevel.Warning,
		"Message {MessageId} processing failed in light mode with error: {ErrorMessage}")]
	private static partial void LogMessageProcessingFailedInLightMode(
		ILogger<InboxMiddleware> logger,
		string messageId,
		string errorMessage);

	[LoggerMessage(MiddlewareEventId.InboxDuplicateDetected + 32, LogLevel.Error,
		"Exception during light mode processing for message {MessageId}")]
	private static partial void LogExceptionDuringLightModeProcessing(
		ILogger<InboxMiddleware> logger,
		string messageId,
		Exception ex);

	[LoggerMessage(MiddlewareEventId.InboxDuplicateDetected + 33, LogLevel.Error,
		"Failed to serialize message {MessageTypeName}")]
	private static partial void LogFailedToSerializeMessage(
		ILogger<InboxMiddleware> logger,
		string messageTypeName,
		Exception ex);

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.InboxMiddlewareExecuting, LogLevel.Warning,
		"Cannot process message without identifier, skipping inbox processing")]
	private partial void LogCannotProcessMessageWithoutIdentifier();

	[LoggerMessage(MiddlewareEventId.InboxMiddlewareExecuting + 10, LogLevel.Debug,
		"Processing message {MessageId} with inbox semantics")]
	private partial void LogProcessingMessage(string messageId);

	[LoggerMessage(MiddlewareEventId.InboxMiddlewareExecuting + 11, LogLevel.Warning,
		"No inbox store or deduplicator available, processing without inbox semantics")]
	private partial void LogNoInboxStoreOrDeduplicator();

	[LoggerMessage(MiddlewareEventId.InboxMiddlewareExecuting + 12, LogLevel.Error,
		"Exception occurred during inbox processing for message {MessageId}")]
	private partial void LogExceptionDuringInboxProcessing(string messageId, Exception ex);

	[LoggerMessage(MiddlewareEventId.InboxMessageProcessed, LogLevel.Information,
		"Message {MessageId} has already been processed, skipping")]
	private partial void LogMessageAlreadyProcessed(string messageId);

	[LoggerMessage(MiddlewareEventId.InboxMessageProcessed + 20, LogLevel.Warning,
		"Message {MessageId} is already being processed, skipping")]
	private partial void LogMessageBeingProcessed(string messageId);

	[LoggerMessage(MiddlewareEventId.InboxMessageProcessed + 25, LogLevel.Warning,
		"Message {MessageId} has been stuck in Processing state beyond timeout, resetting for reprocessing")]
	private partial void LogMessageProcessingTimedOut(string messageId);

	[LoggerMessage(MiddlewareEventId.InboxMessageProcessed + 21, LogLevel.Information,
		"Message {MessageId} previously failed, will retry")]
	private partial void LogMessagePreviouslyFailed(string messageId);

	[LoggerMessage(MiddlewareEventId.MessageReceivedInInbox, LogLevel.Debug,
		"Message {MessageId} is ready for processing")]
	private partial void LogMessageReadyForProcessing(string messageId);

	[LoggerMessage(MiddlewareEventId.MessageReceivedInInbox + 20, LogLevel.Debug,
		"Unknown inbox status {Status} for message {MessageId}, treating as ready for processing")]
	private partial void LogUnknownInboxStatus(InboxStatus status, string messageId);

	[LoggerMessage(MiddlewareEventId.MessageReceivedInInbox + 30, LogLevel.Debug,
		"Created inbox entry for message {MessageId}")]
	private partial void LogCreatedInboxEntry(string messageId);

	[LoggerMessage(MiddlewareEventId.InboxMessageProcessed + 22, LogLevel.Debug,
		"Marked message {MessageId} as processed")]
	private partial void LogMarkedMessageAsProcessed(string messageId);

	[LoggerMessage(MiddlewareEventId.InboxMessageProcessed + 23, LogLevel.Warning,
		"Marked message {MessageId} as failed with error: {ErrorMessage}")]
	private partial void LogMarkedMessageAsFailed(string messageId, string errorMessage);

	[LoggerMessage(MiddlewareEventId.InboxMessageProcessed + 24, LogLevel.Error,
		"Marked message {MessageId} as failed due to exception")]
	private partial void LogMarkedMessageAsFailedDueToException(string messageId, Exception ex);

	/// <summary>
	/// Processes message using full inbox semantics with persistent storage.
	/// </summary>
	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Middleware.Inbox.InboxMiddleware.SerializeMessage(IDispatchMessage)")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Middleware.Inbox.InboxMiddleware.SerializeMessage(IDispatchMessage)")]
	private async Task<IMessageResult> ProcessWithFullInboxAsync(
		string messageId,
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		// Use message type as handler type for middleware-level inbox tracking.
		// The composite key (messageId, handlerType) supports per-handler deduplication,
		// but at middleware level we use message type as a reasonable default since
		// the specific handler is not yet known.
		var messageType = message.GetType();
		var handlerType = messageType.FullName ?? messageType.Name;

		// Single-read status lookup avoids an extra storage round-trip on the hot path.
		var existingEntry = await _inboxStore.GetEntryAsync(messageId, handlerType, cancellationToken).ConfigureAwait(false);

		if (existingEntry != null)
		{
			switch (existingEntry.Status)
			{
				case InboxStatus.Processing:
					if (existingEntry.LastAttemptAt.HasValue &&
						existingEntry.LastAttemptAt.Value + _options.ProcessingTimeout < DateTimeOffset.UtcNow)
					{
						LogMessageProcessingTimedOut(messageId);
						existingEntry.Status = InboxStatus.Received;
						break; // Fall through to reprocess
					}

					LogMessageBeingProcessed(messageId);
					return MR.Success();

				case InboxStatus.Processed:
					LogMessageAlreadyProcessed(messageId);
					return MR.Success();

				case InboxStatus.Failed:
					LogMessagePreviouslyFailed(messageId);
					break;

				case InboxStatus.Received:
					LogMessageReadyForProcessing(messageId);
					break;

				default:
					// Unknown status, treat as ready for processing
					LogUnknownInboxStatus(existingEntry.Status, messageId);
					break;
			}
		}
		else
		{
			// Create new inbox entry
			var messageTypeName = messageType.Name;
			var payload = SerializeMessage(message);
			var metadata = ExtractMetadata(message, context);

			existingEntry = await _inboxStore
				.CreateEntryAsync(messageId, handlerType, messageTypeName, payload, metadata, cancellationToken)
				.ConfigureAwait(false);

			LogCreatedInboxEntry(messageId);
		}

		// Mark as processing
		existingEntry.MarkProcessing();

		try
		{
			// Execute the message handler
			var result = await nextDelegate(message, context, cancellationToken)
				.ConfigureAwait(false);

			if (result.Succeeded)
			{
				// Mark as processed on success
				await _inboxStore.MarkProcessedAsync(messageId, handlerType, cancellationToken).ConfigureAwait(false);
				LogMarkedMessageAsProcessed(messageId);
			}
			else
			{
				// Mark as failed with error details
				var errorMessage = result.ErrorMessage ?? "Message processing failed";
				await _inboxStore.MarkFailedAsync(messageId, handlerType, errorMessage, cancellationToken).ConfigureAwait(false);
				LogMarkedMessageAsFailed(messageId, errorMessage);
			}

			return result;
		}
		catch (Exception ex)
		{
			// Mark as failed on exception
			await _inboxStore.MarkFailedAsync(messageId, handlerType, ex.Message, cancellationToken).ConfigureAwait(false);
			LogMarkedMessageAsFailedDueToException(messageId, ex);
			throw;
		}
	}

	/// <summary>
	/// Processes message using light mode with in-memory deduplication.
	/// </summary>
	private async Task<IMessageResult> ProcessWithLightModeAsync(
		string messageId,
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		var expiry = TimeSpan.FromHours(_options.DeduplicationExpiryHours);

		// Check for duplicates
		var isDuplicate = await _deduplicator.IsDuplicateAsync(messageId, expiry, cancellationToken)
			.ConfigureAwait(false);

		if (isDuplicate)
		{
			LogMessageIsDuplicate(_logger, messageId);
			return MR.Success();
		}

		try
		{
			// Execute the message handler
			var result = await nextDelegate(message, context, cancellationToken)
				.ConfigureAwait(false);

			// Mark as processed only on success to prevent duplicate processing on retry
			if (result.Succeeded)
			{
				await _deduplicator.MarkProcessedAsync(messageId, expiry, cancellationToken).ConfigureAwait(false);
				LogMarkedMessageAsProcessedInLightMode(_logger, messageId);
			}
			else
			{
				LogMessageProcessingFailedInLightMode(_logger, messageId, result.ErrorMessage ?? string.Empty);
			}

			return result;
		}
		catch (Exception ex)
		{
			LogExceptionDuringLightModeProcessing(_logger, messageId, ex);
			throw;
		}
	}

	/// <summary>
	/// Serializes the message for storage in the inbox.
	/// </summary>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	private byte[] SerializeMessage(IDispatchMessage message)
	{
		try
		{
			return JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());
		}
		catch (Exception ex)
		{
			LogFailedToSerializeMessage(_logger, message.GetType().Name, ex);
			return Encoding.UTF8.GetBytes($"{{\"error\":\"Serialization failed: {ex.Message}\"}}");
		}
	}

	/// <summary>
	/// Creates a logging scope with inbox context.
	/// </summary>
	private IDisposable? CreateInboxLoggingScope(string messageId, IDispatchMessage message)
	{
		var inboxMode = _inboxStore != null ? "Full" : "Light";
		return InboxLogScope(_logger, messageId, message.GetType().Name, true, inboxMode);
	}
}
