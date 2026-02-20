// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Adapter interface that routes transport messages through the dispatcher pipeline rather than directly to handlers, ensuring all messages
/// flow through IDispatcher.
/// </summary>
public interface ITransportAdapter
{
	/// <summary>
	/// Gets the unique name of this transport adapter.
	/// </summary>
	/// <value> The adapter identifier. </value>
	string Name { get; }

	/// <summary>
	/// Gets the transport type this adapter handles.
	/// </summary>
	/// <value> The transport type managed by the adapter. </value>
	string TransportType { get; }

	/// <summary>
	/// Gets a value indicating whether the adapter is currently running.
	/// </summary>
	/// <value> <see langword="true" /> if the adapter is running; otherwise, <see langword="false" />. </value>
	bool IsRunning { get; }

	/// <summary>
	/// Receives a message from the transport and routes it through the dispatcher.
	/// </summary>
	/// <param name="transportMessage"> The raw transport message. </param>
	/// <param name="dispatcher"> The dispatcher to route through. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The result of message processing. </returns>
	Task<IMessageResult> ReceiveAsync(
		object transportMessage,
		IDispatcher dispatcher,
		CancellationToken cancellationToken);

	/// <summary>
	/// Sends a message through the transport after processing.
	/// </summary>
	/// <param name="message"> The message to send. </param>
	/// <param name="destination"> The destination endpoint. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Task representing the send operation. </returns>
	Task SendAsync(
		IDispatchMessage message,
		string destination,
		CancellationToken cancellationToken);

	/// <summary>
	/// Starts the transport adapter.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous start operation. </returns>
	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops the transport adapter.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous stop operation. </returns>
	Task StopAsync(CancellationToken cancellationToken);
}
