// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines the minimal interface for receiving messages from a transport source.
/// Replaces the bloated <c>ICloudMessageConsumer</c> (11 methods) with a focused contract (3 methods + GetService).
/// </summary>
/// <remarks>
/// Follows the Microsoft.Extensions.AI <c>IChatClient</c> pattern:
/// <list type="bullet">
/// <item>Minimal surface area — only core receive/ack/reject operations.</item>
/// <item>Advanced features (visibility timeout, start/stop, streaming) via <see cref="GetService"/>.</item>
/// <item>Cross-cutting concerns (telemetry, DLQ routing) via <see cref="DelegatingTransportReceiver"/> decorators.</item>
/// </list>
/// </remarks>
public interface ITransportReceiver : IAsyncDisposable
{
	/// <summary>
	/// Gets the source name (queue or subscription) this receiver is configured for.
	/// </summary>
	/// <value>The source name.</value>
	string Source { get; }

	/// <summary>
	/// Receives messages from the source.
	/// </summary>
	/// <param name="maxMessages">Maximum number of messages to receive.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The received messages.</returns>
	Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken);

	/// <summary>
	/// Acknowledges successful processing of a message, so the broker stops tracking it.
	/// </summary>
	/// <remarks>
	/// Returning normally means the broker accepted the acknowledgement. An implementation that cannot
	/// complete the settlement throws rather than returning, because a caller cannot distinguish a
	/// settled message from an unsettled one by any other means, and the redelivery that follows an
	/// unreported failure reaches the consumer as an unexplained duplicate.
	/// </remarks>
	/// <param name="message">The message to acknowledge.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>Task representing the acknowledgment operation.</returns>
	/// <exception cref="TransportSettlementException">
	/// The broker did not accept the acknowledgement, so the message remains outstanding and is likely
	/// to be delivered again. Read <see cref="TransportSettlementException.RedeliveryExpectation"/> to
	/// see whether the transport could tell. Clients of this interface must be idempotent.
	/// </exception>
	Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Rejects a message and optionally requeues it for retry.
	/// </summary>
	/// <remarks>
	/// <paramref name="requeue"/> states the outcome the caller requires: <see langword="true"/> asks
	/// the broker to deliver the message again, <see langword="false"/> asks it not to. Not every broker
	/// can suppress redelivery for a single message, and an implementation that cannot deliver the
	/// requested outcome must not report success for it.
	/// </remarks>
	/// <param name="message">The message to reject.</param>
	/// <param name="reason">The reason for rejection.</param>
	/// <param name="requeue">Whether to requeue the message for retry.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>Task representing the reject operation.</returns>
	/// <exception cref="TransportSettlementException">
	/// The broker did not accept the rejection. Read
	/// <see cref="TransportSettlementException.RedeliveryExpectation"/> to see whether the message is
	/// expected to arrive again: a failed <c>requeue: false</c> leaves a poison message queued, which a
	/// caller must handle rather than reject in a loop.
	/// </exception>
	Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the underlying transport service (e.g., <c>IConsumer</c>, <c>ServiceBusReceiver</c>, <c>IAmazonSQS</c>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Follows the <c>IChatClient.GetService()</c> pattern from Microsoft.Extensions.AI.
	/// Returns <see langword="null"/> if the requested service type is not available.
	/// </para>
	/// <para>
	/// The default implementation answers for any service this instance itself implements, so a transport
	/// that provides one directly need not override it. A transport overrides it to hand out its native
	/// SDK handle, and a decorator overrides it to answer for itself before deferring to the transport it
	/// wraps.
	/// </para>
	/// </remarks>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not available.</returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}
}
