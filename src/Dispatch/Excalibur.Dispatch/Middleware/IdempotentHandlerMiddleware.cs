// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware that applies idempotency based on configuration or the <see cref="IdempotentAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// This middleware intercepts message processing and checks for idempotency configuration.
/// Configuration can come from two sources:
/// <list type="number">
/// <item><description><see cref="IInboxConfigurationProvider"/> - Fluent configuration (takes precedence)</description></item>
/// <item><description><see cref="IdempotentAttribute"/> - Attribute on handler class</description></item>
/// </list>
/// </para>
/// <para>
/// The middleware supports two storage modes:
/// <list type="bullet">
/// <item>
/// <description>In-memory: Fast, in-process deduplication via <see cref="IInMemoryDeduplicator"/></description>
/// </item>
/// <item>
/// <description>Persistent: Distributed deduplication via <see cref="IInboxStore"/></description>
/// </item>
/// </list>
/// </para>
/// </remarks>
[AppliesTo(MessageKinds.All)]
public sealed partial class IdempotentHandlerMiddleware : IDispatchMiddleware
{
	/// <summary>
	/// The key used to store the handler type in the message context Items dictionary.
	/// </summary>
	public const string HandlerTypeKey = "Excalibur.Dispatch.HandlerType";

	private readonly IInboxStore? _inboxStore;

	private readonly IInMemoryDeduplicator _inMemoryDeduplicator;

	private readonly IMessageIdProvider? _messageIdProvider;

	private readonly IInboxConfigurationProvider? _configurationProvider;

	private readonly ILogger<IdempotentHandlerMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="IdempotentHandlerMiddleware"/> class.
	/// </summary>
	/// <param name="inMemoryDeduplicator"> The in-memory deduplicator service. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="inboxStore"> The optional persistent inbox store. </param>
	/// <param name="messageIdProvider"> The optional custom message ID provider. </param>
	/// <param name="configurationProvider"> The optional inbox configuration provider. </param>
	public IdempotentHandlerMiddleware(
		IInMemoryDeduplicator inMemoryDeduplicator,
		ILogger<IdempotentHandlerMiddleware> logger,
		IInboxStore? inboxStore = null,
		IMessageIdProvider? messageIdProvider = null,
		IInboxConfigurationProvider? configurationProvider = null)
	{
		_inMemoryDeduplicator = inMemoryDeduplicator ?? throw new ArgumentNullException(nameof(inMemoryDeduplicator));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_inboxStore = inboxStore;
		_messageIdProvider = messageIdProvider;
		_configurationProvider = configurationProvider;
	}

	/// <inheritdoc />
	/// <remarks>
	/// The middleware runs just before handler execution to check for duplicates.
	/// Using Processing - 1 ensures it runs after routing but before the handler.
	/// </remarks>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing - 1;

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

		// 1. Get handler type from context
		var handlerType = GetHandlerType(context);
		if (handlerType is null)
		{
			// No handler type available, pass through
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// 2. Get configuration - ConfigureInbox takes precedence over [Idempotent] attribute
		var (settings, isConfigured) = GetIdempotencySettings(handlerType);
		if (!isConfigured)
		{
			// Handler is not configured for idempotency, pass through
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// 3. Extract message ID based on strategy
		var messageId = ExtractMessageId(message, context, handlerType, settings);
		if (string.IsNullOrEmpty(messageId))
		{
			LogNoMessageId(handlerType.Name);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// 4. Check if already processed
		var handlerTypeName = handlerType.FullName ?? handlerType.Name;

		bool isDuplicate;
		if (settings.UseInMemory)
		{
			isDuplicate = await _inMemoryDeduplicator.IsDuplicateAsync(messageId, settings.Retention, cancellationToken)
				.ConfigureAwait(false);
		}
		else if (_inboxStore is not null)
		{
			isDuplicate = await _inboxStore.IsProcessedAsync(messageId, handlerTypeName, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			// No inbox store configured, fall back to in-memory
			isDuplicate = await _inMemoryDeduplicator.IsDuplicateAsync(messageId, settings.Retention, cancellationToken)
				.ConfigureAwait(false);
		}

		if (isDuplicate)
		{
			LogDuplicateSkipped(messageId, handlerType.Name);
			return MessageResult.Success(); // Skip duplicate
		}

		// 5. Invoke handler
		var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		// 6. Mark as processed on success
		if (result.Succeeded)
		{
			if (settings.UseInMemory)
			{
				await _inMemoryDeduplicator.MarkProcessedAsync(messageId, settings.Retention, cancellationToken).ConfigureAwait(false);
			}
			else if (_inboxStore is not null)
			{
				_ = await _inboxStore.TryMarkAsProcessedAsync(messageId, handlerTypeName, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// Fall back to in-memory
				await _inMemoryDeduplicator.MarkProcessedAsync(messageId, settings.Retention, cancellationToken).ConfigureAwait(false);
			}

			LogMessageProcessed(messageId, handlerType.Name);
		}

		return result;
	}

	private static Type? GetHandlerType(IMessageContext context)
	{
		// Try to get handler type from context Items
		if (context.Items.TryGetValue(HandlerTypeKey, out var value) && value is Type handlerType)
		{
			return handlerType;
		}

		return null;
	}

	private static string? GetFromHeader(IMessageContext context, string headerName)
	{
		// First try MessageId if header name matches
		if (string.Equals(headerName, "MessageId", StringComparison.OrdinalIgnoreCase))
		{
			return context.MessageId;
		}

		// Try to get from Items dictionary
		if (context.Items.TryGetValue(headerName, out var value))
		{
			return value?.ToString();
		}

		return null;
	}

	private static string? CreateCompositeKey(IMessageContext context, Type handlerType)
	{
		var correlationId = context.CorrelationId;
		if (string.IsNullOrEmpty(correlationId))
		{
			correlationId = context.MessageId;
		}

		if (string.IsNullOrEmpty(correlationId))
		{
			return null;
		}

		return $"{handlerType.Name}:{correlationId}";
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.DuplicateRequestDetected, LogLevel.Information,
		"Duplicate message {MessageId} skipped for handler {HandlerType}")]
	private partial void LogDuplicateSkipped(string messageId, string handlerType);

	[LoggerMessage(MiddlewareEventId.IdempotencyCheckPassed, LogLevel.Debug,
		"Message {MessageId} processed for handler {HandlerType}")]
	private partial void LogMessageProcessed(string messageId, string handlerType);

	[LoggerMessage(MiddlewareEventId.IdempotencyKeyGenerated, LogLevel.Debug,
		"No message ID available for idempotency check on handler {HandlerType}")]
	private partial void LogNoMessageId(string handlerType);

	/// <summary>
	/// Gets idempotency settings from ConfigureInbox or [Idempotent] attribute.
	/// </summary>
	private (InboxHandlerSettings Settings, bool IsConfigured) GetIdempotencySettings(Type handlerType)
	{
		// 1. First, check IInboxConfigurationProvider (ConfigureInbox takes precedence)
		if (_configurationProvider is not null)
		{
			var providerSettings = _configurationProvider.GetConfiguration(handlerType);
			if (providerSettings is not null)
			{
				return (providerSettings, true);
			}
		}

		// 2. Fall back to [Idempotent] attribute on handler class
		var attribute = handlerType.GetCustomAttribute<IdempotentAttribute>();
		if (attribute is not null)
		{
			var settings = new InboxHandlerSettings
			{
				Retention = TimeSpan.FromMinutes(attribute.RetentionMinutes),
				UseInMemory = attribute.UseInMemory,
				Strategy = attribute.Strategy,
				HeaderName = attribute.HeaderName,
			};
			return (settings, true);
		}

		// No configuration
		return (default!, false);
	}

	private string? ExtractMessageId(
		IDispatchMessage message,
		IMessageContext context,
		Type handlerType,
		InboxHandlerSettings settings)
	{
		return settings.Strategy switch
		{
			MessageIdStrategy.FromHeader => GetFromHeader(context, settings.HeaderName),
			MessageIdStrategy.FromCorrelationId => context.CorrelationId,
			MessageIdStrategy.CompositeKey => CreateCompositeKey(context, handlerType),
			MessageIdStrategy.Custom => _messageIdProvider?.GetMessageId(message, context),
			_ => context.MessageId,
		};
	}
}
