// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SkipBehavior = Excalibur.Dispatch.Messaging.SkipBehavior;

namespace Excalibur.Dispatch.Middleware.Inbox;

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
/// <para>
/// <b>Delivery guarantee (honest contract — j8xcrn).</b> The claim is taken atomically <em>before</em> the
/// handler runs and the message is marked processed <em>after</em> the handler succeeds — two separate steps,
/// not a single transaction. Therefore the guarantee is:
/// <list type="bullet">
/// <item><description><b>Exactly-once for concurrent redelivery</b>: the atomic claim
/// (<see cref="Excalibur.Dispatch.IClaimableInboxStore"/> / <see cref="IClaimableDeduplicator"/>) blocks the
/// second of N simultaneous duplicates, so only one handler invocation runs concurrently.</description></item>
/// <item><description><b>At-least-once across a process crash</b>: if the process dies after the claim but
/// before mark-processed, the claim is reclaimed after its timeout and the handler runs again. <b>Handlers
/// must therefore be idempotent.</b></description></item>
/// </list>
/// True exactly-once across a crash would require a single handler+mark transaction per provider (tracked
/// separately); this middleware does not promise it. A store that lacks the atomic-claim capability fails
/// fast at startup (see the idempotency claim-capability validator) rather than silently degrading.
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

	private readonly SkipBehavior _duplicateBehavior;

	private readonly ILogger<IdempotentHandlerMiddleware> _logger;

	private volatile bool _inMemoryFallbackWarned;

	/// <summary>
	/// Initializes a new instance of the <see cref="IdempotentHandlerMiddleware"/> class.
	/// </summary>
	/// <param name="inboxOptions"> The inbox options controlling duplicate behavior. </param>
	/// <param name="inMemoryDeduplicator"> The in-memory deduplicator service. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="inboxStore"> The optional persistent inbox store. </param>
	/// <param name="messageIdProvider"> The optional custom message ID provider. </param>
	/// <param name="configurationProvider"> The optional inbox configuration provider. </param>
	public IdempotentHandlerMiddleware(
		IOptions<InboxOptions> inboxOptions,
		IInMemoryDeduplicator inMemoryDeduplicator,
		ILogger<IdempotentHandlerMiddleware> logger,
		IInboxStore? inboxStore = null,
		IMessageIdProvider? messageIdProvider = null,
		IInboxConfigurationProvider? configurationProvider = null)
	{
		ArgumentNullException.ThrowIfNull(inboxOptions);
		_duplicateBehavior = inboxOptions.Value.DuplicateBehavior;
		_inMemoryDeduplicator = inMemoryDeduplicator ?? throw new ArgumentNullException(nameof(inMemoryDeduplicator));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_inboxStore = inboxStore;
		_messageIdProvider = messageIdProvider;
		_configurationProvider = configurationProvider;
	}

	/// <inheritdoc />
	/// <remarks>
	/// The middleware runs just before handler execution to check for duplicates.
	/// </remarks>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Deduplication;

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

		// 4. Atomically CLAIM the message BEFORE executing the handler (claim-before-execute).
		//    Exactly one of N concurrent duplicates wins the claim; the rest are treated as duplicates.
		//    On handler failure the claim is RELEASED so a redelivery can re-admit the message -- a
		//    claim-then-leave-terminal would silently drop a message whose handler failed.
		LogIdempotencyExecuting(messageId, handlerType.Name);
		var handlerTypeName = handlerType.FullName ?? handlerType.Name;

		// Resolve the atomic-claim capability for the active storage path.
		IClaimableInboxStore? inboxClaim = null;
		IClaimableDeduplicator? dedupClaim = null;
		bool usingInMemoryDedup;

		if (settings.UseInMemory)
		{
			usingInMemoryDedup = true;
			dedupClaim = _inMemoryDeduplicator as IClaimableDeduplicator;
		}
		else if (_inboxStore is not null)
		{
			usingInMemoryDedup = false;
			inboxClaim = _inboxStore as IClaimableInboxStore;
		}
		else
		{
			WarnInMemoryFallback();
			usingInMemoryDedup = true;
			dedupClaim = _inMemoryDeduplicator as IClaimableDeduplicator;
		}

		// Fail LOUD (never a silent non-atomic fallback) when the in-memory idempotency path is selected but the
		// registered deduplicator cannot claim atomically. Falling through to the check-then-act legacy path here
		// would silently degrade idempotency — under concurrent duplicate delivery two callers could both observe
		// "not duplicate" and both execute the handler. Making that configuration inexpressible at the point of use
		// (ADR-336 clause 2; mirrors the inbox-store startup guard and the pfb7s4 wired-or-fail-loud precedent)
		// covers BOTH idempotency-config sources — ConfigureInbox and the [Idempotent] attribute — because it acts
		// on the already-resolved settings, with no startup enumeration. The inbox-store path keeps its existing
		// legacy fallback (guarded separately at startup by IdempotencyClaimCapabilityValidator).
		if (usingInMemoryDedup && dedupClaim is null)
		{
			throw new InvalidOperationException(
				$"Handler '{handlerType.FullName}' is configured for in-memory idempotency (UseInMemory) but the " +
				$"registered IInMemoryDeduplicator ('{_inMemoryDeduplicator.GetType().FullName}') does not implement " +
				"IClaimableDeduplicator. Without atomic claiming it would fall back to a non-atomic check-then-act " +
				"under which concurrent duplicates can both execute the handler. Register a claim-capable deduplicator " +
				"(the default InMemoryDeduplicator supports atomic claiming).");
		}

		// Legacy fallback for a custom INBOX store that does not implement the atomic-claim capability. The
		// in-memory dedup path is now fail-loud above, so only the inbox-store path reaches here; registered
		// persistent stores are guarded at startup (fail-fast), so this preserves prior check-then-act behavior
		// only for non-conforming custom inbox stores.
		if (inboxClaim is null && dedupClaim is null)
		{
			return await InvokeLegacyAsync(
					message, context, nextDelegate, handlerType, handlerTypeName, messageId, settings, cancellationToken)
				.ConfigureAwait(false);
		}

		var claimed = dedupClaim is not null
			? await dedupClaim.TryClaimAsync(messageId, settings.Retention, cancellationToken).ConfigureAwait(false)
			: await inboxClaim!.TryClaimAsync(messageId, handlerTypeName, cancellationToken).ConfigureAwait(false);

		if (!claimed)
		{
			switch (_duplicateBehavior)
			{
				case SkipBehavior.Silent:
					break;

				case SkipBehavior.LogOnly:
					LogDuplicateSkipped(messageId, handlerType.Name);
					break;

				case SkipBehavior.ThrowOnDuplicate:
					LogDuplicateSkipped(messageId, handlerType.Name);
					throw new DuplicateMessageException(messageId);
			}

			return MessageResult.Success();
		}

		// 5. Invoke the handler while holding the claim.
		IMessageResult result;
		try
		{
			result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			// Handler threw -- release the claim so the message stays retryable on redelivery.
			await ReleaseClaimAsync(inboxClaim, dedupClaim, messageId, handlerTypeName, cancellationToken).ConfigureAwait(false);
			throw;
		}

		// 6. Finalize the claim on success; release it on failure (preserve at-least-once-until-success).
		if (result.Succeeded)
		{
			if (inboxClaim is not null)
			{
				// Finalize the persistent claim: Processing -> Processed.
				await _inboxStore!.MarkProcessedAsync(messageId, handlerTypeName, cancellationToken).ConfigureAwait(false);
			}

			// In-memory deduplicator path: the successful claim IS the dedup marker -- nothing to finalize.
			LogMessageProcessed(messageId, handlerType.Name);
		}
		else
		{
			await ReleaseClaimAsync(inboxClaim, dedupClaim, messageId, handlerTypeName, cancellationToken).ConfigureAwait(false);
		}

		return result;
	}

	private static async ValueTask ReleaseClaimAsync(
		IClaimableInboxStore? inboxClaim,
		IClaimableDeduplicator? dedupClaim,
		string messageId,
		string handlerTypeName,
		CancellationToken cancellationToken)
	{
		if (inboxClaim is not null)
		{
			await inboxClaim.ReleaseAsync(messageId, handlerTypeName, cancellationToken).ConfigureAwait(false);
		}
		else if (dedupClaim is not null)
		{
			await dedupClaim.ReleaseAsync(messageId, cancellationToken).ConfigureAwait(false);
		}
	}

	// Backward-compatible non-atomic check-then-act path for custom stores/deduplicators that do not
	// implement the atomic-claim capability. Conforming stores use the atomic claim protocol above.
	private async ValueTask<IMessageResult> InvokeLegacyAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		Type handlerType,
		string handlerTypeName,
		string messageId,
		InboxHandlerSettings settings,
		CancellationToken cancellationToken)
	{
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
			WarnInMemoryFallback();
			isDuplicate = await _inMemoryDeduplicator.IsDuplicateAsync(messageId, settings.Retention, cancellationToken)
				.ConfigureAwait(false);
		}

		if (isDuplicate)
		{
			switch (_duplicateBehavior)
			{
				case SkipBehavior.Silent:
					break;

				case SkipBehavior.LogOnly:
					LogDuplicateSkipped(messageId, handlerType.Name);
					break;

				case SkipBehavior.ThrowOnDuplicate:
					LogDuplicateSkipped(messageId, handlerType.Name);
					throw new DuplicateMessageException(messageId);
			}

			return MessageResult.Success();
		}

		var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

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
				WarnInMemoryFallback();
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
	[LoggerMessage(MiddlewareEventId.IdempotencyMiddlewareExecuting, LogLevel.Trace,
		"Idempotency check executing for message {MessageId} on handler {HandlerType}")]
	private partial void LogIdempotencyExecuting(string messageId, string handlerType);

	[LoggerMessage(MiddlewareEventId.DuplicateRequestDetected, LogLevel.Information,
		"Duplicate message {MessageId} skipped for handler {HandlerType}")]
	private partial void LogDuplicateSkipped(string messageId, string handlerType);

	[LoggerMessage(MiddlewareEventId.IdempotencyCheckPassed, LogLevel.Debug,
		"Message {MessageId} processed for handler {HandlerType}")]
	private partial void LogMessageProcessed(string messageId, string handlerType);

	[LoggerMessage(MiddlewareEventId.IdempotencyKeyGenerated, LogLevel.Debug,
		"No message ID available for idempotency check on handler {HandlerType}")]
	private partial void LogNoMessageId(string handlerType);

	[LoggerMessage(MiddlewareEventId.IdempotencyInMemoryFallback, LogLevel.Warning,
		"[Idempotent] handlers are using in-memory deduplication because no IInboxStore is registered. " +
		"Deduplication state will be lost on restart, risking duplicate processing in production. " +
		"Register a persistent IInboxStore via AddSqlServerInboxStore(), AddPostgresInboxStore(), etc.")]
	private partial void LogInMemoryFallbackWarning();

	private void WarnInMemoryFallback()
	{
		if (!_inMemoryFallbackWarned)
		{
			_inMemoryFallbackWarned = true;
			LogInMemoryFallbackWarning();
		}
	}

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
