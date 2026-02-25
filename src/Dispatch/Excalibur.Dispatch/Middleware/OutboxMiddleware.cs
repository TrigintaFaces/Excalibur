// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OutboxOptions = Excalibur.Dispatch.Options.Middleware.OutboxOptions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for implementing the Transactional Outbox pattern to ensure reliable message publishing.
/// </summary>
/// <remarks>
/// This middleware stages outbound messages in the outbox store during message processing, ensuring:
/// <list type="bullet">
/// <item> <strong> Atomicity </strong>: Messages are only published if the business operation succeeds </item>
/// <item> <strong> Reliability </strong>: Messages are not lost due to transport failures </item>
/// <item> <strong> Consistency </strong>: Outbound messages reflect actual state changes </item>
/// <item> <strong> Order Preservation </strong>: Messages are published in the correct sequence </item>
/// </list>
/// The outbox middleware captures outgoing messages during handler execution and stages them for later delivery by a background service.
/// This decouples message publishing from the main processing flow and provides guaranteed delivery semantics.
/// </remarks>
[AppliesTo(MessageKinds.Action | MessageKinds.Event)]
[RequiresFeatures(DispatchFeatures.Outbox)]
public sealed partial class OutboxMiddleware : IDispatchMiddleware
{
	private readonly OutboxOptions _options;
	private readonly IOutboxStore? _outboxStore;
	private readonly ILogger<OutboxMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxMiddleware" /> class. Creates a new outbox middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for outbox behavior. </param>
	/// <param name="outboxStore"> Optional persistent outbox store for message staging. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public OutboxMiddleware(
		IOptions<OutboxOptions> options,
		IOutboxStore? outboxStore,
		ILogger<OutboxMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_outboxStore = outboxStore;
		_logger = logger;

		// Validate configuration
		if (_options.Enabled && _outboxStore == null)
		{
			throw new InvalidOperationException(
				Resources.OutboxMiddleware_StoreNotRegistered);
		}
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Skip outbox processing if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Check if this is an outbound message that should be staged
		if (!ShouldStageMessage(message, context))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Set up logging scope and activity tags
		using var logScope = CreateOutboxLoggingScope(message, context);
		SetOutboxActivityTags(message, context);

		LogProcessingMessage(message.GetType().Name);

		try
		{
			// Execute the handler first
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			// Only stage outbound messages if the handler succeeded
			if (result.Succeeded)
			{
				await StageOutboundMessagesAsync(context, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				LogHandlerFailed(message.GetType().Name);
			}

			return result;
		}
		catch (Exception ex)
		{
			LogOutboxProcessingError(message.GetType().Name, ex);
			throw;
		}
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for outbox tracing.
	/// </summary>
	private static void SetOutboxActivityTags(IDispatchMessage message, IMessageContext context)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("outbox.enabled", value: true);
		_ = activity.SetTag("outbox.message_type", message.GetType().Name);

		var outboundCount = context.GetItem<List<OutboundMessage>>("OutboundMessages")?.Count ?? 0;
		_ = activity.SetTag("outbox.outbound_count", outboundCount);
	}

	/// <summary>
	/// Determines if a message should trigger outbox staging.
	/// </summary>
	private bool ShouldStageMessage(IDispatchMessage message, IMessageContext context)
	{
		// Check if message bypasses outbox
		var messageType = message.GetType();
		if (messageType.GetCustomAttributes(typeof(BypassOutboxAttribute), inherit: true).Length != 0)
		{
			return false;
		}

		// Check if message type is in bypass list
		if (_options.BypassOutboxForTypes?.Contains(messageType.Name) == true)
		{
			return false;
		}

		// Check context flag
		var bypassOutbox = context.GetItem<bool?>("BypassOutbox");
		if (bypassOutbox == true)
		{
			return false;
		}

		// Only stage if this is a command/event that might produce outbound messages
		return message is IDispatchAction or IDispatchEvent;
	}

	/// <summary>
	/// Stages any outbound messages that were queued during handler execution.
	/// </summary>
	private async Task StageOutboundMessagesAsync(IMessageContext context, CancellationToken cancellationToken)
	{
		// Check if any outbound messages were queued during processing
		var outboundMessages = context.GetItem<List<OutboundMessage>>("OutboundMessages");
		if (outboundMessages == null || outboundMessages.Count == 0)
		{
			LogNoOutboundMessages();
			return;
		}

		LogStagingMessages(outboundMessages.Count);

		// Stage each message in the outbox
		foreach (var outboundMessage in outboundMessages)
		{
			try
			{
				// Set correlation and causation IDs if not already set
				if (string.IsNullOrEmpty(outboundMessage.CorrelationId))
				{
					outboundMessage.CorrelationId = context.CorrelationId;
				}

				if (string.IsNullOrEmpty(outboundMessage.CausationId))
				{
					outboundMessage.CausationId = context.GetItem<string>("MessageId");
				}

				if (string.IsNullOrEmpty(outboundMessage.TenantId))
				{
					outboundMessage.TenantId = context.TenantId;
				}

				// Apply message priority from options
				if (outboundMessage.Priority == 0 && _options.DefaultPriority > 0)
				{
					outboundMessage.Priority = _options.DefaultPriority;
				}

				// Stage the message
				await _outboxStore.StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);

				LogStagedMessage(outboundMessage.Id, outboundMessage.MessageType, outboundMessage.Destination);
			}
			catch (Exception ex)
			{
				LogStageMessageError(outboundMessage.Id, outboundMessage.MessageType, ex);

				if (!_options.ContinueOnStagingError)
				{
					throw;
				}
			}
		}

		// Clear the outbound messages from context after staging
		context.SetItem<List<OutboundMessage>?>("OutboundMessages", value: null);

		LogStagingSuccess(outboundMessages.Count);
	}

	/// <summary>
	/// Creates a logging scope with outbox context.
	/// </summary>
	private IDisposable? CreateOutboxLoggingScope(IDispatchMessage message, IMessageContext context)
	{
		var scopeProperties =
			new Dictionary<string, object>(StringComparer.Ordinal) { ["MessageType"] = message.GetType().Name, ["OutboxEnabled"] = true };

		if (context.CorrelationId != null)
		{
			scopeProperties["CorrelationId"] = context.CorrelationId;
		}

		return _logger.BeginScope(scopeProperties);
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.OutboxMiddlewareExecuting, LogLevel.Debug,
		"Processing message {MessageType} with outbox semantics")]
	private partial void LogProcessingMessage(string messageType);

	[LoggerMessage(MiddlewareEventId.OutboxMiddlewareExecuting + 10, LogLevel.Warning,
		"Outbox is disabled for message type {MessageType}, outbound messages will not be staged")]
	private partial void LogOutboxDisabled(string messageType);

	[LoggerMessage(MiddlewareEventId.OutboxStagingFailed, LogLevel.Error,
		"Exception occurred during outbox processing for message {MessageType}")]
	private partial void LogOutboxProcessingError(string messageType, Exception ex);

	[LoggerMessage(MiddlewareEventId.OutboxStagingCompleted, LogLevel.Debug,
		"No outbound messages to stage")]
	private partial void LogNoOutboundMessages();

	[LoggerMessage(MiddlewareEventId.MessageStagedInOutbox, LogLevel.Debug,
		"Staging {MessageCount} outbound messages")]
	private partial void LogStagingMessages(int messageCount);

	[LoggerMessage(MiddlewareEventId.MessageStagedInOutbox + 10, LogLevel.Debug,
		"Staged outbound message {MessageId} of type {MessageType} to {Destination}")]
	private partial void LogStagedMessage(string messageId, string messageType, string destination);

	[LoggerMessage(MiddlewareEventId.OutboxStagingFailed + 10, LogLevel.Error,
		"Failed to stage outbound message {MessageId} of type {MessageType}")]
	private partial void LogStageMessageError(string messageId, string messageType, Exception ex);

	[LoggerMessage(MiddlewareEventId.OutboxStagingCompleted + 10, LogLevel.Information,
		"Successfully staged {MessageCount} outbound messages")]
	private partial void LogStagingSuccess(int messageCount);

	[LoggerMessage(MiddlewareEventId.OutboxStagingFailed + 11, LogLevel.Warning,
		"Handler failed for message {MessageType}, skipping outbox staging")]
	private partial void LogHandlerFailed(string messageType);
}
