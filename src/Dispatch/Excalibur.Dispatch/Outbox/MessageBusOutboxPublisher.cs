// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Globalization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Outbox;

/// <summary>
/// Outbox publisher that stages messages for reliable delivery via a message bus. Supports multi-transport publishing with parallel
/// delivery and per-transport status tracking.
/// </summary>
/// <remarks>
/// This implementation uses the transactional outbox pattern to ensure messages are reliably delivered even in the face of failures.
/// Messages are first staged to the outbox store within the same transaction as the business operation, then published asynchronously by
/// background processors.
/// </remarks>
public sealed partial class MessageBusOutboxPublisher : IOutboxPublisher
{
	private readonly IOutboxStore _outboxStore;
	private readonly IOutboxStoreAdmin? _outboxStoreAdmin;
	private readonly IMultiTransportOutboxStore? _multiTransportStore;
	private readonly IPayloadSerializer _serializer;
	private readonly IMessageBusAdapter? _messageBus;
	private readonly TransportRegistry? _transportRegistry;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<MessageBusOutboxPublisher> _logger;

	private readonly Stopwatch _operationStopwatch = new();
	private long _totalOperations;
	private long _totalPublished;
	private long _totalFailed;
	private DateTimeOffset? _lastOperationAt;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageBusOutboxPublisher" /> class.
	/// </summary>
	/// <param name="outboxStore"> The outbox store for message persistence. </param>
	/// <param name="serializer"> The payload serializer for message body serialization with pluggable format support. </param>
	/// <param name="messageBus"> The message bus adapter for publishing (for single-transport mode). </param>
	/// <param name="serviceProvider"> The service provider for creating message contexts. </param>
	/// <param name="logger"> The logger instance. </param>
	public MessageBusOutboxPublisher(
		IOutboxStore outboxStore,
		IPayloadSerializer serializer,
		IMessageBusAdapter messageBus,
		IServiceProvider serviceProvider,
		ILogger<MessageBusOutboxPublisher> logger)
	{
		_outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
		_outboxStoreAdmin = outboxStore as IOutboxStoreAdmin;
		_multiTransportStore = outboxStore as IMultiTransportOutboxStore;
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageBusOutboxPublisher" /> class with multi-transport support.
	/// </summary>
	/// <param name="outboxStore"> The outbox store for message persistence. </param>
	/// <param name="serializer"> The payload serializer for message body serialization with pluggable format support. </param>
	/// <param name="transportRegistry"> The transport registry for multi-transport publishing. </param>
	/// <param name="serviceProvider"> The service provider for creating message contexts. </param>
	/// <param name="logger"> The logger instance. </param>
	public MessageBusOutboxPublisher(
		IOutboxStore outboxStore,
		IPayloadSerializer serializer,
		TransportRegistry transportRegistry,
		IServiceProvider serviceProvider,
		ILogger<MessageBusOutboxPublisher> logger)
	{
		_outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
		_outboxStoreAdmin = outboxStore as IOutboxStoreAdmin;
		_multiTransportStore = outboxStore as IMultiTransportOutboxStore;
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_transportRegistry = transportRegistry ?? throw new ArgumentNullException(nameof(transportRegistry));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<OutboundMessage> PublishAsync(
		object message,
		string destination,
		DateTimeOffset? scheduledAt,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(destination);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = _serializer.SerializeObject(message, message.GetType());
		var messageType = message.GetType().FullName ?? message.GetType().Name;

		var outboundMessage = new OutboundMessage(messageType, payload, destination) { ScheduledAt = scheduledAt };

		await _outboxStore.StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);

		LogStagedMessage(outboundMessage.Id, messageType, destination);

		return outboundMessage;
	}

	/// <summary>
	/// Publishes a message to multiple transports with per-transport delivery tracking.
	/// </summary>
	/// <param name="message"> The message to publish. </param>
	/// <param name="transports"> The transport delivery configurations. </param>
	/// <param name="scheduledAt"> Optional scheduled delivery time. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The staged outbound message. </returns>
	public async Task<OutboundMessage> PublishMultiTransportAsync(
		object message,
		IEnumerable<OutboundMessageTransport> transports,
		CancellationToken cancellationToken,
		DateTimeOffset? scheduledAt = null)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(transports);

		if (_multiTransportStore is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_MultiTransportRequiresStore);
		}

		var transportList = transports.ToList();
		if (transportList.Count == 0)
		{
			throw new ArgumentException(
				Resources.MessageBusOutboxPublisher_AtLeastOneTransportRequired,
				nameof(transports));
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = _serializer.SerializeObject(message, message.GetType());
		var messageType = message.GetType().FullName ?? message.GetType().Name;

		var outboundMessage = new OutboundMessage(messageType, payload, transportList[0].Destination ?? string.Empty)
		{
			ScheduledAt = scheduledAt,
			IsMultiTransport = true,
			TargetTransports = string.Join(",", transportList.Select(t => t.TransportName))
		};

		// Initialize transport deliveries with the message ID
		foreach (var transport in transportList)
		{
			transport.MessageId = outboundMessage.Id;
			outboundMessage.TransportDeliveries.Add(transport);
		}

		await _multiTransportStore.StageMessageWithTransportsAsync(outboundMessage, transportList, cancellationToken)
			.ConfigureAwait(false);

		LogStagedMultiTransportMessage(
			outboundMessage.Id,
			messageType,
			transportList.Count,
			outboundMessage.TargetTransports);

		return outboundMessage;
	}

	/// <inheritdoc />
	public async Task<PublishingResult> PublishPendingMessagesAsync(CancellationToken cancellationToken)
	{
		var messages = await _outboxStore.GetUnsentMessagesAsync(100, cancellationToken).ConfigureAwait(false);
		return await PublishMessagesAsync(messages.ToList(), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<PublishingResult> PublishScheduledMessagesAsync(CancellationToken cancellationToken)
	{
		if (_outboxStoreAdmin is null)
		{
			return PublishingResult.Success(0, 0, TimeSpan.Zero);
		}

		var messages = await _outboxStoreAdmin.GetScheduledMessagesAsync(DateTimeOffset.UtcNow, 100, cancellationToken).ConfigureAwait(false);
		return await PublishMessagesAsync(messages.ToList(), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<PublishingResult> RetryFailedMessagesAsync(
		int maxRetries,
		CancellationToken cancellationToken)
	{
		if (maxRetries < 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maxRetries),
				Resources.MessageBusOutboxPublisher_MaxRetriesNonNegative);
		}

		if (_outboxStoreAdmin is null)
		{
			return PublishingResult.Success(0, 0, TimeSpan.Zero);
		}

		var messages = await _outboxStoreAdmin.GetFailedMessagesAsync(maxRetries, null, 100, cancellationToken).ConfigureAwait(false);
		return await PublishMessagesAsync(messages.ToList(), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public PublisherStatistics GetStatistics()
	{
		var total = _totalPublished + _totalFailed;
		var successRate = total > 0 ? (_totalPublished * 100.0 / total) : 100.0;

		return new PublisherStatistics
		{
			TotalOperations = _totalOperations,
			TotalMessagesPublished = _totalPublished,
			TotalMessagesFailed = _totalFailed,
			CurrentSuccessRate = successRate,
			LastOperationAt = _lastOperationAt,
			CapturedAt = DateTimeOffset.UtcNow
		};
	}

	/// <summary>
	/// Publishes pending deliveries for a specific transport in parallel.
	/// </summary>
	/// <param name="transportName"> The transport name to process. </param>
	/// <param name="batchSize"> Maximum number of deliveries to process. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The publishing result. </returns>
	public async Task<PublishingResult> PublishPendingTransportDeliveriesAsync(
		string transportName,
		CancellationToken cancellationToken,
		int batchSize = 100)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		if (_multiTransportStore is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_PerTransportRequiresStore);
		}

		if (_transportRegistry is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_PerTransportRequiresRegistry);
		}

		var adapter = _transportRegistry.GetTransportAdapter(transportName)
					  ?? throw new InvalidOperationException(
						  string.Format(
							  CultureInfo.CurrentCulture,
							  Resources.MessageBusOutboxPublisher_NoTransportAdapter,
							  transportName));

		_operationStopwatch.Restart();
		_totalOperations++;
		_lastOperationAt = DateTimeOffset.UtcNow;

		var deliveries = await _multiTransportStore.GetPendingTransportDeliveriesAsync(transportName, batchSize, cancellationToken)
			.ConfigureAwait(false);

		var deliveryList = deliveries.ToList();
		if (deliveryList.Count == 0)
		{
			_operationStopwatch.Stop();
			return PublishingResult.Success(0, 0, _operationStopwatch.Elapsed);
		}

		// Publish all deliveries in parallel
		var tasks = deliveryList.Select(d =>
			PublishToTransportAsync(d.Message, d.Transport, adapter, cancellationToken));

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		var successCount = results.Count(r => r.IsSuccess);
		var failureCount = results.Count(r => !r.IsSuccess);
		var errors = results.Where(r => !r.IsSuccess)
			.Select(r => new PublishingError(
				r.MessageId,
				r.ErrorMessage ?? Resources.MessageBusOutboxPublisher_UnknownError,
				r.Exception))
			.ToList();

		_ = Interlocked.Add(ref _totalPublished, successCount);
		_ = Interlocked.Add(ref _totalFailed, failureCount);

		_operationStopwatch.Stop();

		LogTransportPublishSummary(
			successCount,
			transportName,
			failureCount,
			_operationStopwatch.ElapsedMilliseconds);

		return failureCount > 0
			? PublishingResult.WithFailures(successCount, failureCount, errors, _operationStopwatch.Elapsed)
			: PublishingResult.Success(successCount, 0, _operationStopwatch.Elapsed);
	}

	[LoggerMessage(LogLevel.Debug,
		"Staged message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogStagedMessage(string messageId, string messageType, string destination);

	[LoggerMessage(LogLevel.Debug,
		"Staged multi-transport message {MessageId} of type {MessageType} to {TransportCount} transports: {Transports}")]
	private partial void LogStagedMultiTransportMessage(
		string messageId,
		string messageType,
		int transportCount,
		string transports);

	[LoggerMessage(LogLevel.Information,
		"Published {SuccessCount} messages to transport {TransportName}, {FailureCount} failed in {Duration}ms")]
	private partial void LogTransportPublishSummary(
		int successCount,
		string transportName,
		int failureCount,
		long duration);

	[LoggerMessage(LogLevel.Debug,
		"Published message {MessageId} to transport {TransportName} at {Destination}")]
	private partial void LogPublishedMessageToTransport(
		string messageId,
		string transportName,
		string destination);

	[LoggerMessage(LogLevel.Warning, "Failed to publish message {MessageId} to transport {TransportName}")]
	private partial void LogFailedToPublishToTransport(
		string messageId,
		string transportName,
		Exception ex);

	[LoggerMessage(LogLevel.Warning,
		"Message {MessageId} partially delivered: {SuccessCount} succeeded, {FailureCount} failed")]
	private partial void LogMessagePartiallyDelivered(string messageId, int successCount, int failureCount);

	[LoggerMessage(LogLevel.Debug, "Published message {MessageId} to {Destination}")]
	private partial void LogPublishedMessageToDestination(string messageId, string destination);

	[LoggerMessage(LogLevel.Warning, "Failed to publish message {MessageId} to {Destination}")]
	private partial void LogFailedToPublishToDestination(string messageId, string destination, Exception ex);

	[LoggerMessage(LogLevel.Warning,
		"No transport deliveries found for multi-transport message {MessageId}")]
	private partial void LogNoTransportDeliveries(string messageId);

	private async Task<TransportPublishResult> PublishToTransportAsync(
		OutboundMessage message,
		OutboundMessageTransport transport,
		ITransportAdapter adapter,
		CancellationToken cancellationToken)
	{
		try
		{
			var wrappedMessage = new OutboxDispatchMessage(message.Payload);
			var destination = transport.Destination ?? message.Destination;

			await adapter.SendAsync(wrappedMessage, destination, cancellationToken).ConfigureAwait(false);

			if (_multiTransportStore is not null)
			{
				await _multiTransportStore.MarkTransportSentAsync(message.Id, transport.TransportName, cancellationToken)
					.ConfigureAwait(false);
			}

			LogPublishedMessageToTransport(
				message.Id,
				transport.TransportName,
				destination);

			return TransportPublishResult.Success(message.Id, transport.TransportName);
		}
		catch (Exception ex)
		{
			if (_multiTransportStore is not null)
			{
				await _multiTransportStore.MarkTransportFailedAsync(
					message.Id, transport.TransportName, ex.Message, cancellationToken).ConfigureAwait(false);
			}

			LogFailedToPublishToTransport(message.Id, transport.TransportName, ex);

			return TransportPublishResult.Failure(message.Id, transport.TransportName, ex.Message, ex);
		}
	}

	private async Task<PublishingResult> PublishMessagesAsync(
		IReadOnlyList<OutboundMessage> messages,
		CancellationToken cancellationToken)
	{
		_operationStopwatch.Restart();
		_totalOperations++;
		_lastOperationAt = DateTimeOffset.UtcNow;

		var successCount = 0;
		var failureCount = 0;
		var errors = new List<PublishingError>();

		foreach (var message in messages)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				if (message.IsMultiTransport && _multiTransportStore is not null && _transportRegistry is not null)
				{
					// Multi-transport publishing: publish to all transports in parallel
					var result = await PublishMultiTransportMessageAsync(message, cancellationToken).ConfigureAwait(false);

					if (result.FailureCount == 0)
					{
						successCount++;
						_ = Interlocked.Increment(ref _totalPublished);
					}
					else if (result.SuccessCount == 0)
					{
						failureCount++;
						_ = Interlocked.Increment(ref _totalFailed);
						errors.AddRange(result.Errors);
					}
					else
					{
						// Partial success - count as success but record errors
						successCount++;
						_ = Interlocked.Increment(ref _totalPublished);
						LogMessagePartiallyDelivered(
							message.Id,
							result.SuccessCount,
							result.FailureCount);
					}
				}
				else
				{
					// Single-transport publishing (legacy mode)
					await PublishSingleTransportMessageAsync(message, cancellationToken).ConfigureAwait(false);
					successCount++;
					_ = Interlocked.Increment(ref _totalPublished);
				}

				LogPublishedMessageToDestination(message.Id, message.Destination);
			}
			catch (Exception ex)
			{
				await _outboxStore.MarkFailedAsync(message.Id, ex.Message, message.RetryCount + 1, cancellationToken).ConfigureAwait(false);

				failureCount++;
				_ = Interlocked.Increment(ref _totalFailed);
				errors.Add(new PublishingError(message.Id, ex.Message, ex));

				LogFailedToPublishToDestination(message.Id, message.Destination, ex);
			}
		}

		_operationStopwatch.Stop();

		return failureCount > 0
			? PublishingResult.WithFailures(successCount, failureCount, errors, _operationStopwatch.Elapsed)
			: PublishingResult.Success(successCount, 0, _operationStopwatch.Elapsed);
	}

	private async Task PublishSingleTransportMessageAsync(
		OutboundMessage message,
		CancellationToken cancellationToken)
	{
		if (_messageBus is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_SingleTransportRequiresAdapter);
		}

		var wrappedMessage = new OutboxDispatchMessage(message.Payload);
		var context = new MessageContext(wrappedMessage, _serviceProvider)
		{
			MessageId = message.Id,
			CorrelationId = message.CorrelationId,
			PartitionKey = message.PartitionKey
		};

		_ = await _messageBus.PublishAsync(wrappedMessage, context, cancellationToken).ConfigureAwait(false);
		await _outboxStore.MarkSentAsync(message.Id, cancellationToken).ConfigureAwait(false);
	}

	private async Task<PublishingResult> PublishMultiTransportMessageAsync(
		OutboundMessage message,
		CancellationToken cancellationToken)
	{
		if (_multiTransportStore is null || _transportRegistry is null)
		{
			throw new InvalidOperationException(
				Resources.MessageBusOutboxPublisher_MultiTransportRequiresRegistry);
		}

		// Get transport deliveries for this message
		var deliveries = message.TransportDeliveries.Count > 0
			? message.TransportDeliveries
			:
			[
				.. await _multiTransportStore.GetTransportDeliveriesAsync(message.Id, cancellationToken)
					.ConfigureAwait(false)
			];

		if (deliveries.Count == 0)
		{
			LogNoTransportDeliveries(message.Id);
			return PublishingResult.Success(0, 0, TimeSpan.Zero);
		}

		// Publish to all transports in parallel
		var tasks = new List<Task<TransportPublishResult>>();

		foreach (var delivery in deliveries.Where(d => d.Status == TransportDeliveryStatus.Pending))
		{
			var adapter = _transportRegistry.GetTransportAdapter(delivery.TransportName);
			if (adapter is null)
			{
				// Mark as skipped if transport adapter not found
				await _multiTransportStore.MarkTransportSkippedAsync(
						message.Id,
						delivery.TransportName,
						Resources.MessageBusOutboxPublisher_TransportAdapterNotFound,
						cancellationToken)
					.ConfigureAwait(false);
				continue;
			}

			tasks.Add(PublishToTransportAsync(message, delivery, adapter, cancellationToken));
		}

		if (tasks.Count == 0)
		{
			return PublishingResult.Success(0, 0, TimeSpan.Zero);
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		var successCount = results.Count(r => r.IsSuccess);
		var failureCount = results.Count(r => !r.IsSuccess);
		var errors = results.Where(r => !r.IsSuccess)
			.Select(r => new PublishingError(
				r.MessageId,
				r.ErrorMessage ?? Resources.MessageBusOutboxPublisher_UnknownError,
				r.Exception))
			.ToList();

		// Update aggregate message status
		await _multiTransportStore.UpdateAggregateStatusAsync(message.Id, cancellationToken).ConfigureAwait(false);

		return failureCount > 0
			? PublishingResult.WithFailures(successCount, failureCount, errors, TimeSpan.Zero)
			: PublishingResult.Success(successCount, 0, TimeSpan.Zero);
	}

	/// <summary>
	/// Result of publishing to a single transport.
	/// </summary>
	private readonly struct TransportPublishResult
	{
		private TransportPublishResult(
			string messageId,
			string transportName,
			bool isSuccess,
			string? errorMessage,
			Exception? exception)
		{
			MessageId = messageId;
			TransportName = transportName;
			IsSuccess = isSuccess;
			ErrorMessage = errorMessage;
			Exception = exception;
		}

		public string MessageId { get; }
		public string TransportName { get; }
		public bool IsSuccess { get; }
		public string? ErrorMessage { get; }
		public Exception? Exception { get; }

		public static TransportPublishResult Success(string messageId, string transportName)
			=> new(messageId, transportName, true, null, null);

		public static TransportPublishResult Failure(string messageId, string transportName, string errorMessage,
			Exception? exception = null)
			=> new(messageId, transportName, false, errorMessage, exception);
	}

	/// <summary>
	/// Wrapper message for outbox payloads that implements IDispatchMessage.
	/// </summary>
	private sealed class OutboxDispatchMessage : IDispatchMessage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="OutboxDispatchMessage" /> class.
		/// </summary>
		/// <param name="payload"> The serialized message payload. </param>
		public OutboxDispatchMessage(byte[] payload)
		{
			Payload = payload ?? throw new ArgumentNullException(nameof(payload));
		}

		/// <summary>
		/// Gets the serialized message payload.
		/// </summary>
		public byte[] Payload { get; }
	}
}
