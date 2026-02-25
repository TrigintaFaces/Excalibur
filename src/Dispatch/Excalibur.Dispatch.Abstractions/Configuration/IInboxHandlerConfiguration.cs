// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Configuration for a specific handler's inbox (idempotency) behavior.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides a fluent API for configuring how a handler processes messages
/// idempotently. Settings configured here override any <see cref="IdempotentAttribute"/>
/// settings on the handler class.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// inbox.ForHandler&lt;PaymentHandler&gt;()
///     .WithRetention(TimeSpan.FromHours(24))
///     .UseInMemory()
///     .WithStrategy(MessageIdStrategy.FromCorrelationId);
/// </code>
/// </para>
/// </remarks>
public interface IInboxHandlerConfiguration
{
	/// <summary>
	/// Sets the retention period for processed message IDs.
	/// </summary>
	/// <param name="retention"> The duration to track message IDs. </param>
	/// <returns> The configuration for fluent chaining. </returns>
	/// <remarks>
	/// Messages processed before this period expires will no longer be tracked
	/// and may be reprocessed if received again. Default is 24 hours.
	/// </remarks>
	IInboxHandlerConfiguration WithRetention(TimeSpan retention);

	/// <summary>
	/// Uses in-memory deduplication (fast, non-persistent).
	/// </summary>
	/// <returns> The configuration for fluent chaining. </returns>
	/// <remarks>
	/// In-memory storage is faster but not shared across instances.
	/// Use for serverless scenarios or when persistence is not required.
	/// </remarks>
	IInboxHandlerConfiguration UseInMemory();

	/// <summary>
	/// Uses persistent storage for deduplication via <see cref="IInboxStore"/>.
	/// </summary>
	/// <returns> The configuration for fluent chaining. </returns>
	/// <remarks>
	/// Persistent storage is shared across instances and survives restarts.
	/// Requires an <see cref="IInboxStore"/> implementation to be registered.
	/// </remarks>
	IInboxHandlerConfiguration UsePersistent();

	/// <summary>
	/// Sets the message ID extraction strategy.
	/// </summary>
	/// <param name="strategy"> The strategy for extracting message IDs. </param>
	/// <returns> The configuration for fluent chaining. </returns>
	IInboxHandlerConfiguration WithStrategy(MessageIdStrategy strategy);

	/// <summary>
	/// Sets the header name for message ID extraction when using <see cref="MessageIdStrategy.FromHeader"/>.
	/// </summary>
	/// <param name="headerName"> The name of the header containing the message ID. </param>
	/// <returns> The configuration for fluent chaining. </returns>
	IInboxHandlerConfiguration WithHeaderName(string headerName);

	/// <summary>
	/// Configures a custom message ID provider for <see cref="MessageIdStrategy.Custom"/> strategy.
	/// </summary>
	/// <typeparam name="TProvider"> The type of the message ID provider. </typeparam>
	/// <returns> The configuration for fluent chaining. </returns>
	IInboxHandlerConfiguration WithMessageIdProvider<TProvider>()
		where TProvider : class, IMessageIdProvider;
}
