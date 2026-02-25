// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Defines the interface for routing messages from transport adapters through the dispatcher.
/// </summary>
public interface ITransportAdapterRouter
{
	/// <summary>
	/// Routes a message received from a transport adapter through the dispatcher pipeline.
	/// </summary>
	/// <param name="message"> The message received from the transport. </param>
	/// <param name="context"> The message context containing transport metadata. </param>
	/// <param name="adapterId"> The identifier of the transport adapter that received the message. </param>
	/// <param name="cancellationToken"> Token to cancel the routing operation. </param>
	/// <returns> A task representing the routing operation result. </returns>
	Task<IMessageResult> RouteAsync(
		IDispatchMessage message,
		IMessageContext context,
		string adapterId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Routes a message batch received from a transport adapter through the dispatcher pipeline.
	/// </summary>
	/// <param name="messages"> The batch of messages received from the transport. </param>
	/// <param name="contexts"> The message contexts for each message in the batch. </param>
	/// <param name="adapterId"> The identifier of the transport adapter that received the messages. </param>
	/// <param name="cancellationToken"> Token to cancel the routing operation. </param>
	/// <returns> A task representing the batch routing operation results. </returns>
	Task<IReadOnlyList<IMessageResult>> RouteBatchAsync(
		IReadOnlyList<IDispatchMessage> messages,
		IReadOnlyList<IMessageContext> contexts,
		string adapterId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Registers a transport adapter with the router for message routing.
	/// </summary>
	/// <param name="adapter"> The transport adapter to register. </param>
	/// <param name="adapterId"> The unique identifier for the adapter. </param>
	/// <param name="cancellationToken"> Token to cancel the registration operation. </param>
	/// <returns> A task representing the registration operation. </returns>
	Task RegisterAdapterAsync(
		IMessageBusAdapter adapter,
		string adapterId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Unregisters a transport adapter from the router.
	/// </summary>
	/// <param name="adapterId"> The identifier of the adapter to unregister. </param>
	/// <param name="cancellationToken"> Token to cancel the unregistration operation. </param>
	/// <returns> A task representing the unregistration operation. </returns>
	Task UnregisterAdapterAsync(
		string adapterId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the health status of all registered transport adapters.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the health check operation. </param>
	/// <returns> A task containing the health check results for all adapters. </returns>
	Task<IDictionary<string, HealthCheckResult>> CheckAdapterHealthAsync(
		CancellationToken cancellationToken);
}
