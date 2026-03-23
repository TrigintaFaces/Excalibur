// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Factory for creating <see cref="MessageContext"/> instances in transport adapters.
/// Centralizes context creation to ensure consistent CorrelationId, TenantId, and
/// other context field propagation across all transport adapter implementations.
/// </summary>
internal static class TransportContextFactory
{
	/// <summary>
	/// Creates a <see cref="MessageContext"/> for a received (incoming) message.
	/// Preserves incoming CorrelationId and TenantId from message metadata if available.
	/// </summary>
	/// <param name="message">The received message.</param>
	/// <param name="serviceProvider">The service provider for dependency resolution.</param>
	/// <param name="messageId">The pre-generated message ID (used by adapter for logging).</param>
	/// <returns>A properly initialized <see cref="MessageContext"/>.</returns>
	public static MessageContext CreateForReceive(
		IDispatchMessage message,
		IServiceProvider serviceProvider,
		string messageId)
	{
		var context = new MessageContext(message, serviceProvider)
		{
			MessageId = messageId,
			CorrelationId = ResolveCorrelationId(message, messageId),
		};

		ResolveTenantId(message, context);
		context.SetMessageType(message.GetType().FullName);
		context.SetReceivedTimestampUtc(DateTimeOffset.UtcNow);

		return context;
	}

	/// <summary>
	/// Creates a <see cref="MessageContext"/> for a sent (outgoing) message.
	/// Preserves incoming CorrelationId from message metadata if available,
	/// rather than generating a new GUID.
	/// </summary>
	/// <param name="message">The message being sent.</param>
	/// <param name="serviceProvider">The service provider for dependency resolution.</param>
	/// <param name="messageId">The pre-generated message ID (used by adapter for logging).</param>
	/// <returns>A properly initialized <see cref="MessageContext"/>.</returns>
	public static MessageContext CreateForSend(
		IDispatchMessage message,
		IServiceProvider serviceProvider,
		string messageId)
	{
		var context = new MessageContext(message, serviceProvider)
		{
			MessageId = messageId,
			CorrelationId = ResolveCorrelationId(message, messageId),
		};

		ResolveTenantId(message, context);

		return context;
	}

	/// <summary>
	/// Extracts the CorrelationId from a message's metadata if available,
	/// falling back to the provided messageId for root messages.
	/// </summary>
	private static string ResolveCorrelationId(IDispatchMessage message, string fallbackMessageId)
	{
		// Check if the message is a domain event with metadata carrying CorrelationId
		if (message is IDomainEvent domainEvent &&
			domainEvent.Metadata is { } metadata &&
			metadata.TryGetValue("CorrelationId", out var corrIdObj) &&
			corrIdObj is string corrId &&
			!string.IsNullOrEmpty(corrId))
		{
			return corrId;
		}

		// Root message -- start new correlation chain using the messageId
		return fallbackMessageId;
	}

	/// <summary>
	/// Extracts the TenantId from a message's metadata and applies it to the context.
	/// </summary>
	private static void ResolveTenantId(IDispatchMessage message, MessageContext context)
	{
		if (message is IDomainEvent domainEvent &&
			domainEvent.Metadata is { } metadata &&
			metadata.TryGetValue("TenantId", out var tenantIdObj) &&
			tenantIdObj is string tenantId &&
			!string.IsNullOrEmpty(tenantId))
		{
			context.GetOrCreateIdentityFeature().TenantId = tenantId;
		}
	}
}
