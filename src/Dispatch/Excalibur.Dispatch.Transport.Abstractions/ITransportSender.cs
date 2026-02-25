// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines the minimal interface for sending messages to a transport destination.
/// Replaces the bloated <c>ICloudMessagePublisher</c> (10 methods) with a focused contract (3 methods + GetService).
/// </summary>
/// <remarks>
/// Follows the Microsoft.Extensions.AI <c>IChatClient</c> pattern:
/// <list type="bullet">
/// <item>Minimal surface area — only core send operations.</item>
/// <item>Transport-specific features via <see cref="TransportMessage.Properties"/> with well-known keys.</item>
/// <item>Direct SDK access via <see cref="GetService"/> for advanced scenarios.</item>
/// <item>Cross-cutting concerns (telemetry, ordering, dedup) via <see cref="DelegatingTransportSender"/> decorators.</item>
/// </list>
/// </remarks>
public interface ITransportSender : IAsyncDisposable
{
	/// <summary>
	/// Gets the destination name (queue or topic) this sender is configured for.
	/// </summary>
	/// <value>The destination name.</value>
	string Destination { get; }

	/// <summary>
	/// Sends a single message to the destination.
	/// </summary>
	/// <param name="message">The message to send.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The result of the send operation.</returns>
	Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Sends multiple messages as a batch to the destination.
	/// </summary>
	/// <param name="messages">The messages to send.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The results of the batch send operation.</returns>
	Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken);

	/// <summary>
	/// Flushes any pending messages in internal buffers.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>Task representing the flush operation.</returns>
	Task FlushAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the underlying transport service (e.g., <c>IProducer</c>, <c>ServiceBusSender</c>, <c>IAmazonSQS</c>).
	/// </summary>
	/// <remarks>
	/// Follows the <c>IChatClient.GetService()</c> pattern from Microsoft.Extensions.AI.
	/// Returns <see langword="null"/> if the requested service type is not available.
	/// </remarks>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not available.</returns>
	object? GetService(Type serviceType) => null;
}
