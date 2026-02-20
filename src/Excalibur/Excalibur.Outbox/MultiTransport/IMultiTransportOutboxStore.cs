// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Outbox.MultiTransport;

/// <summary>
/// Extends <see cref="IOutboxStore"/> with multi-transport routing capabilities.
/// </summary>
/// <remarks>
/// <para>
/// Enables outbox messages to be routed to specific transports (e.g., Kafka, RabbitMQ, Azure Service Bus)
/// based on configurable bindings. This supports scenarios where different message types
/// need to be delivered through different messaging infrastructure.
/// </para>
/// <para>
/// Follows the Microsoft pattern of minimal interfaces (3 methods) with focused responsibility.
/// Reference: Azure.Messaging.ServiceBus ServiceBusSender pattern.
/// </para>
/// </remarks>
public interface IMultiTransportOutboxStore : IOutboxStore
{
	/// <summary>
	/// Stages a message for delivery to a specific named transport.
	/// </summary>
	/// <param name="transportName"> The name of the transport to deliver the message through. </param>
	/// <param name="message"> The outbound message to stage. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous stage operation. </returns>
	/// <exception cref="ArgumentException"> Thrown when transportName is null or empty. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when message is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the transport name is not registered. </exception>
	ValueTask PublishToTransportAsync(
		string transportName,
		OutboundMessage message,
		CancellationToken cancellationToken);

	/// <summary>
	/// Stages a message for delivery to multiple named transports simultaneously.
	/// </summary>
	/// <param name="transportNames"> The names of the transports to deliver the message through. </param>
	/// <param name="message"> The outbound message to stage. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous stage operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when transportNames or message is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when any transport name is not registered. </exception>
	ValueTask PublishToTransportsAsync(
		IReadOnlyList<string> transportNames,
		OutboundMessage message,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the list of registered transport names.
	/// </summary>
	/// <returns> The registered transport names. </returns>
	IReadOnlyList<string> GetRegisteredTransports();
}
