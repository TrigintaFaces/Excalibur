// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.AzureServiceBus;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Service Bus implementation of <see cref="ITransportReceiver"/>.
/// Uses <see cref="ServiceBusReceiver"/> for native message consumption.
/// </summary>
/// <remarks>
/// Caches <see cref="ServiceBusReceivedMessage"/> instances for acknowledgment/rejection.
/// The native message is stored via <c>"azure.lock_token"</c> in <see cref="TransportReceivedMessage.ProviderData"/>.
/// Outstanding native receipts retain their message bodies and metadata until claimed for settlement
/// or receiver disposal. The 10,000-receipt diagnostic threshold does not impose a hard memory or
/// admission bound; callers must limit outstanding work. A received receipt is not discarded merely
/// because this threshold was crossed.
/// </remarks>
internal sealed partial class ServiceBusTransportReceiver : ITransportReceiver
{
	/// <summary>
	/// The default maximum inbound-payload length (256 KiB), the Azure Service Bus standard-tier
	/// message ceiling, applied when a consumer does not configure one.
	/// </summary>
	private const int DefaultMaxPayloadBytes = 256 * 1024;

	private readonly IServiceBusReceiverSeam _receiver;
	private readonly ILogger _logger;
	private readonly int? _maxPayloadBytes;
	private const int OutstandingReceiptWarningThreshold = 10_000;
	private readonly ConcurrentDictionary<string, ServiceBusReceivedMessage> _messageCache = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusTransportReceiver"/> class.
	/// Wraps the SDK receiver in a <see cref="ServiceBusReceiverAdapter"/>.
	/// </summary>
	/// <param name="receiver">The Azure Service Bus receiver.</param>
	/// <param name="source">The source queue or subscription name.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="maxPayloadBytes">
	/// The maximum inbound-payload length in bytes enforced before body materialization, or
	/// <see langword="null"/> to opt out (unbounded). Defaults to 256 KiB.
	/// </param>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Adapter is stored in _receiver field and disposed via DisposeAsync.")]
	public ServiceBusTransportReceiver(
		ServiceBusReceiver receiver,
		string source,
		ILogger<ServiceBusTransportReceiver> logger,
		int? maxPayloadBytes = DefaultMaxPayloadBytes)
		: this(CreateAdapter(receiver), source, logger, maxPayloadBytes)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusTransportReceiver"/>
	/// class using a pre-built adapter. Used by tests to substitute the SDK via
	/// the <see cref="IServiceBusReceiverSeam"/> seam.
	/// </summary>
	/// <param name="receiver">The Service Bus receiver adapter.</param>
	/// <param name="source">The source queue or subscription name.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="maxPayloadBytes">
	/// The maximum inbound-payload length in bytes enforced before body materialization, or
	/// <see langword="null"/> to opt out (unbounded). Defaults to 256 KiB.
	/// </param>
	internal ServiceBusTransportReceiver(
		IServiceBusReceiverSeam receiver,
		string source,
		ILogger<ServiceBusTransportReceiver> logger,
		int? maxPayloadBytes = DefaultMaxPayloadBytes)
	{
		_receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxPayloadBytes = maxPayloadBytes;
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		try
		{
			var messages = await _receiver.ReceiveMessagesAsync(maxMessages, cancellationToken)
				.ConfigureAwait(false);

			var result = new List<TransportReceivedMessage>(messages.Count);
			foreach (var sbMessage in messages)
			{
				TransportReceivedMessage received;
				try
				{
					received = ConvertToReceivedMessage(sbMessage);
				}
				catch (PayloadTooLargeException ex)
				{
					// Poison-message guard: an oversized payload can never be processed and, left
					// unsettled, would be redelivered until its max delivery count — a poison loop that
					// stalls the entity. Dead-letter the single offending message (no requeue) before
					// deserializing it, then continue the batch rather than aborting on the throw.
					LogPayloadTooLargeRejected(Source, sbMessage.Body.ToMemory().Length, ex);
					using var dlqCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
					await _receiver.DeadLetterMessageAsync(
							sbMessage,
							deadLetterReason: "PayloadTooLarge",
							deadLetterErrorDescription: ex.Message,
							dlqCts.Token)
						.ConfigureAwait(false);
					continue;
				}

				// Retain the native settlement receipt before returning its envelope to the caller.
				_ = _messageCache.TryAdd(sbMessage.LockToken, sbMessage);
				if (_messageCache.Count > OutstandingReceiptWarningThreshold)
				{
					LogOutstandingReceiptWarning(Source, OutstandingReceiptWarningThreshold);
				}

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

		try
		{
			var lockToken = GetLockToken(message);
			if (_messageCache.TryRemove(lockToken, out var sbMessage))
			{
				// Ack must complete even during shutdown to prevent redelivery;
				// use dedicated timeout instead of caller's cancellation token
				using var ackCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _receiver.CompleteMessageAsync(sbMessage, ackCts.Token)
					.ConfigureAwait(false);
				LogMessageAcknowledged(message.Id, Source);
			}
			else
			{
				throw new InvalidOperationException(
					$"Message with lock token '{lockToken}' not found in cache. It may have already been processed or the lock expired.");
			}
		}
		catch (TransportSettlementException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogAcknowledgeError(message.Id, Source, ex);
			throw CreateSettlementException("acknowledge", message, ex);
		}
	}

	/// <inheritdoc />
	public async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		try
		{
			var lockToken = GetLockToken(message);
			if (_messageCache.TryRemove(lockToken, out var sbMessage))
			{
				// Reject must complete even during shutdown to prevent redelivery;
				// use dedicated timeout instead of caller's cancellation token
				using var rejectCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				if (requeue)
				{
					await _receiver.AbandonMessageAsync(sbMessage, rejectCts.Token)
						.ConfigureAwait(false);
					LogMessageRejectedRequeue(message.Id, Source, reason ?? "no reason");
				}
				else
				{
					await _receiver.DeadLetterMessageAsync(
							sbMessage,
							deadLetterReason: "Rejected",
							deadLetterErrorDescription: reason,
							rejectCts.Token)
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
		catch (TransportSettlementException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogRejectError(message.Id, Source, ex);
			throw CreateSettlementException("reject", message, ex);
		}
	}

	private static TransportSettlementException CreateSettlementException(string operation, TransportReceivedMessage message, Exception cause) =>
		new($"Failed to {operation} Azure Service Bus message '{message.Id}'.", cause)
		{
			TransportName = "AzureServiceBus",
			RedeliveryExpectation = TransportRedeliveryExpectation.Unspecified,
			// Settlement claims remove the receipt before native I/O; this receipt cannot be retried.
			Retryability = SettlementRetryability.Permanent,
		};

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(IServiceBusReceiverSeam))
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

	private static IServiceBusReceiverSeam CreateAdapter(ServiceBusReceiver receiver)
	{
		ArgumentNullException.ThrowIfNull(receiver);
		return new ServiceBusReceiverAdapter(receiver);
	}

	private TransportReceivedMessage ConvertToReceivedMessage(ServiceBusReceivedMessage sbMessage)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE materializing the body
		// (sbMessage.Body.ToMemory() below). Fail-closed — throws PayloadTooLargeException, which the
		// receive loop catches to dead-letter the poison message (no requeue); never truncated/dropped.
		PayloadSizeGuard.EnsureWithinLimit(sbMessage.Body.ToMemory().Length, _maxPayloadBytes);

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
				[TransportOrderingMetadata.AzureServiceBusSequenceKey] = sbMessage.SequenceNumber,
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

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverCacheFull, LogLevel.Warning,
		"Service Bus transport receiver: outstanding receipt count exceeds the diagnostic threshold ({Threshold}) for {Source}. " +
		"Native receipts remain available for settlement and retain message bodies and metadata. " +
		"Limit outstanding work to control retained memory.")]
	private partial void LogOutstandingReceiptWarning(string source, int threshold);

	[LoggerMessage(AzureServiceBusEventId.TransportReceiverPayloadTooLarge, LogLevel.Warning,
		"Service Bus transport receiver: rejected oversized payload ({PayloadBytes} bytes) from {Source}, dead-lettered before materialization.")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
