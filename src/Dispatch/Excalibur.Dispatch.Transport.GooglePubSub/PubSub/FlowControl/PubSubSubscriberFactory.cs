// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Factory for creating Google Cloud Pub/Sub subscribers with integrated flow control.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PubSubSubscriberFactory" /> class. </remarks>
/// <param name="flowControlOptions"> The flow control options. </param>
/// <param name="loggerFactory"> The logger factory. </param>
public partial class PubSubSubscriberFactory(
	IOptions<PubSubFlowControlOptions> flowControlOptions,
	ILoggerFactory loggerFactory)
{
	private readonly PubSubFlowControlOptions _flowControlOptions =
		flowControlOptions?.Value ?? throw new ArgumentNullException(nameof(flowControlOptions));

	private readonly ILogger<PubSubSubscriberFactory> _logger = loggerFactory.CreateLogger<PubSubSubscriberFactory>();

	/// <summary>
	/// Creates a new subscriber client with flow control.
	/// </summary>
	/// <param name="subscriptionName"> The subscription name. </param>
	/// <param name="messageHandler"> The message handler. </param>
	/// <param name="errorHandler"> The error handler. </param>
	/// <returns> A configured subscriber client. </returns>
	public async Task<FlowControlledSubscriber> CreateSubscriberAsync(
		SubscriptionName subscriptionName,
		Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> messageHandler,
		Func<PubsubMessage, AckError, Task>? errorHandler = null)
	{
		ArgumentNullException.ThrowIfNull(subscriptionName);
		ArgumentNullException.ThrowIfNull(messageHandler);

		var flowController = new PubSubFlowController(
			_flowControlOptions.MaxOutstandingElementCount,
			_flowControlOptions.MaxOutstandingElementCount);

		var settings = CreateSubscriberSettings(flowController);

		// Create wrapped handlers that integrate with flow control
		var wrappedHandler = CreateFlowControlledMessageHandler(flowController, messageHandler);
		var wrappedErrorHandler = errorHandler != null
			? CreateFlowControlledErrorHandler(flowController, errorHandler)
			: null;

		// Use SubscriberClientBuilder per the new API
		var subscriberBuilder = new SubscriberClientBuilder { SubscriptionName = subscriptionName, Settings = settings };

		var subscriber = await subscriberBuilder.BuildAsync().ConfigureAwait(false);

		LogSubscriberCreated(subscriptionName,
			settings.FlowControlSettings.MaxOutstandingElementCount,
			settings.FlowControlSettings.MaxOutstandingByteCount);

		return new FlowControlledSubscriber(subscriber, flowController, wrappedHandler, wrappedErrorHandler);
	}

	/// <summary>
	/// Creates a new subscriber client with custom settings and flow control.
	/// </summary>
	/// <param name="subscriptionName"> The subscription name. </param>
	/// <param name="clientBuilder"> Custom client builder. </param>
	/// <param name="messageHandler"> The message handler. </param>
	/// <param name="errorHandler"> The error handler. </param>
	/// <returns> A configured subscriber client. </returns>
	public async Task<FlowControlledSubscriber> CreateSubscriberAsync(
		SubscriptionName subscriptionName,
		SubscriberClientBuilder clientBuilder,
		Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> messageHandler,
		Func<PubsubMessage, AckError, Task>? errorHandler = null)
	{
		ArgumentNullException.ThrowIfNull(subscriptionName);
		ArgumentNullException.ThrowIfNull(clientBuilder);
		ArgumentNullException.ThrowIfNull(messageHandler);

		var flowController = new PubSubFlowController(
			_flowControlOptions.MaxOutstandingElementCount,
			_flowControlOptions.MaxOutstandingElementCount);

		var settings = CreateSubscriberSettings(flowController);

		// Configure the client builder with flow control settings
		clientBuilder.Settings = settings;

		var wrappedHandler = CreateFlowControlledMessageHandler(flowController, messageHandler);
		var wrappedErrorHandler = errorHandler != null
			? CreateFlowControlledErrorHandler(flowController, errorHandler)
			: null;

		var subscriber = await clientBuilder.BuildAsync(CancellationToken.None).ConfigureAwait(false);

		return new FlowControlledSubscriber(subscriber, flowController, wrappedHandler, wrappedErrorHandler);
	}

	private static SubscriberClient.Settings CreateSubscriberSettings(PubSubFlowController flowController)
	{
		var settings = new SubscriberClient.Settings
		{
			FlowControlSettings = flowController.CreateSubscriberFlowControlSettings(),

			// Set other recommended settings for high throughput
			MaxTotalAckExtension = TimeSpan.FromHours(1),
			AckDeadline = TimeSpan.FromSeconds(60),

			// Uses the default scheduler internally for better performance
		};

		return settings;
	}

	private static Func<PubsubMessage, AckError, Task>
		CreateFlowControlledErrorHandler(
			PubSubFlowController flowController,
			Func<PubsubMessage, AckError, Task> innerHandler) =>
		async (message, error) =>
		{
			var messageSize = message.CalculateSize();
			var metrics = flowController.GetMetrics();
			metrics.RecordProcessingError(messageSize);

			await innerHandler(message, error).ConfigureAwait(false);
		};

	private Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>
		CreateFlowControlledMessageHandler(
			PubSubFlowController flowController,
			Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> innerHandler) =>
		async (message, cancellationToken) =>
		{
			// Note: The actual flow control is handled by the SubscriberClient itself when configured with FlowControlSettings. This
			// wrapper is for metrics.
			var messageSize = message.CalculateSize();

			try
			{
				var reply = await innerHandler(message, cancellationToken).ConfigureAwait(false);

				// Update metrics based on reply
				var metrics = flowController.GetMetrics();
				if (reply == SubscriberClient.Reply.Ack)
				{
					metrics.RecordMessageProcessed(messageSize);
				}
				else
				{
					metrics.RecordProcessingError(messageSize);
				}

				return reply;
			}
			catch (Exception ex)
			{
				LogMessageProcessingError(message.MessageId, ex);
				var metrics = flowController.GetMetrics();
				metrics.RecordProcessingError(messageSize);
				throw;
			}
		};

	// Source-generated logging methods (Sprint 363 - EventId Migration)
	[LoggerMessage(GooglePubSubEventId.FlowControlledSubscriberCreated, LogLevel.Information,
		"Created flow-controlled subscriber for subscription {Subscription} with MaxElements={MaxElements}, MaxBytes={MaxBytes}")]
	private partial void LogSubscriberCreated(SubscriptionName subscription, long? maxElements, long? maxBytes);

	[LoggerMessage(GooglePubSubEventId.SubscriberMessageProcessingError, LogLevel.Error,
		"Error processing message {MessageId}")]
	private partial void LogMessageProcessingError(string messageId, Exception ex);
}
