// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery.BatchProcessing;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Queues;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.Outbox.Diagnostics;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DeliveryMetadata = Excalibur.Dispatch.Messaging.MessageMetadata;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// High-performance outbox processor implementation that manages message queuing, batching, and dispatch operations. This component
/// implements the outbox pattern with advanced features including dynamic batch sizing, parallel processing, telemetry integration, and
/// comprehensive error handling for reliable message delivery.
/// </summary>
/// <remarks>
/// CA1506 suppressed: OutboxProcessor is a core coordinator that legitimately orchestrates message dispatch through multiple subsystems
/// (batching, telemetry, persistence, scheduling, error handling). High coupling is inherent to its coordination responsibilities and
/// cannot be reduced without fragmenting cohesive functionality.
/// </remarks>
[SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
	Justification = "Core coordinator that legitimately orchestrates dispatch through multiple subsystems")]
public sealed partial class OutboxProcessor : IOutboxProcessor
{
	/// <summary>
	/// Format marker byte for MemoryPack envelope format. Binary envelope data starts with 0x01, while JSON data starts with 0x7B ('{').
	/// </summary>
	private const byte EnvelopeFormatMarker = 0x01;

	/// <summary>
	/// Cached composite format for performance.
	/// </summary>
	private static readonly CompositeFormat AttemptedToRunWithoutCallingInitFormat =
		CompositeFormat.Parse(ErrorConstants.AttemptedToRunWithoutCallingInit);

	private readonly OutboxOptions _options;
	private readonly Channel<IOutboxMessage> _outboxMessages;
	private readonly int _queueCapacity;

	// Orchestration layer IOutboxStore for integration event outbox (restored from git history)
	private readonly IOutboxStore _outboxStore;

	private readonly IJsonSerializer _serializer;
	private readonly IInternalSerializer? _internalSerializer;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<OutboxProcessor> _logger;
	private readonly TelemetryClient? _telemetryClient;
	private readonly BatchProcessingMetrics _batchMetrics;
	private readonly DynamicBatchSizeCalculator? _batchSizeCalculator;

	// Resilience components
	private readonly IDeadLetterQueue _deadLetterQueue;

	private readonly ITransportCircuitBreakerRegistry _circuitBreakerRegistry;
	private readonly IBackoffCalculator _backoffCalculator;
	private readonly DeliveryGuaranteeOptions _deliveryGuaranteeOptions;

	private int _disposedFlag;

	private Task? _producerTask;

	private Task<int>? _consumerTask;

	private volatile bool _producerStopped;

	private string? _dispatcherId;

	private InstanceAwareQueue? _idQueue;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxProcessor" /> class. Initializes a new instance of the OutboxProcessor with
	/// required dependencies and configuration. This constructor sets up the message queue, batch processing metrics, dynamic batch sizing,
	/// and validates configuration parameters for optimal performance and reliability.
	/// </summary>
	/// <param name="options"> Configuration options for outbox processing behavior and performance tuning. </param>
	/// <param name="outboxStore"> Persistent store for outbox message management and retrieval operations. </param>
	/// <param name="serializer"> JSON serializer for message and metadata serialization/deserialization. </param>
	/// <param name="serviceProvider"> Service provider for dependency injection and message bus resolution. </param>
	/// <param name="logger"> Logger for outbox processing activities, errors, and performance monitoring. </param>
	/// <param name="telemetryClient"> Optional Application Insights telemetry client for advanced monitoring. </param>
	/// <param name="internalSerializer"> Optional internal serializer for high-performance binary envelope serialization. </param>
	/// <param name="deadLetterQueue"> Optional dead letter queue for failed messages. Uses NullDeadLetterQueue if not provided. </param>
	/// <param name="circuitBreakerRegistry">
	/// Optional circuit breaker registry for transport resilience. Uses NullTransportCircuitBreakerRegistry if not provided.
	/// </param>
	/// <param name="backoffCalculator">
	/// Optional backoff calculator for retry delays. Uses ExponentialBackoffCalculator.Default if not provided.
	/// </param>
	/// <param name="deliveryGuaranteeOptions"> Optional delivery guarantee options. Uses default at-least-once semantics if not provided. </param>
	/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when configuration validation fails. </exception>
	public OutboxProcessor(
		IOptions<OutboxOptions> options,
		IOutboxStore outboxStore,
		IJsonSerializer serializer,
		IServiceProvider serviceProvider,
		ILogger<OutboxProcessor> logger,
		TelemetryClient? telemetryClient = null,
		IInternalSerializer? internalSerializer = null,
		IDeadLetterQueue? deadLetterQueue = null,
		ITransportCircuitBreakerRegistry? circuitBreakerRegistry = null,
		IBackoffCalculator? backoffCalculator = null,
		IOptions<DeliveryGuaranteeOptions>? deliveryGuaranteeOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(outboxStore);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;

		if (_options.QueueCapacity < _options.ProducerBatchSize)
		{
			throw new InvalidOperationException(
				Excalibur.Outbox.Resources.OutboxProcessor_QueueCapacityCannotBeLessThanProducerBatchSize);
		}

		_outboxStore = outboxStore;
		_serializer = serializer;
		_internalSerializer = internalSerializer;
		_serviceProvider = serviceProvider;
		_logger = logger;
		_telemetryClient = telemetryClient;
		_queueCapacity = options.Value.QueueCapacity;
		_outboxMessages = Channel.CreateBounded<IOutboxMessage>(new BoundedChannelOptions(options.Value.QueueCapacity)
		{
			FullMode = BoundedChannelFullMode.Wait, SingleReader = false, SingleWriter = false, AllowSynchronousContinuations = false,
		});
		_batchMetrics = new BatchProcessingMetrics($"OutboxProcessor.{nameof(BatchProcessingMetrics)}", telemetryClient);

		// Initialize resilience components with null object pattern defaults
		_deadLetterQueue = deadLetterQueue ?? NullDeadLetterQueue.Instance;
		_circuitBreakerRegistry = circuitBreakerRegistry ?? NullTransportCircuitBreakerRegistry.Instance;
		_backoffCalculator = backoffCalculator ?? ExponentialBackoffCalculator.CreateForMessageQueue();
		_deliveryGuaranteeOptions = deliveryGuaranteeOptions?.Value ?? new DeliveryGuaranteeOptions();

		if (_options.EnableDynamicBatchSizing)
		{
			_batchSizeCalculator = new DynamicBatchSizeCalculator(
				_options.MinBatchSize,
				_options.MaxBatchSize,
				_options.ConsumerBatchSize);
		}
	}

	/// <summary>
	/// Initializes the outbox processor with a unique dispatcher identifier and starts background processing tasks. This method sets up the
	/// producer-consumer pipeline, message queuing, and begins continuous outbox processing with proper error handling and graceful
	/// shutdown support.
	/// </summary>
	/// <param name="dispatcherId"> Unique identifier for this dispatcher instance, used for message ownership and coordination. </param>
	/// <exception cref="ArgumentException"> Thrown when dispatcherId is null, empty, or whitespace. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when processor is already initialized or in invalid state. </exception>
	public void Init(string dispatcherId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherId);

		_dispatcherId = dispatcherId;
		var orderedQueue = new SimpleOrderedSet<string>(_queueCapacity + _options.ProducerBatchSize);
		_idQueue = new InstanceAwareQueue(dispatcherId, orderedQueue);
	}

	/// <summary>
	/// Processes and dispatches all pending messages from the outbox using producer-consumer pattern with batch processing. This method
	/// implements the core outbox functionality with parallel message processing, dynamic batch sizing, comprehensive error handling, and
	/// telemetry integration for reliable message delivery.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token to support graceful shutdown and timeout scenarios. </param>
	/// <returns> Task containing the total number of messages successfully dispatched from the outbox. </returns>
	/// <exception cref="ObjectDisposedException"> Thrown when the processor has been disposed. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when processor is not properly initialized via Init method. </exception>
	/// <exception cref="MessagingException"> Thrown when message processing fails due to broker or serialization issues. </exception>
	public async Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		if (string.IsNullOrWhiteSpace(_dispatcherId) || _idQueue == null)
		{
			throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, AttemptedToRunWithoutCallingInitFormat,
				nameof(DispatchPendingMessagesAsync), nameof(Init)));
		}

		// Get unsent messages for processing (using available interface method)
		var unsentMessages = await _outboxStore
			.GetUnsentMessagesAsync(_options.ProducerBatchSize, cancellationToken)
			.ConfigureAwait(false);

		_producerTask = Task.Factory
			.StartNew(
				() => ProducerLoopAsync(cancellationToken),
				cancellationToken,
				TaskCreationOptions.LongRunning,
				TaskScheduler.Default)
			.Unwrap();
		_consumerTask = Task.Factory
			.StartNew(
				() => ConsumerLoopAsync(cancellationToken),
				cancellationToken,
				TaskCreationOptions.LongRunning,
				TaskScheduler.Default)
			.Unwrap();

		await _producerTask.ConfigureAwait(false);
		var consumerResult = await _consumerTask.ConfigureAwait(false);

		return consumerResult;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCoreAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	private static async ValueTask SafeDisposeAsync(object resource)
	{
		if (resource is IAsyncDisposable resourceAsyncDisposable)
		{
			await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
			return;
		}

		if (resource is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Checks if the payload data is in binary envelope format.
	/// </summary>
	/// <param name="payload"> The payload data to check. </param>
	/// <returns> True if payload starts with envelope format marker (0x01), false otherwise. </returns>
	private static bool IsEnvelopeFormat(byte[] payload) =>
		payload.Length > 0 && payload[0] == EnvelopeFormatMarker;

	/// <summary>
	/// Converts an OutboundMessage to IOutboxMessage format. Handles both binary envelope format and legacy JSON format.
	/// </summary>
	/// <param name="outboundMessage"> The outbound message to convert. </param>
	/// <returns> An IOutboxMessage implementation. </returns>
	private IOutboxMessage ConvertToOutboxMessageWithEnvelopeSupport(OutboundMessage outboundMessage)
	{
		// Check if payload is in binary envelope format and we have a deserializer
		if (_internalSerializer is not null && IsEnvelopeFormat(outboundMessage.Payload))
		{
			// Skip the format marker byte and deserialize the envelope
			var envelopeData = outboundMessage.Payload.AsSpan(1);
			var envelope = DeserializeEnvelope(envelopeData);

			if (envelope is not null)
			{
				// Convert envelope back to IOutboxMessage with the original payload
				var headersJson = envelope.Headers is not null
					? JsonSerializer.Serialize(envelope.Headers, CoreMessageJsonContext.Default.DictionaryStringString)
					: "{}";

				return new OutboxMessage(
					messageId: envelope.MessageId.ToString(),
					messageType: envelope.MessageType ?? outboundMessage.MessageType,
					messageMetadata: headersJson,
					messageBody: envelope.Payload is not null ? Encoding.UTF8.GetString(envelope.Payload) : string.Empty,
					createdAt: envelope.CreatedAt,
					expiresAt: outboundMessage.ScheduledAt)
				{
					Attempts = outboundMessage.RetryCount, DispatcherId = null, DispatcherTimeout = null,
				};
			}
		}

		// Fallback to legacy JSON format conversion
		return ConvertToOutboxMessageLegacy(outboundMessage);
	}

	/// <summary>
	/// Converts an OutboundMessage to IOutboxMessage format using legacy JSON approach.
	/// </summary>
	/// <param name="outboundMessage"> The outbound message to convert. </param>
	/// <returns> An IOutboxMessage implementation. </returns>
	private static OutboxMessage ConvertToOutboxMessageLegacy(OutboundMessage outboundMessage) =>
		new OutboxMessage(
			messageId: outboundMessage.Id,
			messageType: outboundMessage.MessageType,
			messageMetadata: JsonSerializer.Serialize(outboundMessage.Headers, CoreMessageJsonContext.Default.DictionaryStringObject),
			messageBody: Encoding.UTF8.GetString(outboundMessage.Payload),
			createdAt: outboundMessage.CreatedAt,
			expiresAt: outboundMessage.ScheduledAt)
		{
			Attempts = outboundMessage.RetryCount,
			DispatcherId = null, // OutboundMessage doesn't have this property
			DispatcherTimeout = null, // OutboundMessage doesn't have this property
		};

	/// <summary>
	/// Deserializes an OutboxEnvelope from binary format if IInternalSerializer is available.
	/// </summary>
	/// <param name="data"> The serialized envelope data. </param>
	/// <returns> The deserialized envelope, or null if internal serializer is not configured. </returns>
	private OutboxEnvelope? DeserializeEnvelope(ReadOnlySpan<byte> data)
	{
		if (_internalSerializer is null)
		{
			return null;
		}

		return _internalSerializer.Deserialize<OutboxEnvelope>(data);
	}

	/// <summary>
	/// Disposes of resources used by the <see cref="OutboxProcessor" />.
	/// </summary>
	private async ValueTask DisposeAsyncCoreAsync()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		LogDisposingResources();

		try
		{
			if (_consumerTask is { IsCompleted: false })
			{
				LogConsumerNotCompleted();

				try
				{
					_ = await _consumerTask.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
				}
				catch (TimeoutException ex)
				{
					LogConsumerTimeoutDuringDisposal(ex);
				}
			}

			if (_producerTask is not null)
			{
				await SafeDisposeAsync(_producerTask).ConfigureAwait(false);
			}

			if (_consumerTask is not null)
			{
				await SafeDisposeAsync(_consumerTask).ConfigureAwait(false);
			}

			if (_idQueue is not null)
			{
				await SafeDisposeAsync(_idQueue).ConfigureAwait(false);
			}

			_ = _outboxMessages.Writer.TryComplete();

			_batchMetrics?.Dispose();
		}
		catch (Exception ex)
		{
			LogErrorDisposingAsyncResources(ex);
		}
	}

	private async Task ProducerLoopAsync(CancellationToken cancellationToken)
	{
		try
		{
			var totalQueued = 0;
			var reachedLimit = false;

			while (!cancellationToken.IsCancellationRequested && !reachedLimit)
			{
				var availableSlots = Math.Max(0, _queueCapacity - _outboxMessages.Reader.Count);
				var remainingMessages = _options.PerRunTotal > 0 ? _options.PerRunTotal - totalQueued : _options.ProducerBatchSize;
				var batchSize = Math.Min(_options.ProducerBatchSize, remainingMessages);

				if (availableSlots < batchSize)
				{
					// Event-driven wait: block until channel has capacity instead of polling
					if (!await _outboxMessages.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
					{
						// Channel completed, exit producer
						break;
					}

					continue;
				}

				var batch = await ReserveBatchRecordsAsync(batchSize, cancellationToken).ConfigureAwait(false);

				if (batch.Count == 0)
				{
					LogProducerIdleExiting(_dispatcherId);

					break;
				}

				LogEnqueuingBatchRecords(batch.Count);

				foreach (var outboxRecord in batch)
				{
					if (await _idQueue.AddAsync(outboxRecord.MessageId, cancellationToken).ConfigureAwait(false))
					{
						await _outboxMessages.Writer.WriteAsync(outboxRecord, cancellationToken).ConfigureAwait(false);
						totalQueued++;
					}
				}

				reachedLimit = _options.PerRunTotal > 0 && totalQueued >= _options.PerRunTotal;

				_telemetryClient?.GetMetric("Outbox.BatchSize").TrackValue(batch.Count);
			}
		}
		catch (OperationCanceledException)
		{
			LogOutboxProducerCanceled();
		}
		catch (Exception ex)
		{
			LogErrorInProducerLoop(ex);

			throw;
		}
		finally
		{
			_producerStopped = true;
			_outboxMessages.Writer.Complete();

			LogProducerCompleted();
		}
	}

	private async Task<int> ConsumerLoopAsync(CancellationToken cancellationToken)
	{
		var totalProcessedCount = 0;

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (_disposedFlag == 1)
				{
					LogDisposalRequestedExitingData();

					break;
				}

				if (_producerStopped && _outboxMessages.Reader.Count == 0)
				{
					LogConsumerExiting();

					break;
				}

				var batchSize = _batchSizeCalculator?.CurrentBatchSize ?? _options.ConsumerBatchSize;
				var batch = await ChannelBatchUtilities.DequeueBatchAsync(_outboxMessages.Reader, batchSize, cancellationToken)
					.ConfigureAwait(false);

				if (batch.Length == 0)
				{
					continue;
				}

				var stopwatch = ValueStopwatch.StartNew();

				if (_options.ParallelProcessingDegree > 1)
				{
					// Parallel batch processing
					var processedCount = await ProcessBatchParallelAsync(batch, cancellationToken).ConfigureAwait(false);
					totalProcessedCount += processedCount;
				}
				else
				{
					// Sequential processing (backward compatibility)
					foreach (var record in batch)
					{
						await DispatchReservedRecordAsync(record, cancellationToken).ConfigureAwait(false);
						totalProcessedCount++;
					}
				}

				// Record batch metrics
				var duration = stopwatch.Elapsed;
				_batchMetrics.RecordBatchCompleted(
					batch.Length,
					batch.Length, // Will be updated with actual success/failure counts
					0,
					duration,
					new Dictionary<string, object?>(StringComparer.Ordinal) { ["ProcessorType"] = "Outbox" });

				_telemetryClient?.TrackMetric("OutboxMessagesDispatched", totalProcessedCount);
			}
		}
		catch (OperationCanceledException)
		{
			LogConsumerCanceled();
		}
		catch (Exception ex)
		{
			LogErrorInConsumerLoop(ex);

			throw;
		}

		LogOutboxProcessingCompleted(totalProcessedCount);

		_telemetryClient?.GetMetric("Outbox.RecordsProcessed").TrackValue(totalProcessedCount);

		return totalProcessedCount;
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox batch reservation converts outbound records using runtime serialization.")]
	private async Task<IReadOnlyCollection<IOutboxMessage>> ReserveBatchRecordsAsync(int batchSize, CancellationToken cancellationToken)
	{
		var records = await _outboxStore
			.GetUnsentMessagesAsync(batchSize, cancellationToken)
			.ConfigureAwait(false);

		// Convert OutboundMessage to IOutboxMessage with envelope format support (ADR-058)
		var outboxMessages = records.Select(ConvertToOutboxMessageWithEnvelopeSupport).ToList().AsReadOnly();
		return outboxMessages;
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification =
			"Outbox message dispatch requires runtime type resolution from MessageTypeRegistry for polymorphic message types - reflection is intentional")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox dispatch uses runtime deserialization for stored message payloads.")]
	private async Task DispatchReservedRecordAsync(IOutboxMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var stopwatch = ValueStopwatch.StartNew();
		var attempt = message.Attempts + 1;

		// Get circuit breaker for the transport (uses message type as transport name for now)
		var circuitBreaker = _circuitBreakerRegistry.GetOrCreate(message.MessageType);

		// Check if circuit breaker is open - route directly to DLQ
		if (circuitBreaker.State == CircuitState.Open)
		{
			LogCircuitBreakerOpen(message.MessageType, message.MessageId);
			await RouteToDeadLetterQueueAsync(message, DeadLetterReason.CircuitBreakerOpen, null, cancellationToken).ConfigureAwait(false);
			return;
		}

		try
		{
			_telemetryClient?.TrackEvent(
				"Outbox.OutboxRecordDispatchStarted",
				new Dictionary<string, string>(StringComparer.Ordinal)
				{
					{ "DispatcherId", _dispatcherId }, { "MessageId", message.MessageId }
				});

			LogDispatchingOutboxRecord(message.MessageId, _dispatcherId);

			// Execute dispatch through circuit breaker
			await circuitBreaker.ExecuteAsync(async ct =>
			{
				await DispatchAsync(message, ct).ConfigureAwait(false);
				return true;
			}, cancellationToken).ConfigureAwait(false);

			// Record success for circuit breaker
			circuitBreaker.RecordSuccess();

			LogSuccessfullyDispatchedOutboxRecord(message.MessageId, _dispatcherId);

			_telemetryClient?.TrackMetric("Outbox.MessageProcessingDuration", stopwatch.Elapsed.TotalMilliseconds);
			_telemetryClient?.TrackEvent(
				"Outbox.OutboxRecordDispatchSucceeded",
				new Dictionary<string, string>(StringComparer.Ordinal)
				{
					{ "DispatcherId", _dispatcherId }, { "MessageId", message.MessageId }
				});

			await _outboxStore.MarkSentAsync(message.MessageId, cancellationToken).ConfigureAwait(false);

			LogMarkedOutboxRecordSent(message.MessageId);
		}
		catch (CircuitBreakerOpenException)
		{
			// Circuit opened during execution - route to DLQ
			LogCircuitBreakerOpen(message.MessageType, message.MessageId);
			await RouteToDeadLetterQueueAsync(message, DeadLetterReason.CircuitBreakerOpen, null, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// Record failure for circuit breaker
			circuitBreaker.RecordFailure(ex);

			LogErrorDispatchingOutboxRecord(message.MessageId, _dispatcherId, ex);

			_telemetryClient?.TrackException(
				ex,
				new Dictionary<string, string>
					(StringComparer.Ordinal)
					{
						{ "DispatcherId", _dispatcherId }, { "MessageId", message.MessageId }, { "ErrorType", ex.GetType().Name },
					});

			// Check if max retries exceeded
			if (attempt >= _options.MaxAttempts)
			{
				// Route to dead letter queue
				await RouteToDeadLetterQueueAsync(message, DeadLetterReason.MaxRetriesExceeded, ex, cancellationToken)
					.ConfigureAwait(false);
			}
			else if (_deliveryGuaranteeOptions.EnableAutomaticRetry)
			{
				// Calculate backoff delay for next retry
				var backoffDelay = _backoffCalculator.CalculateDelay(attempt);
				LogRetryWithBackoff(message.MessageId, attempt, backoffDelay.TotalMilliseconds);

				// Mark as failed with incremented retry count (scheduler will pick up after delay)
				await _outboxStore.MarkFailedAsync(message.MessageId, ex.Message, attempt, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// Automatic retry disabled, mark as failed
				await _outboxStore.MarkFailedAsync(message.MessageId, ex.Message, attempt, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Routes a failed message to the dead letter queue.
	/// </summary>
	private async Task RouteToDeadLetterQueueAsync(
		IOutboxMessage message,
		DeadLetterReason reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		var reasonText = reason switch
		{
			DeadLetterReason.MaxRetriesExceeded => "Max retries exceeded",
			DeadLetterReason.CircuitBreakerOpen => "Circuit breaker open",
			DeadLetterReason.MessageExpired => "Message expired",
			DeadLetterReason.DeserializationFailed => "Deserialization failed",
			DeadLetterReason.HandlerNotFound => "Handler not found",
			DeadLetterReason.ValidationFailed => "Validation failed",
			DeadLetterReason.UnhandledException => "Unhandled exception",
			DeadLetterReason.PoisonMessage => "Poison message",
			_ => reason.ToString()
		};

		LogMessageRoutedToDlq(message.MessageId, reasonText);

		var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["MessageType"] = message.MessageType,
			["DispatcherId"] = _dispatcherId ?? string.Empty,
			["Attempts"] = message.Attempts.ToString(CultureInfo.InvariantCulture),
			["CreatedAt"] = message.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
		};

		_ = await _deadLetterQueue.EnqueueAsync(
			message,
			reason,
			cancellationToken,
			exception,
			metadata).ConfigureAwait(false);

		// Mark the message as moved to DLQ in the outbox store
		await _outboxStore.MarkFailedAsync(
			message.MessageId,
			$"Moved to DLQ: {reasonText}",
			_options.MaxAttempts, // Mark as max attempts to prevent reprocessing
			cancellationToken).ConfigureAwait(false);
	}

	private async Task<int> ProcessBatchParallelAsync(IOutboxMessage[] batch, CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var successfulIds = new ConcurrentBag<string>();
		var failedToRetry = new ConcurrentBag<string>();
		var failedToDeadLetter =
			new ConcurrentBag<(string MessageId, IOutboxMessage Message, Exception? Exception, DeadLetterReason Reason)>();

		// Process messages in parallel
		var (successful, failed) = await Batching.ProcessBatchAsync(
			batch,
			async (message, ct) =>
			{
				var attempt = message.Attempts + 1;

				// Get circuit breaker for the transport
				var circuitBreaker = _circuitBreakerRegistry.GetOrCreate(message.MessageType);

				// Check if circuit breaker is open - route directly to DLQ
				if (circuitBreaker.State == CircuitState.Open)
				{
					LogCircuitBreakerOpen(message.MessageType, message.MessageId);
					failedToDeadLetter.Add((message.MessageId, message, null, DeadLetterReason.CircuitBreakerOpen));

					return; // Don't throw - we've handled this case
				}

				try
				{
					// Execute dispatch through circuit breaker
					await circuitBreaker.ExecuteAsync(async token =>
					{
						await DispatchSingleMessageAsync(message, token).ConfigureAwait(false);
						return true;
					}, ct).ConfigureAwait(false);

					// Record success for circuit breaker
					circuitBreaker.RecordSuccess();

					// AD-222-1: For MinimizedWindow, mark sent immediately after dispatch
					if (_options.DeliveryGuarantee == OutboxDeliveryGuarantee.MinimizedWindow)
					{
						await _outboxStore.MarkSentAsync(message.MessageId, ct).ConfigureAwait(false);
					}
					else
					{
						// For AtLeastOnce and TransactionalWhenApplicable, collect for batch completion
						successfulIds.Add(message.MessageId);
					}
				}
				catch (CircuitBreakerOpenException)
				{
					// Circuit opened during execution
					LogCircuitBreakerOpen(message.MessageType, message.MessageId);
					failedToDeadLetter.Add((message.MessageId, message, null, DeadLetterReason.CircuitBreakerOpen));
				}
				catch (Exception ex)
				{
					// Record failure for circuit breaker
					circuitBreaker.RecordFailure(ex);

					LogErrorDispatchingOutboxRecord(message.MessageId, _dispatcherId, ex);

					if (attempt >= _options.MaxAttempts)
					{
						failedToDeadLetter.Add((message.MessageId, message, ex, DeadLetterReason.MaxRetriesExceeded));
					}
					else if (_deliveryGuaranteeOptions.EnableAutomaticRetry)
					{
						// Calculate backoff delay for logging
						var backoffDelay = _backoffCalculator.CalculateDelay(attempt);
						LogRetryWithBackoff(message.MessageId, attempt, backoffDelay.TotalMilliseconds);

						failedToRetry.Add(message.MessageId);
					}
					else
					{
						failedToRetry.Add(message.MessageId);
					}

					throw; // Re-throw for Batching to track
				}
			},
			_options.ParallelProcessingDegree,
			_options.BatchProcessingTimeout,
			cancellationToken).ConfigureAwait(false);

		// Route messages to DLQ
		foreach (var (messageId, message, exception, reason) in failedToDeadLetter)
		{
			await RouteToDeadLetterQueueAsync(message, reason, exception, cancellationToken).ConfigureAwait(false);
		}

		// AD-222-1: Branch on DeliveryGuarantee setting for batch completion
		var successfulIdsOnly = successfulIds.ToList();

		// For MinimizedWindow, messages were already marked individually in the dispatch loop
		if (_options.DeliveryGuarantee != OutboxDeliveryGuarantee.MinimizedWindow && successfulIdsOnly.Count > 0)
		{
			// AD-222-3: TransactionalWhenApplicable - check store capability and use transaction if supported
			if (_options.DeliveryGuarantee == OutboxDeliveryGuarantee.TransactionalWhenApplicable)
			{
				await MarkWithTransactionOrFallbackAsync(successfulIdsOnly, cancellationToken).ConfigureAwait(false);
			}
			else if (_options.EnableBatchDatabaseOperations)
			{
				// AtLeastOnce with batch operations
				await PerformBatchDatabaseOperationsAsync(successfulIdsOnly, failedToRetry.ToList(), new List<string>(), cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				// AtLeastOnce without batch operations - fall back to individual operations
				foreach (var id in successfulIdsOnly)
				{
					await _outboxStore.MarkSentAsync(id, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		// Handle failed messages for retry (applies to all guarantee levels)
		if (!failedToRetry.IsEmpty)
		{
			if (_options.EnableBatchDatabaseOperations && _options.DeliveryGuarantee != OutboxDeliveryGuarantee.MinimizedWindow)
			{
				// Batch operations handled above for non-MinimizedWindow
				foreach (var id in failedToRetry)
				{
					await _outboxStore.MarkFailedAsync(id, ErrorConstants.RetryAttempt, 1, cancellationToken).ConfigureAwait(false);
				}
			}
			else
			{
				foreach (var id in failedToRetry)
				{
					await _outboxStore.MarkFailedAsync(id, ErrorConstants.RetryAttempt, 1, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		// Update dynamic batch size if enabled
		if (_batchSizeCalculator != null)
		{
			var successRate = batch.Length > 0 ? (double)successful.Count / batch.Length : 0;
			_batchSizeCalculator.RecordBatchResult(batch.Length, stopwatch.Elapsed, successRate);
		}

		// Record detailed metrics
		_batchMetrics.RecordBatchCompleted(
			batch.Length,
			successful.Count,
			failed.Count,
			stopwatch.Elapsed,
			new Dictionary<string, object?>
				(StringComparer.Ordinal)
				{
					["ProcessorType"] = "Outbox",
					["ParallelDegree"] = _options.ParallelProcessingDegree,
					["BatchOperationsEnabled"] = _options.EnableBatchDatabaseOperations,
				});

		return successful.Count;
	}

	private async Task PerformBatchDatabaseOperationsAsync(
		List<string> successfulIds,
		List<string> failedToRetry,
		List<string> failedToDeadLetter,
		CancellationToken cancellationToken)
	{
		var tasks = new List<Task>();

		if (successfulIds.Count > 0)
		{
			// Process successful messages in parallel
			tasks.Add(Task.Factory.StartNew(async () =>
			{
				foreach (var id in successfulIds)
				{
					await _outboxStore.MarkSentAsync(id, cancellationToken).ConfigureAwait(false);
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
		}

		if (failedToRetry.Count > 0)
		{
			// Process retry messages in parallel
			tasks.Add(Task.Factory.StartNew(async () =>
			{
				foreach (var id in failedToRetry)
				{
					await _outboxStore.MarkFailedAsync(id, ErrorConstants.RetryAttempt, 1, cancellationToken).ConfigureAwait(false);
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
		}

		if (failedToDeadLetter.Count > 0)
		{
			// Process dead letter messages in parallel
			tasks.Add(Task.Factory.StartNew(async () =>
			{
				foreach (var id in failedToDeadLetter)
				{
					await _outboxStore
						.MarkFailedAsync(id, ErrorConstants.MaxRetriesReachedMovedToDeadLetter, _options.MaxAttempts, cancellationToken)
						.ConfigureAwait(false);
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Marks messages as sent using transactional operations if supported, otherwise falls back to MinimizedWindow behavior.
	/// </summary>
	/// <param name="messageIds"> The IDs of messages to mark as sent. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <remarks>
	/// <para>
	/// Implements fallback behavior for TransactionalWhenApplicable.
	/// </para>
	/// <para>
	/// When the store supports transactions (implements <see cref="ITransactionalOutboxStore" /> with <c> SupportsTransactions = true
	/// </c>), all messages are marked atomically within a single transaction for exactly-once semantics.
	/// </para>
	/// <para> When transactions are not supported, falls back to individual message completion (MinimizedWindow behavior) and logs a warning. </para>
	/// </remarks>
	private async Task MarkWithTransactionOrFallbackAsync(
		List<string> messageIds,
		CancellationToken cancellationToken)
	{
		// Check if store supports transactions
		if (_outboxStore is ITransactionalOutboxStore txStore && txStore.SupportsTransactions)
		{
			// Use transactional completion (exactly-once)
			await txStore.MarkSentTransactionalAsync(messageIds, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			// Fall back to MinimizedWindow behavior (individual completion)
			foreach (var id in messageIds)
			{
				await _outboxStore.MarkSentAsync(id, cancellationToken).ConfigureAwait(false);
			}

			LogTransactionalFallback(messageIds.Count);
		}
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification =
			"Outbox message dispatch requires runtime type resolution from MessageTypeRegistry for polymorphic message types - reflection is intentional")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox dispatch uses runtime deserialization for stored message payloads.")]
	[SuppressMessage("Style", "RCS1163:Unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency and future cancellation support")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency and future cancellation support")]
	private async Task DispatchSingleMessageAsync(IOutboxMessage message, CancellationToken cancellationToken)
	{
		_telemetryClient?.TrackEvent(
			"Outbox.OutboxRecordDispatchStarted",
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "DispatcherId", _dispatcherId }, { "MessageId", message.MessageId }
			});

		LogDispatchingOutboxRecord(message.MessageId, _dispatcherId);

		await DispatchAsync(message, cancellationToken).ConfigureAwait(false);

		LogSuccessfullyDispatchedOutboxRecord(message.MessageId, _dispatcherId);

		_telemetryClient?.TrackEvent(
			"Outbox.OutboxRecordDispatchSucceeded",
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "DispatcherId", _dispatcherId }, { "MessageId", message.MessageId }
			});
	}

	[RequiresUnreferencedCode("Uses DeserializeAsync with runtime type resolution from MessageTypeRegistry")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer.DeserializeAsync(String, Type)")]
	private async Task DispatchAsync(IOutboxMessage outboxMessage, CancellationToken cancellationToken)
	{
		if (await _serializer.DeserializeAsync(outboxMessage.MessageBody, typeof(OutboxMessage)).ConfigureAwait(false) is not OutboxMessage
		    message)
		{
			_telemetryClient?.TrackMetric("Outbox.EmptyOutboxMessage", 1);
			return;
		}

		try
		{
			var type = MessageTypeRegistry.GetType(message.MessageType)
			           ?? throw new TypeLoadException($"{ErrorConstants.TypeNotFoundInRegistry}: {message.MessageType}");

			if (await _serializer.DeserializeAsync(message.MessageBody, type).ConfigureAwait(false) is not IIntegrationEvent
			    integrationEvent)
			{
				throw new InvalidOperationException(
					$"{ErrorConstants.CouldNotDeserializeAsIntegrationEvent}: {message.MessageType}");
			}

			var deserializedMetadata =
				await _serializer.DeserializeAsync(message.MessageMetadata, typeof(DeliveryMetadata)).ConfigureAwait(false);
			if (deserializedMetadata is not DeliveryMetadata deliveryMetadata)
			{
				throw new InvalidOperationException(
					$"{ErrorConstants.FailedToDeserializeMessageMetadata}: {message.MessageId}");
			}

			using var scope = _serviceProvider.CreateScope();

			var metaDictionary = new Dictionary<string, string?>
				(StringComparer.Ordinal)
				{
					["CorrelationId"] = deliveryMetadata.CorrelationId,
					["CausationId"] = deliveryMetadata.CausationId,
					["TraceParent"] = deliveryMetadata.TraceParent,
					["TenantId"] = deliveryMetadata.TenantId,
					["UserId"] = deliveryMetadata.UserId,
					["ContentType"] = deliveryMetadata.ContentType,
					["SerializerVersion"] = deliveryMetadata.SerializerVersion,
					["MessageVersion"] = deliveryMetadata.MessageVersion,
					["ContractVersion"] = deliveryMetadata.ContractVersion,
				};

			var messageContext = DispatchContextInitializer.CreateFromMetadata(metaDictionary);
			messageContext.MessageId = message.MessageId;

			var scopedDispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
			var result = await scopedDispatcher.DispatchAsync(integrationEvent, messageContext, cancellationToken)
				.ConfigureAwait(false);

			// Check if the dispatch was successful
			if (result is { Succeeded: false })
			{
				var errorMessage = result.ErrorMessage ?? ErrorConstants.MessageDispatchFailed;
				throw new InvalidOperationException(errorMessage);
			}
		}
		catch (Exception ex)
		{
			_telemetryClient?.TrackException(
				ex,
				new Dictionary<string, string>(StringComparer.Ordinal) { { "MessageId", message.MessageId } });

			LogDispatchFailed(message.MessageId, ex);

			throw;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(OutboxEventId.OutboxDispatchFailed, LogLevel.Error, "Failed to dispatch message with ID {MessageId}")]
	private partial void LogDispatchFailed(string messageId, Exception ex);

	[LoggerMessage(OutboxEventId.OutboxDisposingResources, LogLevel.Information, "Disposing OutboxProcessor resources asynchronously.")]
	private partial void LogDisposingResources();

	[LoggerMessage(OutboxEventId.OutboxConsumerNotCompleted, LogLevel.Warning, "Disposing OutboxProcessor but Consumer has not completed.")]
	private partial void LogConsumerNotCompleted();

	[LoggerMessage(OutboxEventId.OutboxConsumerTimeoutDuringDisposal, LogLevel.Warning,
		"Consumer did not complete in time during async disposal.")]
	private partial void LogConsumerTimeoutDuringDisposal(Exception ex);

	[LoggerMessage(OutboxEventId.OutboxErrorDisposingAsyncResources, LogLevel.Error, "Error disposing OutboxProcessor asynchronously.")]
	private partial void LogErrorDisposingAsyncResources(Exception ex);

	[LoggerMessage(OutboxEventId.OutboxProducerIdleExiting, LogLevel.Information,
		"No outbox record found. Dispatcher {DispatcherId} idle and producer exiting.")]
	private partial void LogProducerIdleExiting(string dispatcherId);

	[LoggerMessage(OutboxEventId.OutboxEnqueuingBatchRecords, LogLevel.Information, "Enqueuing {BatchSize} outbox records")]
	private partial void LogEnqueuingBatchRecords(int batchSize);

	[LoggerMessage(OutboxEventId.OutboxProducerCanceled, LogLevel.Debug, "Outbox producer canceled.")]
	private partial void LogOutboxProducerCanceled();

	[LoggerMessage(OutboxEventId.OutboxErrorInProducerLoop, LogLevel.Error, "Error in Outbox ProducerLoopAsync")]
	private partial void LogErrorInProducerLoop(Exception ex);

	[LoggerMessage(OutboxEventId.OutboxProducerCompleted, LogLevel.Information,
		"Outbox Producer has completed execution. Channel marked as complete.")]
	private partial void LogProducerCompleted();

	[LoggerMessage(OutboxEventId.OutboxConsumerExiting, LogLevel.Information, "No more Outbox records. Consumer is exiting.")]
	private partial void LogConsumerExiting();

	[LoggerMessage(OutboxEventId.OutboxConsumerCanceled, LogLevel.Debug, "Consumer canceled normally.")]
	private partial void LogConsumerCanceled();

	[LoggerMessage(OutboxEventId.OutboxErrorInConsumerLoop, LogLevel.Error, "Error in ConsumerLoopAsync")]
	private partial void LogErrorInConsumerLoop(Exception ex);

	[LoggerMessage(OutboxEventId.OutboxProcessingCompleted, LogLevel.Information,
		"Completed Outbox processing, total events processed: {TotalEvents}")]
	private partial void LogOutboxProcessingCompleted(int totalEvents);

	[LoggerMessage(OutboxEventId.DispatchingOutboxRecord, LogLevel.Information,
		"Dispatching OutboxRecord with MessageId {MessageId} from dispatcher {DispatcherId}")]
	private partial void LogDispatchingOutboxRecord(string messageId, string dispatcherId);

	[LoggerMessage(OutboxEventId.SuccessfullyDispatchedOutboxRecord, LogLevel.Information,
		"Successfully dispatched OutboxRecord with MessageId {MessageId} from dispatcher {DispatcherId}")]
	private partial void LogSuccessfullyDispatchedOutboxRecord(string messageId, string dispatcherId);

	[LoggerMessage(OutboxEventId.MarkedOutboxRecordSent, LogLevel.Information,
		"Marked OutboxRecord with MessageId {MessageId} as sent after successful dispatch")]
	private partial void LogMarkedOutboxRecordSent(string messageId);

	[LoggerMessage(OutboxEventId.ErrorDispatchingOutboxRecord, LogLevel.Error,
		"Error dispatching OutboxRecord with MessageId {MessageId} from dispatcher {DispatcherId}")]
	private partial void LogErrorDispatchingOutboxRecord(string messageId, string dispatcherId, Exception ex);

	[LoggerMessage(OutboxEventId.OutboxDisposalRequestedExitingData, LogLevel.Warning,
		"ConsumerLoopAsync: disposal requested, exit Excalibur.Data.")]
	private partial void LogDisposalRequestedExitingData();

	[LoggerMessage(OutboxEventId.OutboxMessageRoutedToDlq, LogLevel.Warning, "Message {MessageId} routed to dead letter queue: {Reason}")]
	private partial void LogMessageRoutedToDlq(string messageId, string reason);

	[LoggerMessage(OutboxEventId.OutboxCircuitBreakerOpen, LogLevel.Warning,
		"Circuit breaker open for transport {TransportName}, message {MessageId} routed to DLQ")]
	private partial void LogCircuitBreakerOpen(string transportName, string messageId);

	[LoggerMessage(OutboxEventId.OutboxRetryWithBackoff, LogLevel.Debug,
		"Message {MessageId} retry attempt {Attempt}, backoff delay {DelayMs}ms")]
	private partial void LogRetryWithBackoff(string messageId, int attempt, double delayMs);

	[LoggerMessage(OutboxEventId.OutboxTransactionalFallback, LogLevel.Warning,
		"TransactionalWhenApplicable requested but store does not support transactions. Falling back to MinimizedWindow behavior for {MessageCount} messages.")]
	private partial void LogTransactionalFallback(int messageCount);
}
