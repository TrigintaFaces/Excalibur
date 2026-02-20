// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a contract for publishing messages within a system.
/// </summary>
/// <remarks>
/// This interface provides the core abstraction for message publishing in the Excalibur framework. It should be implemented by
/// transport-specific publishers and used by application code that needs to send messages through the messaging infrastructure.
/// </remarks>
public interface IMessagePublisher
{
	/// <summary>
	/// Publishes a message asynchronously to the appropriate message-handling system.
	/// </summary>
	/// <typeparam name="TMessage"> The type of the message to be published. </typeparam>
	/// <param name="message"> The message to be published. </param>
	/// <param name="context"> The message context containing metadata such as correlation ID, tenant ID, or other relevant information. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous operation of publishing the message. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="message" /> or <paramref name="context" /> is <c> null </c>.
	/// </exception>
	/// <exception cref="OperationCanceledException"> Thrown if the operation is cancelled via <paramref name="cancellationToken" />. </exception>
	Task PublishAsync<TMessage>(TMessage message, IMessageContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Publishes a message asynchronously to the appropriate message-handling system.
	/// </summary>
	/// <typeparam name="TMessage"> The type of the message to be published. </typeparam>
	/// <param name="message"> The message to be published. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous operation of publishing the message. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="message" /> is <c> null </c>. </exception>
	/// <exception cref="OperationCanceledException"> Thrown if the operation is cancelled via <paramref name="cancellationToken" />. </exception>
	/// <remarks>
	/// This overload creates a new message context internally. For better control over message metadata, use the overload that accepts an
	/// explicit <see cref="IMessageContext" />.
	/// </remarks>
	Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken);
}
