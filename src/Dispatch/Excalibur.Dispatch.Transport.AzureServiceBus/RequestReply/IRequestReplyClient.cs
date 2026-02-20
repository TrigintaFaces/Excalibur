// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Client for request/reply messaging patterns over Azure Service Bus.
/// </summary>
/// <remarks>
/// <para>
/// Implements the request/reply pattern using Azure Service Bus sessions for correlation.
/// Each request is sent with a unique session ID, and the reply is received on a session-enabled
/// queue or subscription filtered by that session ID.
/// </para>
/// <para>
/// This pattern is useful for synchronous-style interactions over an asynchronous messaging
/// infrastructure, such as querying remote services or awaiting confirmation of an operation.
/// </para>
/// <para>
/// The correlation workflow:
/// <list type="number">
///   <item><description>Caller sends a request message with a <c>ReplyToSessionId</c>.</description></item>
///   <item><description>Responder processes the request and sends a reply to the reply queue with the matching session ID.</description></item>
///   <item><description>Caller accepts the session and receives the reply.</description></item>
/// </list>
/// </para>
/// <para>
/// This interface follows the Microsoft pattern from <c>Azure.Messaging.ServiceBus</c>,
/// specifically the <c>ServiceBusSender</c> (3 methods) + <c>ServiceBusSessionReceiver</c> approach.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var reply = await requestReplyClient.SendRequestAsync(
///     requestMessage, "orders-queue", cancellationToken);
/// </code>
/// </example>
public interface IRequestReplyClient : IAsyncDisposable
{
	/// <summary>
	/// Sends a request message and awaits a correlated reply.
	/// </summary>
	/// <param name="request">The request message to send.</param>
	/// <param name="destinationEntity">The Service Bus queue or topic to send the request to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the <see cref="RequestReplyMessage"/> reply.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="request"/> or <paramref name="destinationEntity"/> is null.
	/// </exception>
	/// <exception cref="TimeoutException">
	/// Thrown when no reply is received within the configured timeout.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method performs the full request/reply cycle:
	/// <list type="number">
	///   <item><description>Generates a unique session ID for correlation.</description></item>
	///   <item><description>Sends the request with <c>ReplyToSessionId</c> set.</description></item>
	///   <item><description>Accepts a session on the reply queue using the session ID.</description></item>
	///   <item><description>Receives and returns the reply message.</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	Task<RequestReplyMessage> SendRequestAsync(
		RequestReplyMessage request,
		string destinationEntity,
		CancellationToken cancellationToken);

	/// <summary>
	/// Receives a reply message for a previously sent request using the specified session ID.
	/// </summary>
	/// <param name="sessionId">The session ID to correlate the reply with.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the <see cref="RequestReplyMessage"/> reply, or <c>null</c> if no reply
	/// is available before the cancellation token fires.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sessionId"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method when you need to decouple the send and receive steps,
	/// for example when sending a batch of requests and then collecting replies.
	/// </para>
	/// </remarks>
	Task<RequestReplyMessage?> ReceiveReplyAsync(
		string sessionId,
		CancellationToken cancellationToken);
}
