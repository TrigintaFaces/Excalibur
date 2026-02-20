// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Interface for message bus adapters that provide transport-specific message delivery capabilities.
/// </summary>
/// <remarks>
/// Message bus adapters serve as the bridge between the Dispatch messaging framework and specific transport technologies (RabbitMQ, Azure
/// Service Bus, Apache Kafka, etc.). They handle the low-level details of message serialization, routing, and delivery while providing a
/// consistent abstraction for the messaging pipeline.
/// </remarks>
public interface IMessageBusAdapter : IDisposable
{
	/// <summary>
	/// Gets the name of this message bus adapter.
	/// </summary>
	/// <value> A descriptive name identifying the transport type (e.g., "RabbitMQ", "Azure Service Bus"). </value>
	string Name { get; }

	/// <summary>
	/// Gets a value indicating whether this adapter supports message publishing.
	/// </summary>
	/// <value> True if the adapter can publish messages; otherwise, false. </value>
	bool SupportsPublishing { get; }

	/// <summary>
	/// Gets a value indicating whether this adapter supports message subscription and consumption.
	/// </summary>
	/// <value> True if the adapter can subscribe to and consume messages; otherwise, false. </value>
	bool SupportsSubscription { get; }

	/// <summary>
	/// Gets a value indicating whether this adapter supports transactional message operations.
	/// </summary>
	/// <value> True if the adapter supports transactions; otherwise, false. </value>
	bool SupportsTransactions { get; }

	/// <summary>
	/// Gets a value indicating whether this adapter is currently connected and ready for operations.
	/// </summary>
	/// <value> True if the adapter is connected; otherwise, false. </value>
	bool IsConnected { get; }

	/// <summary>
	/// Initializes the message bus adapter with the specified configuration.
	/// </summary>
	/// <param name="options"> Configuration options for the adapter. </param>
	/// <param name="cancellationToken"> Token to cancel the initialization operation. </param>
	/// <returns> A task representing the initialization operation. </returns>
	Task InitializeAsync(IMessageBusOptions options, CancellationToken cancellationToken);

	/// <summary>
	/// Publishes a message to the specified destination.
	/// </summary>
	/// <param name="message"> The message to publish. </param>
	/// <param name="context"> The message context containing routing and metadata information. </param>
	/// <param name="cancellationToken"> Token to cancel the publishing operation. </param>
	/// <returns> A task representing the publishing operation result. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when the adapter doesn't support publishing. </exception>
	Task<IMessageResult> PublishAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Subscribes to messages of the specified type from the given source.
	/// </summary>
	/// <param name="subscriptionName"> The name identifying this subscription. </param>
	/// <param name="messageHandler"> The handler to invoke for received messages. </param>
	/// <param name="options"> Subscription-specific configuration options. </param>
	/// <param name="cancellationToken"> Token to cancel the subscription operation. </param>
	/// <returns> A task representing the subscription operation. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when the adapter doesn't support subscriptions. </exception>
	Task SubscribeAsync(
		string subscriptionName,
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> messageHandler,
		IMessageBusOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Unsubscribes from messages using the specified subscription name.
	/// </summary>
	/// <param name="subscriptionName"> The subscription name to unsubscribe. </param>
	/// <param name="cancellationToken"> Token to cancel the unsubscribe operation. </param>
	/// <returns> A task representing the unsubscribe operation. </returns>
	Task UnsubscribeAsync(string subscriptionName, CancellationToken cancellationToken);

	/// <summary>
	/// Performs a health check on the message bus adapter.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the health check operation. </param>
	/// <returns> A task containing the health check result. </returns>
	Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Starts the message bus adapter and begins processing operations.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the start operation. </param>
	/// <returns> A task representing the start operation. </returns>
	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops the message bus adapter and ceases all operations.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the stop operation. </param>
	/// <returns> A task representing the stop operation. </returns>
	Task StopAsync(CancellationToken cancellationToken);
}
