// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Service Bus implementation of <see cref="ITransportSubscriber"/>.
/// Uses <see cref="ServiceBusProcessor"/> for native push-based message delivery.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ServiceBusProcessor"/> provides a push-based consumption model where Azure Service Bus
/// delivers messages to registered handlers. This maps directly to the <see cref="ITransportSubscriber"/>
/// pattern — the handler callback is invoked for each received message.
/// </para>
/// <para>
/// Message settlement is determined by the <see cref="MessageAction"/> returned from the handler:
/// <list type="bullet">
/// <item><see cref="MessageAction.Acknowledge"/> completes the message.</item>
/// <item><see cref="MessageAction.Reject"/> dead-letters the message.</item>
/// <item><see cref="MessageAction.Requeue"/> abandons the message for redelivery.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class ServiceBusTransportSubscriber : ITransportSubscriber
{
	private readonly ServiceBusProcessor _processor;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusTransportSubscriber"/> class.
	/// </summary>
	/// <param name="processor">The Azure Service Bus processor.</param>
	/// <param name="source">The source queue or subscription name.</param>
	/// <param name="logger">The logger instance.</param>
	public ServiceBusTransportSubscriber(
		ServiceBusProcessor processor,
		string source,
		ILogger<ServiceBusTransportSubscriber> logger)
	{
		_processor = processor ?? throw new ArgumentNullException(nameof(processor));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		_processor.ProcessMessageAsync += async args =>
		{
			var received = ConvertToReceivedMessage(args.Message);
			LogMessageReceived(received.Id, Source);

			try
			{
				var action = await handler(received, args.CancellationToken).ConfigureAwait(false);

				switch (action)
				{
					case MessageAction.Acknowledge:
						await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
						LogMessageAcknowledged(received.Id, Source);
						break;

					case MessageAction.Reject:
						await args.DeadLetterMessageAsync(args.Message, "Rejected by handler", cancellationToken: args.CancellationToken)
							.ConfigureAwait(false);
						LogMessageRejected(received.Id, Source);
						break;

					case MessageAction.Requeue:
						await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken)
							.ConfigureAwait(false);
						LogMessageRequeued(received.Id, Source);
						break;
				}
			}
			catch (Exception ex)
			{
				LogError(received.Id, Source, ex);
				// Abandon so the message becomes visible again for retry
				await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken)
					.ConfigureAwait(false);
			}
		};

		_processor.ProcessErrorAsync += args =>
		{
			LogError(args.Identifier, Source, args.Exception);
			return Task.CompletedTask;
		};

		await _processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
		LogSubscriptionStarted(Source);

		try
		{
			// Block until cancellation is requested
			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected on cancellation — fall through to stop
		}
		finally
		{
			await _processor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
			LogSubscriptionStopped(Source);
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(ServiceBusProcessor))
		{
			return _processor;
		}

		return null;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await _processor.DisposeAsync().ConfigureAwait(false);
		LogDisposed(Source);
		GC.SuppressFinalize(this);
	}

	private TransportReceivedMessage ConvertToReceivedMessage(ServiceBusReceivedMessage sbMessage)
	{
		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var prop in sbMessage.ApplicationProperties)
		{
			properties[prop.Key] = prop.Value;
		}

		var messageType = properties.TryGetValue("message-type", out var mt) ? mt as string : null;

		return new TransportReceivedMessage
		{
			Id = sbMessage.MessageId,
			Body = sbMessage.Body.ToMemory(),
			ContentType = sbMessage.ContentType,
			MessageType = messageType,
			CorrelationId = sbMessage.CorrelationId,
			Subject = sbMessage.Subject,
			DeliveryCount = sbMessage.DeliveryCount,
			EnqueuedAt = sbMessage.EnqueuedTime,
			Source = Source,
			PartitionKey = sbMessage.PartitionKey,
			MessageGroupId = sbMessage.SessionId,
			LockExpiresAt = sbMessage.LockedUntil,
			Properties = properties,
			ProviderData = new Dictionary<string, object>
			{
				["azure.lock_token"] = sbMessage.LockToken,
				["azure.sequence_number"] = sbMessage.SequenceNumber,
			},
		};
	}

	[LoggerMessage(AzureServiceBusEventId.TransportSubscriberStarted, LogLevel.Information,
		"Service Bus transport subscriber: subscription started for {Source}")]
	private partial void LogSubscriptionStarted(string source);

	[LoggerMessage(AzureServiceBusEventId.TransportSubscriberMessageReceived, LogLevel.Debug,
		"Service Bus transport subscriber: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(AzureServiceBusEventId.TransportSubscriberMessageAcknowledged, LogLevel.Debug,
		"Service Bus transport subscriber: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(AzureServiceBusEventId.TransportSubscriberMessageRejected, LogLevel.Warning,
		"Service Bus transport subscriber: message {MessageId} rejected (dead-lettered) from {Source}")]
	private partial void LogMessageRejected(string messageId, string source);

	[LoggerMessage(AzureServiceBusEventId.TransportSubscriberMessageRequeued, LogLevel.Debug,
		"Service Bus transport subscriber: message {MessageId} requeued (abandoned) from {Source}")]
	private partial void LogMessageRequeued(string messageId, string source);

	[LoggerMessage(AzureServiceBusEventId.TransportSubscriberError, LogLevel.Error,
		"Service Bus transport subscriber: error processing message {MessageId} from {Source}")]
	private partial void LogError(string messageId, string source, Exception exception);

	[LoggerMessage(AzureServiceBusEventId.TransportSubscriberStopped, LogLevel.Information,
		"Service Bus transport subscriber: subscription stopped for {Source}")]
	private partial void LogSubscriptionStopped(string source);

	[LoggerMessage(AzureServiceBusEventId.TransportSubscriberDisposed, LogLevel.Debug,
		"Service Bus transport subscriber disposed for {Source}")]
	private partial void LogDisposed(string source);
}
