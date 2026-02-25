// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Service Bus implementation of <see cref="ITransportReceiver"/>.
/// Uses <see cref="ServiceBusReceiver"/> for native message consumption.
/// </summary>
/// <remarks>
/// Caches <see cref="ServiceBusReceivedMessage"/> instances for acknowledgment/rejection.
/// The native message is stored via <c>"azure.lock_token"</c> in <see cref="TransportReceivedMessage.ProviderData"/>.
/// </remarks>
internal sealed partial class ServiceBusTransportReceiver : ITransportReceiver
{
	private readonly ServiceBusReceiver _receiver;
	private readonly ILogger _logger;
	private readonly ConcurrentDictionary<string, ServiceBusReceivedMessage> _messageCache = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusTransportReceiver"/> class.
	/// </summary>
	/// <param name="receiver">The Azure Service Bus receiver.</param>
	/// <param name="source">The source queue or subscription name.</param>
	/// <param name="logger">The logger instance.</param>
	public ServiceBusTransportReceiver(
		ServiceBusReceiver receiver,
		string source,
		ILogger<ServiceBusTransportReceiver> logger)
	{
		_receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		try
		{
			var messages = await _receiver.ReceiveMessagesAsync(maxMessages, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			var result = new List<TransportReceivedMessage>(messages.Count);
			foreach (var sbMessage in messages)
			{
				var received = ConvertToReceivedMessage(sbMessage);
				_ = _messageCache.TryAdd(sbMessage.LockToken, sbMessage);
				result.Add(received);
				LogMessageReceived(received.Id, Source);
			}

			return result;
		}
		catch (Exception ex)
		{
			LogReceiveError(Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var lockToken = GetLockToken(message);

		try
		{
			if (_messageCache.TryRemove(lockToken, out var sbMessage))
			{
				await _receiver.CompleteMessageAsync(sbMessage, cancellationToken).ConfigureAwait(false);
				LogMessageAcknowledged(message.Id, Source);
			}
			else
			{
				throw new InvalidOperationException(
					$"Message with lock token '{lockToken}' not found in cache. It may have already been processed or the lock expired.");
			}
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogAcknowledgeError(message.Id, Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var lockToken = GetLockToken(message);

		try
		{
			if (_messageCache.TryRemove(lockToken, out var sbMessage))
			{
				if (requeue)
				{
					await _receiver.AbandonMessageAsync(sbMessage, cancellationToken: cancellationToken)
						.ConfigureAwait(false);
					LogMessageRejectedRequeue(message.Id, Source, reason ?? "no reason");
				}
				else
				{
					await _receiver.DeadLetterMessageAsync(sbMessage, reason ?? "Rejected", cancellationToken: cancellationToken)
						.ConfigureAwait(false);
					LogMessageRejected(message.Id, Source, reason ?? "no reason");
				}
			}
			else
			{
				throw new InvalidOperationException(
					$"Message with lock token '{lockToken}' not found in cache.");
			}
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogRejectError(message.Id, Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(ServiceBusReceiver))
		{
			return _receiver;
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
		await _receiver.DisposeAsync().ConfigureAwait(false);
		_messageCache.Clear();
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

	private static string GetLockToken(TransportReceivedMessage message)
	{
		if (message.ProviderData.TryGetValue("azure.lock_token", out var token) && token is string tokenStr)
		{
			return tokenStr;
		}

		throw new InvalidOperationException("Message does not contain an Azure Service Bus lock token in ProviderData.");
	}

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverMessageReceived, LogLevel.Debug,
		"Service Bus transport receiver: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverReceiveError, LogLevel.Error,
		"Service Bus transport receiver: failed to receive messages from {Source}")]
	private partial void LogReceiveError(string source, Exception exception);

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverMessageAcknowledged, LogLevel.Debug,
		"Service Bus transport receiver: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverAcknowledgeError, LogLevel.Error,
		"Service Bus transport receiver: failed to acknowledge message {MessageId} from {Source}")]
	private partial void LogAcknowledgeError(string messageId, string source, Exception exception);

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverMessageRejected, LogLevel.Warning,
		"Service Bus transport receiver: message {MessageId} rejected from {Source}: {Reason}")]
	private partial void LogMessageRejected(string messageId, string source, string reason);

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverMessageRejectedRequeue, LogLevel.Debug,
		"Service Bus transport receiver: message {MessageId} abandoned (requeue) from {Source}: {Reason}")]
	private partial void LogMessageRejectedRequeue(string messageId, string source, string reason);

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverRejectError, LogLevel.Error,
		"Service Bus transport receiver: failed to reject message {MessageId} from {Source}")]
	private partial void LogRejectError(string messageId, string source, Exception exception);

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverDisposed, LogLevel.Debug,
		"Service Bus transport receiver disposed for {Source}")]
	private partial void LogDisposed(string source);
}
