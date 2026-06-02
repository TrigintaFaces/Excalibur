// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Defines a strongly-typed handler for saga timeout messages. Sagas implement this
/// interface to declare which timeout types they handle, enabling the framework to
/// route delivered timeouts directly to the correct handler method.
/// </summary>
/// <typeparam name="TMessage">
/// The timeout message type. This should match the type parameter used when scheduling
/// the timeout via <c>RequestTimeoutAsync&lt;TMessage&gt;(delay, cancellationToken)</c>.
/// </typeparam>
/// <remarks>
/// <para>
/// This interface follows the NServiceBus <c>IHandleTimeouts&lt;T&gt;</c> pattern, providing
/// a clean separation between normal event handling (<c>HandleAsync</c>) and timeout
/// handling. A saga can implement multiple <c>ISagaTimeout&lt;T&gt;</c> interfaces for
/// different timeout message types.
/// </para>
/// <para>
/// When a timeout is delivered and the saga implements <c>ISagaTimeout&lt;T&gt;</c> for
/// the timeout's message type, the framework calls <see cref="HandleTimeoutAsync"/> instead
/// of the general <c>HandleAsync</c> method. If no matching <c>ISagaTimeout&lt;T&gt;</c>
/// is found, the timeout falls through to normal event handling.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// public class OrderSaga : SagaBase&lt;OrderSagaState&gt;,
///     ISagaTimeout&lt;PaymentTimeout&gt;,
///     ISagaTimeout&lt;ShippingTimeout&gt;
/// {
///     public Task HandleTimeoutAsync(PaymentTimeout message, CancellationToken cancellationToken)
///     {
///         // Cancel the order due to payment timeout
///         State.Status = "Cancelled";
///         MarkCompleted();
///         return Task.CompletedTask;
///     }
///
///     public Task HandleTimeoutAsync(ShippingTimeout message, CancellationToken cancellationToken)
///     {
///         // Escalate shipping delay
///         return SendCommandAsync(new EscalateShipping(State.OrderId), cancellationToken);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface ISagaTimeout<in TMessage>
{
	/// <summary>
	/// Handles a timeout message that was previously scheduled via
	/// <c>RequestTimeoutAsync&lt;TMessage&gt;</c>.
	/// </summary>
	/// <param name="message">The timeout message containing any data provided at scheduling time.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous timeout handling operation.</returns>
	Task HandleTimeoutAsync(TMessage message, CancellationToken cancellationToken);
}
