// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;
using MessageKinds = Excalibur.Dispatch.Abstractions.MessageKinds;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for staging outbound messages in an outbox for reliable, transactional message publishing using the Transactional
/// Outbox pattern.
/// </summary>
/// <remarks>
/// This middleware intercepts messages that need to be published as side effects of message processing and stages them in a persistent
/// outbox within the same transaction as the main processing. This ensures:
/// <list type="bullet">
/// <item> Atomic operations - messages are published only if processing succeeds </item>
/// <item> Reliability - messages are not lost due to transport failures </item>
/// <item> Consistency - outbound messages reflect the actual state changes </item>
/// <item> Idempotency - duplicate processing doesn't create duplicate messages </item>
/// <item> Ordering - messages are published in the correct sequence </item>
/// </list>
/// The staged messages are later published by a background service that polls the outbox and marks messages as sent after successful delivery.
/// </remarks>
public sealed partial class OutboxStagingMiddleware : IDispatchMiddleware
{
	private readonly OutboxStagingOptions _options;
	private readonly IOutboxStore? _outboxStore;

	/// <summary>
	/// Keep for backward compatibility.
	/// </summary>
	private readonly IOutboxService? _outboxService;

	private readonly ILogger<OutboxStagingMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxStagingMiddleware"/> class.
	/// Creates a new outbox staging middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for outbox staging. </param>
	/// <param name="outboxStore"> Store for managing outbox operations (preferred). </param>
	/// <param name="outboxService"> Legacy service for managing outbox operations. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public OutboxStagingMiddleware(
		IOptions<OutboxStagingOptions> options,
		IOutboxStore? outboxStore,
		IOutboxService? outboxService,
		ILogger<OutboxStagingMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_outboxStore = outboxStore;
		_outboxService = outboxService;
		_logger = logger;

		// Validate that at least one outbox implementation is available
		if (_options.Enabled && _outboxStore == null && _outboxService == null)
		{
			throw new InvalidOperationException(
				Resources.OutboxStagingMiddleware_NoOutboxServices);
		}
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Outbox staging uses runtime message serialization for legacy compatibility.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Outbox staging uses runtime message serialization for legacy compatibility.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Skip outbox staging if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Set up outbox context for downstream middleware and handlers
		var outboxContext = CreateOutboxContext(message, context);
		SetOutboxContext(context, outboxContext);

		// Set up logging scope
		using var logScope = CreateOutboxLoggingScope(message);

		// Set up OpenTelemetry activity tags
		SetOutboxActivityTags(message);

		LogProcessingWithOutboxEnabled(message.GetType().Name);

		try
		{
			// Continue pipeline execution with outbox context available
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			// Stage any outbound messages that were added during processing
			if (_outboxStore != null)
			{
				await StageOutboundMessagesWithStoreAsync(outboxContext, context, cancellationToken).ConfigureAwait(false);
			}
			else if (_outboxService != null)
			{
				await StageOutboundMessagesAsync(outboxContext, context, cancellationToken).ConfigureAwait(false);
			}

			LogOutboxStagingCompleted(message.GetType().Name, outboxContext.OutboundMessages.Count);

			return result;
		}
		catch (Exception ex)
		{
			LogExceptionDuringOutboxStaging(message.GetType().Name, ex);
			throw;
		}
	}

	/// <summary>
	/// Gets a property value from the message context.
	/// </summary>
	private static string? GetPropertyValue(IMessageContext context, string propertyName)
	{
		var value = context.GetItem<object>(propertyName);
		return value?.ToString();
	}

	/// <summary>
	/// Sets outbox context in the message context for downstream access.
	/// </summary>
	private static void SetOutboxContext(IMessageContext context, OutboxContext outboxContext)
	{
		context.SetItem("OutboxContext", outboxContext);
		context.SetItem("OutboxEnabled", value: true);
	}

	/// <summary>
	/// Gets the transaction from the message context, if available.
	/// </summary>
	private static object? GetTransactionFromContext(IMessageContext context) => context.GetItem<object>("Transaction");

	/// <summary>
	/// Creates message headers for the outbox store.
	/// </summary>
	private static Dictionary<string, object> CreateMessageHeaders(OutboxContext outboxContext, OutboundMessageRequest outboundMessage)
	{
		var headers = new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["MessageType"] = outboundMessage.Message.GetType().AssemblyQualifiedName ?? outboundMessage.Message.GetType().Name,
			["SourceMessageType"] = outboxContext.SourceMessageType,
			["CreatedAt"] = DateTimeOffset.UtcNow,
		};

		if (outboxContext.CorrelationId != null)
		{
			headers["CorrelationId"] = outboxContext.CorrelationId;
		}

		if (outboxContext.CausationId != null)
		{
			headers["CausationId"] = outboxContext.CausationId;
		}

		if (outboxContext.TenantId != null)
		{
			headers["TenantId"] = outboxContext.TenantId;
		}

		return headers;
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for outbox tracing.
	/// </summary>
	private static void SetOutboxActivityTags(IDispatchMessage message)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("outbox.enabled", value: true);
		_ = activity.SetTag("outbox.message_type", message.GetType().Name);
	}

	/// <summary>
	/// Creates an outbox context for tracking outbound messages.
	/// </summary>
	private static OutboxContext CreateOutboxContext(IDispatchMessage message, IMessageContext context)
	{
		var correlationId = GetPropertyValue(context, "CorrelationId");
		var causationId = GetPropertyValue(context, "MessageId"); // Current message becomes causation for outbound
		var tenantId = GetPropertyValue(context, "TenantId");

		return new OutboxContext(
			correlationId,
			causationId,
			tenantId,
			message.GetType().Name);
	}

	/// <summary>
	/// Stages all outbound messages using the new IOutboxStore interface.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Context parameter reserved for future message context enrichment during staging")]
	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Middleware.OutboxStagingMiddleware.SerializeMessageToBytes(IDispatchMessage)")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Middleware.OutboxStagingMiddleware.SerializeMessageToBytes(IDispatchMessage)")]
	private async Task StageOutboundMessagesWithStoreAsync(
		OutboxContext outboxContext,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		if (outboxContext.OutboundMessages.Count == 0)
		{
			LogNoOutboundMessagesToStage();
			return;
		}

		LogStagingOutboundMessagesWithStore(outboxContext.OutboundMessages.Count);

		foreach (var outboundMessage in outboxContext.OutboundMessages)
		{
			try
			{
				// Create outbound message for new store interface
				var messageType = outboundMessage.Message.GetType().Name;
				var payload = SerializeMessageToBytes(outboundMessage.Message);
				var headers = CreateMessageHeaders(outboxContext, outboundMessage);

				var storeMessage = new OutboundMessage(
					messageType,
					payload,
					outboundMessage.Destination ?? "default",
					headers)
				{
					CorrelationId = outboxContext.CorrelationId,
					CausationId = outboxContext.CausationId,
					TenantId = outboxContext.TenantId,
					ScheduledAt = outboundMessage.ScheduledAt,
				};

				// Stage message in outbox store
				await _outboxStore.StageMessageAsync(storeMessage, cancellationToken).ConfigureAwait(false);

				LogStagedOutboundMessageWithStore(outboundMessage.Message.GetType().Name, storeMessage.Id);
			}
			catch (Exception ex)
			{
				LogFailedToStageWithStore(outboundMessage.Message.GetType().Name, ex);
				throw;
			}
		}
	}

	/// <summary>
	/// Stages all outbound messages that were added during message processing (legacy method).
	/// </summary>
	[RequiresUnreferencedCode(
		"Calls Excalibur.Dispatch.Middleware.OutboxStagingMiddleware.SerializeMessageAsync(IDispatchMessage, CancellationToken)")]
	[RequiresDynamicCode(
		"Calls Excalibur.Dispatch.Middleware.OutboxStagingMiddleware.SerializeMessageAsync(IDispatchMessage, CancellationToken)")]
	private async Task StageOutboundMessagesAsync(
		OutboxContext outboxContext,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		if (outboxContext.OutboundMessages.Count == 0)
		{
			LogNoOutboundMessagesToStage();
			return;
		}

		LogStagingOutboundMessages(outboxContext.OutboundMessages.Count);

		// Get transaction context if available (for transactional consistency)
		var transaction = GetTransactionFromContext(context);

		foreach (var outboundMessage in outboxContext.OutboundMessages)
		{
			try
			{
				// Create outbox entry with correlation and causation context
				var outboxEntry = new OutboxEntry(
					id: Guid.NewGuid().ToString(),
					messageType: outboundMessage.Message.GetType().Name,
					messageData: await SerializeMessageAsync(
						outboundMessage.Message,
						cancellationToken).ConfigureAwait(false),
					correlationId: outboxContext.CorrelationId,
					causationId: outboxContext.CausationId,
					tenantId: outboxContext.TenantId,
					destination: outboundMessage.Destination,
					scheduledAt: outboundMessage.ScheduledAt ?? DateTimeOffset.UtcNow,
					createdAt: DateTimeOffset.UtcNow);

				// Stage message in outbox
				await _outboxService.StageMessageAsync(outboxEntry, transaction, cancellationToken).ConfigureAwait(false);

				LogStagedOutboundMessage(outboundMessage.Message.GetType().Name, outboxEntry.Id);
			}
			catch (Exception ex)
			{
				LogFailedToStageMessage(outboundMessage.Message.GetType().Name, ex);
				throw;
			}
		}
	}

	/// <summary>
	/// Serializes a message to bytes for the new outbox store.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	private byte[] SerializeMessageToBytes(IDispatchMessage message)
	{
		try
		{
			var json = JsonSerializer.Serialize(message, message.GetType());
			return Encoding.UTF8.GetBytes(json);
		}
		catch (Exception ex)
		{
			LogFailedToSerializeMessage(message.GetType().Name, ex);
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					Resources.OutboxStagingMiddleware_FailedToSerializeMessage,
					message.GetType().Name),
				ex);
		}
	}

	/// <summary>
	/// Serializes a message for storage in the outbox (legacy method).
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	[SuppressMessage("Style", "RCS1163:Unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency and future cancellable serialization support")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency and future cancellable serialization support")]
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	private Task<string> SerializeMessageAsync(
		IDispatchMessage message,
		CancellationToken cancellationToken)
	{
		// This would typically use your configured message serializer For now, we'll use a simple JSON serialization approach
		try
		{
			// This is a placeholder - would be replaced with your actual serialization logic
			var json = JsonSerializer.Serialize(message, message.GetType());
			return Task.FromResult(json);
		}
		catch (Exception ex)
		{
			LogFailedToSerializeMessage(message.GetType().Name, ex);
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					Resources.OutboxStagingMiddleware_FailedToSerializeMessage,
					message.GetType().Name),
				ex);
		}
	}

	/// <summary>
	/// Creates a logging scope with outbox context.
	/// </summary>
	private IDisposable? CreateOutboxLoggingScope(IDispatchMessage message)
	{
		var scopeProperties =
			new Dictionary<string, object>(StringComparer.Ordinal) { ["MessageType"] = message.GetType().Name, ["OutboxEnabled"] = true };

		return _logger.BeginScope(scopeProperties);
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.OutboxMiddlewareExecuting, LogLevel.Debug,
		"Processing message {MessageType} with outbox staging enabled")]
	private partial void LogProcessingWithOutboxEnabled(string messageType);

	[LoggerMessage(MiddlewareEventId.OutboxStagingCompleted, LogLevel.Debug,
		"Outbox staging completed for message {MessageType} with {MessageCount} outbound messages")]
	private partial void LogOutboxStagingCompleted(string messageType, int messageCount);

	[LoggerMessage(MiddlewareEventId.OutboxStagingFailed, LogLevel.Error,
		"Exception occurred during outbox staging for message {MessageType}")]
	private partial void LogExceptionDuringOutboxStaging(string messageType, Exception ex);

	[LoggerMessage(MiddlewareEventId.OutboxStagingCompleted + 20, LogLevel.Debug,
		"No outbound messages to stage")]
	private partial void LogNoOutboundMessagesToStage();

	[LoggerMessage(MiddlewareEventId.MessageStagedInOutbox + 20, LogLevel.Debug,
		"Staging {MessageCount} outbound messages in outbox using store")]
	private partial void LogStagingOutboundMessagesWithStore(int messageCount);

	[LoggerMessage(MiddlewareEventId.MessageStagedInOutbox + 30, LogLevel.Debug,
		"Staged outbound message {MessageType} in outbox with ID {MessageId}")]
	private partial void LogStagedOutboundMessageWithStore(string messageType, string messageId);

	[LoggerMessage(MiddlewareEventId.OutboxStagingFailed + 20, LogLevel.Error,
		"Failed to stage outbound message {MessageType} in outbox store")]
	private partial void LogFailedToStageWithStore(string messageType, Exception ex);

	[LoggerMessage(MiddlewareEventId.MessageStagedInOutbox + 31, LogLevel.Debug,
		"Staging {MessageCount} outbound messages in outbox")]
	private partial void LogStagingOutboundMessages(int messageCount);

	[LoggerMessage(MiddlewareEventId.MessageStagedInOutbox + 32, LogLevel.Debug,
		"Staged outbound message {MessageType} in outbox with ID {OutboxEntryId}")]
	private partial void LogStagedOutboundMessage(string messageType, string outboxEntryId);

	[LoggerMessage(MiddlewareEventId.OutboxStagingFailed + 40, LogLevel.Error,
		"Failed to stage outbound message {MessageType} in outbox")]
	private partial void LogFailedToStageMessage(string messageType, Exception ex);

	[LoggerMessage(MiddlewareEventId.OutboxStagingFailed + 41, LogLevel.Error,
		"Failed to serialize message {MessageType} for outbox")]
	private partial void LogFailedToSerializeMessage(string messageType, Exception ex);
}
