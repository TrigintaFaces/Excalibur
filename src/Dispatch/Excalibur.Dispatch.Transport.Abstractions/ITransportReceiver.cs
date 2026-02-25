// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	/// Acknowledges successful processing of a message.
	/// </summary>
	/// <param name="message">The message to acknowledge.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>Task representing the acknowledgment operation.</returns>
	Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Rejects a message and optionally requeues it for retry.
	/// </summary>
	/// <param name="message">The message to reject.</param>
	/// <param name="reason">The reason for rejection.</param>
	/// <param name="requeue">Whether to requeue the message for retry.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>Task representing the reject operation.</returns>
	Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the underlying transport service (e.g., <c>IConsumer</c>, <c>ServiceBusReceiver</c>, <c>IAmazonSQS</c>).
	/// </summary>
	/// <remarks>
	/// Follows the <c>IChatClient.GetService()</c> pattern from Microsoft.Extensions.AI.
	/// Returns <see langword="null"/> if the requested service type is not available.
	/// </remarks>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not available.</returns>
	object? GetService(Type serviceType) => null;
}
