// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a low-level handler that returns <see cref="IMessageResult"/> directly.
/// </summary>
/// <typeparam name="TMessage"> The type of message this handler processes. </typeparam>
/// <remarks>
/// <para>
/// <b>For most handlers, use the specialized interfaces instead:</b>
/// <see cref="Delivery.IActionHandler{TAction}"/>,
/// <see cref="Delivery.IActionHandler{TAction, TResult}"/>,
/// <see cref="Delivery.IEventHandler{TEvent}"/>, or
/// <see cref="Delivery.IDocumentHandler{TDocument}"/>.
/// These return your business types directly, and the framework wraps results automatically.
/// </para>
/// <para>
/// Use <see cref="IDispatchHandler{TMessage}"/> only when you need:
/// </para>
/// <list type="bullet">
/// <item><description>Return <c>MessageResult.SuccessFromCache()</c> with <c>CacheHit = true</c></description></item>
/// <item><description>Set <c>ValidationResult</c> or <c>AuthorizationResult</c> on success results</description></item>
/// <item><description>Return failure without throwing an exception</description></item>
/// <item><description>Access <see cref="IMessageContext"/> within the handler</description></item>
/// <item><description>Implement <see cref="IBatchableHandler{TMessage}"/> for batch processing</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // For most handlers, prefer IActionHandler:
/// public class GetOrderHandler : IActionHandler&lt;GetOrderAction, Order&gt;
/// {
///     public Task&lt;Order&gt; HandleAsync(GetOrderAction action, CancellationToken ct)
///         =&gt; _repository.GetByIdAsync(action.OrderId, ct);
/// }
///
/// // Use IDispatchHandler only for advanced scenarios:
/// public class CachedOrderHandler : IDispatchHandler&lt;GetOrderAction&gt;
/// {
///     public async Task&lt;IMessageResult&gt; HandleAsync(
///         GetOrderAction action, IMessageContext context, CancellationToken ct)
///     {
///         if (_cache.TryGet(action.OrderId, out var order))
///             return MessageResult.SuccessFromCache(order);
///         // ...
///     }
/// }
/// </code>
/// </example>
public interface IDispatchHandler<in TMessage>
	where TMessage : IDispatchMessage
{
	/// <summary>
	/// Handles the specified message.
	/// </summary>
	/// <param name="message"> The message to handle. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The result of message processing. </returns>
	Task<IMessageResult> HandleAsync(
		TMessage message,
		IMessageContext context,
		CancellationToken cancellationToken);
}
