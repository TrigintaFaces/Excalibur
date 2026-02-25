// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents a Pub/Sub subscriber with integrated flow control.
/// </summary>
public sealed class FlowControlledSubscriber : IAsyncDisposable
{
	private readonly Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> _messageHandler;
	private readonly Func<PubsubMessage, AckError, Task>? _errorHandler;

	internal FlowControlledSubscriber(
		SubscriberClient subscriberClient,
		PubSubFlowController flowController,
		Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> messageHandler,
		Func<PubsubMessage, AckError, Task>? errorHandler)
	{
		SubscriberClient = subscriberClient;
		FlowController = flowController;
		_messageHandler = messageHandler;
		_errorHandler = errorHandler;
	}

	/// <summary>
	/// Gets the flow controller for monitoring and management.
	/// </summary>
	/// <value>
	/// The flow controller for monitoring and management.
	/// </value>
	public PubSubFlowController FlowController { get; }

	/// <summary>
	/// Gets the underlying subscriber client.
	/// </summary>
	/// <value>
	/// The underlying subscriber client.
	/// </value>
	public SubscriberClient SubscriberClient { get; }

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await SubscriberClient.StopAsync(CancellationToken.None).ConfigureAwait(false);

		if (FlowController is IDisposable disposableFlowController)
		{
			disposableFlowController.Dispose();
		}
	}
}
