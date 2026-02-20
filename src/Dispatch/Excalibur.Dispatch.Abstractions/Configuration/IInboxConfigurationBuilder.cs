// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Fluent builder for configuring selective inbox (idempotency) per handler.
/// </summary>
/// <remarks>
/// <para>
/// This builder provides a fluent API for configuring idempotency behavior for handlers
/// without requiring the <see cref="Inbox.IdempotentAttribute"/>. Configuration is evaluated
/// at application startup and cached for runtime performance.
/// </para>
/// <para>
/// Selection precedence (most to least specific):
/// <list type="number">
/// <item><description><see cref="ForHandler{THandler}"/> - Exact handler type match (HIGHEST)</description></item>
/// <item><description><see cref="ForHandlersMatching"/> - Custom predicate match</description></item>
/// <item><description><see cref="ForMessageType{TMessage}"/> - Message type match</description></item>
/// <item><description><see cref="ForNamespace"/> - Namespace prefix match (LOWEST)</description></item>
/// </list>
/// </para>
/// <para>
/// Settings configured via this builder OVERRIDE any <see cref="Inbox.IdempotentAttribute"/>
/// settings on the handler class.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// services.AddDispatch(options =>
/// {
///     options.ConfigureInbox(inbox =>
///     {
///         inbox.ForHandler&lt;PaymentHandler&gt;()
///             .WithRetention(TimeSpan.FromHours(24));
///
///         inbox.ForNamespace("MyApp.Handlers.Financial",
///             cfg => cfg.WithRetention(TimeSpan.FromDays(7)));
///
///         inbox.ForHandlersMatching(
///             t => t.Name.EndsWith("CriticalHandler"),
///             cfg => cfg.UseInMemory());
///     });
/// });
/// </code>
/// </para>
/// </remarks>
public interface IInboxConfigurationBuilder
{
	/// <summary>
	/// Configures inbox for a specific handler type.
	/// </summary>
	/// <typeparam name="THandler"> The handler type to configure. </typeparam>
	/// <returns> A configuration object for the handler. </returns>
	/// <remarks>
	/// This is the highest priority selection. Configuration set here will override
	/// any other matching rules.
	/// </remarks>
	IInboxHandlerConfiguration ForHandler<THandler>()
		where THandler : class;

	/// <summary>
	/// Configures inbox for handlers matching a predicate.
	/// </summary>
	/// <param name="predicate"> A predicate that determines which handler types match. </param>
	/// <param name="configure"> An action to configure the matching handlers. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// This has higher priority than <see cref="ForNamespace"/> and <see cref="ForMessageType{TMessage}"/>,
	/// but lower priority than <see cref="ForHandler{THandler}"/>.
	/// </remarks>
	IInboxConfigurationBuilder ForHandlersMatching(
		Func<Type, bool> predicate,
		Action<IInboxHandlerConfiguration> configure);

	/// <summary>
	/// Configures inbox for all handlers in a namespace.
	/// </summary>
	/// <param name="namespacePrefix"> The namespace prefix to match. </param>
	/// <param name="configure"> An action to configure the matching handlers. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// This is the lowest priority selection. Use for broad configuration rules
	/// that can be overridden by more specific rules.
	/// </remarks>
	IInboxConfigurationBuilder ForNamespace(
		string namespacePrefix,
		Action<IInboxHandlerConfiguration> configure);

	/// <summary>
	/// Configures inbox for handlers of a specific message type.
	/// </summary>
	/// <typeparam name="TMessage"> The message type to configure handlers for. </typeparam>
	/// <param name="configure"> An action to configure the matching handlers. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// This applies to any handler that processes messages of the specified type.
	/// Has higher priority than <see cref="ForNamespace"/> but lower than
	/// <see cref="ForHandlersMatching"/> and <see cref="ForHandler{THandler}"/>.
	/// </remarks>
	IInboxConfigurationBuilder ForMessageType<TMessage>(
		Action<IInboxHandlerConfiguration> configure)
		where TMessage : IDispatchMessage;
}
