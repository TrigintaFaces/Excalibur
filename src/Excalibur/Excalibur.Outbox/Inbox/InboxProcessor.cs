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
/// Provides the core implementation of the inbox pattern for reliable message processing with exactly-once delivery guarantees. This
/// processor coordinates between message storage, deduplication tracking, and the message dispatching infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// The InboxProcessor implements a producer-consumer pattern where messages are loaded from persistent storage in batches and processed
/// through the message pipeline. It provides configurable batching, parallel processing, retry handling, and comprehensive metrics
/// collection. The processor is designed for high-throughput scenarios while maintaining reliability and observability requirements of
/// enterprise messaging systems.
/// </para>
/// <para>
/// CA1506 suppressed: InboxProcessor is a core coordinator that legitimately orchestrates message processing through multiple subsystems
/// (batching, telemetry, persistence, scheduling, error handling, serialization). High coupling is inherent to its coordination
/// responsibilities and cannot be reduced without fragmenting cohesive functionality.
/// </para>
/// </remarks>
[SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
	Justification = "Core coordinator that legitimately orchestrates message processing through multiple subsystems")]
public sealed partial class InboxProcessor : IInboxProcessor
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

	private readonly InboxOptions _options;
	private readonly Channel<IInboxMessage> _inboxMessages;
	private readonly int _queueCapacity;
	private readonly IInboxStore _inboxStore;
	private readonly IServiceProvider _serviceProvider;
	private readonly IJsonSerializer _serializer;
	private readonly IInternalSerializer? _internalSerializer;
	private readonly ILogger<InboxProcessor> _logger;
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
	/// Initializes a new instance of the <see cref="InboxProcessor" /> class with the specified dependencies and configuration options for
	/// message. processing operations.
	/// </summary>
	/// <param name="options"> Configuration options controlling inbox processing behavior and performance characteristics. </param>
	/// <param name="inboxStore"> The storage service for persistent inbox message operations. </param>
	/// <param name="serviceProvider"> Service provider for dependency injection and handler activation. </param>
	/// <param name="serializer"> JSON serializer for message serialization and deserialization operations. </param>
	/// <param name="logger"> Logger for diagnostic and operational messaging. </param>
	/// <param name="telemetryClient"> Optional Application Insights client for metrics collection. </param>
	/// <param name="internalSerializer"> Optional internal serializer for high-performance binary envelope serialization. </param>
	/// <param name="deadLetterQueue"> Optional dead letter queue for failed messages. Uses NullDeadLetterQueue if not provided. </param>
	/// <param name="circuitBreakerRegistry">
	/// Optional circuit breaker registry for message type resilience. Uses NullTransportCircuitBreakerRegistry if not provided.
	/// </param>
	/// <param name="backoffCalculator"> Optional backoff calculator for retry delays. Uses ExponentialBackoffCalculator if not provided. </param>
	/// <param name="deliveryGuaranteeOptions"> Optional delivery guarantee options. Uses default at-least-once semantics if not provided. </param>
	/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when configuration options are invalid or inconsistent. </exception>
	/// <remarks>
	/// The constructor validates configuration options and initializes internal components including the message queue, batch processing
	/// metrics, and dynamic batch sizing calculator if enabled. Queue capacity must be at least as large as the producer batch size to
	/// ensure proper operation.
	/// </remarks>
	public InboxProcessor(
		IOptions<InboxOptions> options,
		IInboxStore inboxStore,
		IServiceProvider serviceProvider,
		IJsonSerializer serializer,
		ILogger<InboxProcessor> logger,
		TelemetryClient? telemetryClient = null,
		IInternalSerializer? internalSerializer = null,
		IDeadLetterQueue? deadLetterQueue = null,
		ITransportCircuitBreakerRegistry? circuitBreakerRegistry = null,
		IBackoffCalculator? backoffCalculator = null,
		IOptions<DeliveryGuaranteeOptions>? deliveryGuaranteeOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(inboxStore);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;

		if (_options.QueueCapacity < _options.ProducerBatchSize)
		{
			throw new InvalidOperationException(
				Excalibur.Outbox.Resources.InboxProcessor_QueueCapacityCannotBeLessThanProducerBatchSize);
		}

		_inboxStore = inboxStore;
		_serviceProvider = serviceProvider;
		_serializer = serializer;
		_internalSerializer = internalSerializer;
		_logger = logger;
		_telemetryClient = telemetryClient;
		_queueCapacity = _options.QueueCapacity;
		_inboxMessages = Channel.CreateBounded<IInboxMessage>(new BoundedChannelOptions(_options.QueueCapacity)
		{
			FullMode = BoundedChannelFullMode.Wait, SingleReader = false, SingleWriter = false, AllowSynchronousContinuations = false,
		});
		_batchMetrics = new BatchProcessingMetrics($"InboxProcessor.{nameof(BatchProcessingMetrics)}", telemetryClient);

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
	/// Initializes the inbox processor with the specified dispatcher identifier, preparing internal data structures and coordination
	/// mechanisms for message processing operations.
	/// </summary>
	/// <param name="dispatcherId"> Unique identifier for this dispatcher instance used for coordination and tracking. </param>
	/// <exception cref="ArgumentException"> Thrown when dispatcherId is null, empty, or whitespace. </exception>
	/// <remarks>
	/// This method must be called before DispatchPendingMessagesAsync to properly initialize the processor state. It sets up the
	/// instance-aware queue for message deduplication and coordination between producer and consumer operations.
	/// </remarks>
	public void Init(string dispatcherId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherId);

		_dispatcherId = dispatcherId;
		var orderedQueue = new SimpleOrderedSet<string>(_queueCapacity + _options.ProducerBatchSize);
		_idQueue = new InstanceAwareQueue(dispatcherId, orderedQueue);
	}

	/// <summary>
	/// Asynchronously processes all pending messages in the inbox using a producer-consumer pattern, ensuring exactly-once delivery
	/// semantics and proper error handling throughout the operation.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during processing. </param>
	/// <returns>
	/// A task representing the asynchronous operation, containing the total number of messages successfully processed during this execution.
	/// </returns>
	/// <exception cref="ObjectDisposedException"> Thrown if the processor has been disposed. </exception>
	/// <exception cref="InvalidOperationException"> Thrown if Init has not been called before this method. </exception>
	/// <remarks>
	/// This method coordinates producer and consumer tasks where the producer loads messages from storage into an internal queue, and the
	/// consumer processes messages through the dispatch pipeline. Both tasks run concurrently to maximize throughput while maintaining
	/// proper resource utilization and error handling.
	/// </remarks>
	public async Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		if (string.IsNullOrWhiteSpace(_dispatcherId) || _idQueue == null)
		{
			throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, AttemptedToRunWithoutCallingInitFormat,
				nameof(DispatchPendingMessagesAsync), nameof(Init)));
		}

		_producerTask = Task.Factory
			.StartNew(
				() => ProducerLoopAsync(cancellationToken),
				cancellationToken,
				TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
				TaskScheduler.Default)
			.Unwrap();
		_consumerTask = Task.Factory
			.StartNew(
				() => ConsumerLoopAsync(cancellationToken),
				cancellationToken,
				TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
				TaskScheduler.Default)
			.Unwrap();

		await _producerTask.ConfigureAwait(false);
		var consumerResult = await _consumerTask.ConfigureAwait(false);

		return consumerResult;
	}

	/// <summary>
	/// Asynchronously releases all resources used by the InboxProcessor, including internal queues, tasks, and any disposable dependencies.
	/// This method ensures proper cleanup and resource release.
	/// </summary>
	/// <returns> A ValueTask representing the asynchronous dispose operation. </returns>
	/// <remarks>
	/// This method implements the async dispose pattern and ensures that all internal resources are properly cleaned up, including stopping
	/// any running producer/consumer tasks and disposing of queue structures and metrics collectors. The method is safe to call multiple times.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCoreAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Checks if the payload data is in binary envelope format.
	/// </summary>
	/// <param name="payload"> The payload data to check. </param>
	/// <returns> True if payload starts with envelope format marker (0x01), false otherwise. </returns>
	private static bool IsEnvelopeFormat(byte[]? payload) =>
		payload is { Length: > 0 } && payload[0] == EnvelopeFormatMarker;

	/// <summary>
	/// Converts an InboxEntry from the store to an IInboxMessage using legacy format.
	/// </summary>
	/// <param name="entry"> The inbox entry to convert. </param>
	/// <returns> An IInboxMessage representing the entry. </returns>
	private static InboxMessage ConvertToInboxMessageLegacy(InboxEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);

		// Convert metadata dictionary to JSON string (using source-generated context for AOT compatibility)
		var metadataJson = entry.Metadata?.Count > 0
			? JsonSerializer.Serialize(entry.Metadata, CoreMessageJsonContext.Default.DictionaryStringObject)
			: "{}";

		// Convert payload byte array to base64 string
		var bodyString = entry.Payload != null
			? Convert.ToBase64String(entry.Payload)
			: string.Empty;

		// Use object initializer syntax to set required properties
		return new InboxMessage
		{
			ExternalMessageId = entry.MessageId,
			MessageType = entry.MessageType,
			MessageMetadata = metadataJson,
			MessageBody = bodyString,
			ReceivedAt = entry.ReceivedAt,
			Attempts = entry.RetryCount,
		};
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
	/// Converts an InboxEntry from the store to an IInboxMessage for processing. Handles both binary envelope format and legacy
	/// JSON/Base64 format.
	/// </summary>
	/// <param name="entry"> The inbox entry to convert. </param>
	/// <returns> An IInboxMessage representing the entry. </returns>
	private InboxMessage ConvertToInboxMessageWithEnvelopeSupport(InboxEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);

		// Check if payload is in binary envelope format and we have a deserializer
		if (_internalSerializer is not null && IsEnvelopeFormat(entry.Payload))
		{
			// Skip the format marker byte and deserialize the envelope
			var envelopeData = entry.Payload.AsSpan(1);
			var envelope = DeserializeEnvelope(envelopeData);

			if (envelope is not null)
			{
				// Convert envelope metadata back to JSON string
				var metadataJson = envelope.Metadata?.Count > 0
					? JsonSerializer.Serialize(envelope.Metadata, CoreMessageJsonContext.Default.DictionaryStringString)
					: "{}";

				// Convert payload to base64 string for IInboxMessage interface
				var bodyString = envelope.Payload.Length > 0
					? Convert.ToBase64String(envelope.Payload)
					: string.Empty;

				return new InboxMessage
				{
					ExternalMessageId = envelope.MessageId.ToString(),
					MessageType = envelope.MessageType ?? entry.MessageType,
					MessageMetadata = metadataJson,
					MessageBody = bodyString,
					ReceivedAt = envelope.ReceivedAt,
					Attempts = entry.RetryCount,
				};
			}
		}

		// Fallback to legacy format conversion
		return ConvertToInboxMessageLegacy(entry);
	}

	/// <summary>
	/// Deserializes an InboxEnvelope from binary format if IInternalSerializer is available.
	/// </summary>
	/// <param name="data"> The serialized envelope data. </param>
	/// <returns> The deserialized envelope, or null if internal serializer is not configured. </returns>
	private InboxEnvelope? DeserializeEnvelope(ReadOnlySpan<byte> data)
	{
		if (_internalSerializer is null)
		{
			return null;
		}

		return _internalSerializer.Deserialize<InboxEnvelope>(data);
	}

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
					LogConsumerTimeout(ex);
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

			_ = _inboxMessages.Writer.TryComplete();

			_batchMetrics?.Dispose();
		}
		catch (Exception ex)
		{
			LogDisposeError(ex);
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
				var availableSlots = Math.Max(0, _queueCapacity - _inboxMessages.Reader.Count);
				var remainingMessages = _options.PerRunTotal > 0 ? _options.PerRunTotal - totalQueued : _options.ProducerBatchSize;
				var batchSize = Math.Min(_options.ProducerBatchSize, remainingMessages);

				if (availableSlots < batchSize)
				{
					// Event-driven wait: block until channel has capacity. This is more efficient than polling with Task.Delay
					if (!await _inboxMessages.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
					{
						// Channel completed, stop producing
						break;
					}

					continue;
				}

				var batch = await ReserveBatchRecordsAsync(batchSize, cancellationToken).ConfigureAwait(false);

				if (batch.Count == 0)
				{
					LogNoInboxRecord(_dispatcherId);

					break;
				}

				LogEnqueuingBatch(batch.Count);

				foreach (var inboxRecord in batch)
				{
					if (await _idQueue.AddAsync(inboxRecord.MessageId, cancellationToken).ConfigureAwait(false))
					{
						var inboxMessage = ConvertToInboxMessageWithEnvelopeSupport(inboxRecord);
						await _inboxMessages.Writer.WriteAsync(inboxMessage, cancellationToken).ConfigureAwait(false);
						totalQueued++;
					}
				}

				reachedLimit = _options.PerRunTotal > 0 && totalQueued >= _options.PerRunTotal;

				_telemetryClient?.GetMetric("Inbox.BatchSize").TrackValue(batch.Count);
			}
		}
		catch (OperationCanceledException)
		{
			LogProducerCanceled();
		}
		catch (Exception ex)
		{
			LogProducerError(ex);

			throw;
		}
		finally
		{
			_producerStopped = true;
			_inboxMessages.Writer.Complete();

			LogProducerCompleted();
		}
	}

	[UnconditionalSuppressMessage(
		"ReflectionAnalysis",
		"IL2026:RequiresUnreferencedCode",
		Justification =
			"Inbox processing uses runtime type resolution from MessageTypeRegistry; reflection is intentional.")]
	private async Task<int> ConsumerLoopAsync(CancellationToken cancellationToken)
	{
		var totalProcessedCount = 0;

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (_disposedFlag == 1)
				{
					LogConsumerDisposalRequested();
					break;
				}

				if (_producerStopped && _inboxMessages.Reader.Count == 0)
				{
					LogConsumerExiting();

					break;
				}

				var batchSize = _batchSizeCalculator?.CurrentBatchSize ?? _options.ConsumerBatchSize;
				var batch = await ChannelBatchUtilities.DequeueBatchAsync(_inboxMessages.Reader, batchSize, cancellationToken)
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
						await DispatchSingleMessageAsync(record, cancellationToken).ConfigureAwait(false);
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
					new Dictionary<string, object?>(StringComparer.Ordinal) { ["ProcessorType"] = "Inbox" });

				_telemetryClient?.TrackMetric("InboxMessagesDispatched", totalProcessedCount);
			}
		}
		catch (OperationCanceledException)
		{
			LogConsumerCanceled();
		}
		catch (Exception ex)
		{
			LogConsumerError(ex);

			throw;
		}

		LogProcessingComplete(totalProcessedCount);

		_telemetryClient?.GetMetric("Inbox.RecordsProcessed").TrackValue(totalProcessedCount);

		return totalProcessedCount;
	}

	private async Task<IReadOnlyCollection<InboxEntry>> ReserveBatchRecordsAsync(int batchSize, CancellationToken cancellationToken)
	{
		// Get failed entries for retry processing (using available interface method)
		var records = await _inboxStore
			.GetFailedEntriesAsync(3, DateTimeOffset.UtcNow.AddMinutes(-5), batchSize, cancellationToken)
			.ConfigureAwait(false);

		return records.ToList().AsReadOnly();
	}

	[RequiresUnreferencedCode("Uses DeserializeAsync with runtime type resolution from MessageTypeRegistry")]
	private async Task<int> ProcessBatchParallelAsync(IInboxMessage[] batch, CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		// Track (messageId, handlerType) pairs for composite key operations
		var successfulMessages = new ConcurrentBag<(string MessageId, string HandlerType)>();
		var failedToRetryMessages = new ConcurrentBag<(string MessageId, string HandlerType)>();
		var failedToDeadLetter =
			new ConcurrentBag<(string MessageId, IInboxMessage Message, Exception? Exception, DeadLetterReason Reason)>();

		// Process messages in parallel
		var (successful, failed) = await Batching.ProcessBatchAsync(
			batch,
			async (message, ct) =>
			{
				var attempt = message.Attempts + 1;

				// Get circuit breaker for the message type
				var circuitBreaker = _circuitBreakerRegistry.GetOrCreate(message.MessageType);

				// Check if circuit breaker is open - route directly to DLQ
				if (circuitBreaker.State == CircuitState.Open)
				{
					LogCircuitBreakerOpen(message.MessageType, message.ExternalMessageId);
					failedToDeadLetter.Add((message.ExternalMessageId, message, null, DeadLetterReason.CircuitBreakerOpen));

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

					successfulMessages.Add((message.ExternalMessageId, message.MessageType));
				}
				catch (CircuitBreakerOpenException)
				{
					// Circuit opened during execution
					LogCircuitBreakerOpen(message.MessageType, message.ExternalMessageId);
					failedToDeadLetter.Add((message.ExternalMessageId, message, null, DeadLetterReason.CircuitBreakerOpen));
				}
				catch (Exception ex)
				{
					// Record failure for circuit breaker
					circuitBreaker.RecordFailure(ex);

					LogDispatchError(message.ExternalMessageId, _dispatcherId, ex);

					if (attempt >= _options.MaxAttempts)
					{
						failedToDeadLetter.Add((message.ExternalMessageId, message, ex, DeadLetterReason.MaxRetriesExceeded));
					}
					else if (_deliveryGuaranteeOptions.EnableAutomaticRetry)
					{
						// Calculate backoff delay for logging
						var backoffDelay = _backoffCalculator.CalculateDelay(attempt);
						LogRetryWithBackoff(message.ExternalMessageId, attempt, backoffDelay.TotalMilliseconds);

						failedToRetryMessages.Add((message.ExternalMessageId, message.MessageType));
					}
					else
					{
						failedToRetryMessages.Add((message.ExternalMessageId, message.MessageType));
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

		// Perform batch database operations if enabled
		if (_options.EnableBatchDatabaseOperations)
		{
			await PerformBatchDatabaseOperationsAsync(successfulMessages.ToList(), failedToRetryMessages.ToList(), [], cancellationToken)
				.ConfigureAwait(false);
		}
		else
		{
			// Fall back to individual operations
			foreach (var (messageId, handlerType) in successfulMessages)
			{
				await _inboxStore.MarkProcessedAsync(messageId, handlerType, cancellationToken).ConfigureAwait(false);
			}

			foreach (var (messageId, handlerType) in failedToRetryMessages)
			{
				await _inboxStore.MarkFailedAsync(messageId, handlerType, ErrorConstants.ProcessingFailedRetryAttempt, cancellationToken)
					.ConfigureAwait(false);
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
					["ProcessorType"] = "Inbox",
					["ParallelDegree"] = _options.ParallelProcessingDegree,
					["BatchOperationsEnabled"] = _options.EnableBatchDatabaseOperations,
				});

		return successful.Count;
	}

	private async Task PerformBatchDatabaseOperationsAsync(
		List<(string MessageId, string HandlerType)> successfulMessages,
		List<(string MessageId, string HandlerType)> failedToRetry,
		List<(string MessageId, string HandlerType)> failedToDeadLetter,
		CancellationToken cancellationToken)
	{
		var tasks = new List<Task>();

		if (successfulMessages.Count > 0)
		{
			// Process successful messages individually since batch operations are not available
			foreach (var (messageId, handlerType) in successfulMessages)
			{
				tasks.Add(_inboxStore.MarkProcessedAsync(messageId, handlerType, cancellationToken).AsTask());
			}
		}

		if (failedToRetry.Count > 0)
		{
			// Process failed-to-retry messages individually
			foreach (var (messageId, handlerType) in failedToRetry)
			{
				tasks.Add(_inboxStore
					.MarkFailedAsync(messageId, handlerType, ErrorConstants.ProcessingFailedRetryAttempt, cancellationToken).AsTask());
			}
		}

		if (failedToDeadLetter.Count > 0)
		{
			// Process dead letter messages individually
			foreach (var (messageId, handlerType) in failedToDeadLetter)
			{
				tasks.Add(_inboxStore.MarkFailedAsync(messageId, handlerType,
					ErrorConstants.MessageMovedToDeadLetterQueueMaxRetriesExceeded,
					cancellationToken).AsTask());
			}
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[RequiresUnreferencedCode("Uses DeserializeAsync with runtime type resolution from MessageTypeRegistry")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Inbox dispatch uses runtime deserialization for stored message payloads.")]
	private async Task DispatchSingleMessageAsync(IInboxMessage message, CancellationToken cancellationToken)
	{
		LogDispatchingMessage(message.ExternalMessageId, _dispatcherId);
		await DispatchAsync(message, cancellationToken).ConfigureAwait(false);
		LogDispatchSuccess(message.ExternalMessageId, _dispatcherId);
	}

	[RequiresUnreferencedCode("Uses DeserializeAsync with runtime type resolution from MessageTypeRegistry")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer.DeserializeAsync(String, Type)")]
	private async Task DispatchAsync(IInboxMessage storedMessage, CancellationToken cancellationToken)
	{
		var type = MessageTypeRegistry.GetType(storedMessage.MessageType)
		           ?? throw new TypeLoadException($"{ErrorConstants.TypeNotFoundInRegistry}: {storedMessage.MessageType}");
		if (await _serializer.DeserializeAsync(storedMessage.MessageBody, type).ConfigureAwait(false) is not IDispatchMessage
		    dispatchMessage)
		{
			throw new InvalidOperationException(
				$"{ErrorConstants.CouldNotDeserializeAsDispatchMessage}: {storedMessage.MessageType}");
		}

		var deserializedMetadata =
			await _serializer.DeserializeAsync(storedMessage.MessageMetadata, typeof(DeliveryMetadata)).ConfigureAwait(false);
		if (deserializedMetadata is not DeliveryMetadata meta)
		{
			throw new InvalidOperationException(
				$"{ErrorConstants.FailedToDeserializeMessageMetadata}: {storedMessage.ExternalMessageId}");
		}

		var metaDict = new Dictionary<string, string?>
			(StringComparer.Ordinal)
			{
				["CorrelationId"] = meta.CorrelationId,
				["CausationId"] = meta.CausationId,
				["TraceParent"] = meta.TraceParent,
				["TenantId"] = meta.TenantId,
				["UserId"] = meta.UserId,
				["ContentType"] = meta.ContentType,
				["SerializerVersion"] = meta.SerializerVersion,
				["MessageVersion"] = meta.MessageVersion,
				["ContractVersion"] = meta.ContractVersion,
			};

		using var scope = _serviceProvider.CreateScope();
		var context = DispatchContextInitializer.CreateFromMetadata(metaDict);
		context.MessageId = storedMessage.ExternalMessageId;
		context.ExternalId = storedMessage.ExternalMessageId;

		var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
		var result = await dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken).ConfigureAwait(false);

		// Check if the dispatch was successful
		if (result is { Succeeded: false })
		{
			var errorMessage = result.ErrorMessage ?? ErrorConstants.MessageDispatchFailed;
			throw new InvalidOperationException(errorMessage);
		}
	}

	/// <summary>
	/// Routes a failed message to the dead letter queue.
	/// </summary>
	private async Task RouteToDeadLetterQueueAsync(
		IInboxMessage message,
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

		LogMessageRoutedToDlq(message.ExternalMessageId, reasonText);

		var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["MessageType"] = message.MessageType,
			["DispatcherId"] = _dispatcherId ?? string.Empty,
			["Attempts"] = message.Attempts.ToString(CultureInfo.InvariantCulture),
			["ReceivedAt"] = message.ReceivedAt.ToString("O", CultureInfo.InvariantCulture),
		};

		_ = await _deadLetterQueue.EnqueueAsync(
			message,
			reason,
			cancellationToken,
			exception,
			metadata).ConfigureAwait(false);

		// Mark the message as moved to DLQ in the inbox store
		await _inboxStore.MarkFailedAsync(
			message.ExternalMessageId,
			message.MessageType,
			$"Moved to DLQ: {reasonText}",
			cancellationToken).ConfigureAwait(false);
	}

	// Source-generated logging methods
	[LoggerMessage(OutboxEventId.InboxNoRecord, LogLevel.Information,
		"No inbox record found. Dispatcher {DispatcherId} idle and producer exiting.")]
	private partial void LogNoInboxRecord(string dispatcherId);

	[LoggerMessage(OutboxEventId.InboxEnqueuingBatch, LogLevel.Information, "Enqueuing {BatchSize} inbox records")]
	private partial void LogEnqueuingBatch(int batchSize);

	[LoggerMessage(OutboxEventId.InboxProducerCanceled, LogLevel.Debug, "Inbox producer canceled.")]
	private partial void LogProducerCanceled();

	[LoggerMessage(OutboxEventId.InboxProducerError, LogLevel.Error, "Error in Inbox ProducerLoopAsync")]
	private partial void LogProducerError(Exception ex);

	[LoggerMessage(OutboxEventId.InboxProducerCompleted, LogLevel.Information,
		"Inbox Producer has completed execution. Channel marked as complete.")]
	private partial void LogProducerCompleted();

	[LoggerMessage(OutboxEventId.InboxConsumerDisposalRequested, LogLevel.Warning,
		"ConsumerLoopAsync: disposal requested, exit Excalibur.Data.")]
	private partial void LogConsumerDisposalRequested();

	[LoggerMessage(OutboxEventId.InboxConsumerExiting, LogLevel.Information, "No more Inbox records. Consumer is exiting.")]
	private partial void LogConsumerExiting();

	[LoggerMessage(OutboxEventId.InboxConsumerCanceled, LogLevel.Debug, "Consumer canceled normally.")]
	private partial void LogConsumerCanceled();

	[LoggerMessage(OutboxEventId.InboxConsumerError, LogLevel.Error, "Error in ConsumerLoopAsync")]
	private partial void LogConsumerError(Exception ex);

	[LoggerMessage(OutboxEventId.InboxProcessingComplete, LogLevel.Information,
		"Completed Inbox processing, total events processed: {TotalEvents}")]
	private partial void LogProcessingComplete(int totalEvents);

	[LoggerMessage(OutboxEventId.InboxDispatchingMessage, LogLevel.Information,
		"Dispatching Inbox message with MessageId {MessageId} from dispatcher {DispatcherId}")]
	private partial void LogDispatchingMessage(string messageId, string dispatcherId);

	[LoggerMessage(OutboxEventId.InboxDispatchSuccess, LogLevel.Information,
		"Successfully dispatched Inbox message with MessageId {MessageId} from dispatcher {DispatcherId}")]
	private partial void LogDispatchSuccess(string messageId, string dispatcherId);

	[LoggerMessage(OutboxEventId.InboxDispatchError, LogLevel.Error,
		"Error dispatching Inbox message with MessageId {MessageId} from dispatcher {DispatcherId}")]
	private partial void LogDispatchError(string messageId, string dispatcherId, Exception ex);

	[LoggerMessage(OutboxEventId.InboxDisposingResources, LogLevel.Information, "Disposing InboxProcessor resources")]
	private partial void LogDisposingResources();

	[LoggerMessage(OutboxEventId.InboxConsumerNotCompleted, LogLevel.Warning, "Consumer task has not completed during disposal")]
	private partial void LogConsumerNotCompleted();

	[LoggerMessage(OutboxEventId.InboxConsumerTimeout, LogLevel.Error, "Timeout waiting for consumer task to complete during disposal")]
	private partial void LogConsumerTimeout(Exception ex);

	[LoggerMessage(OutboxEventId.InboxDisposeError, LogLevel.Error, "Error occurred during InboxProcessor disposal")]
	private partial void LogDisposeError(Exception ex);

	[LoggerMessage(OutboxEventId.InboxMessageRoutedToDlq, LogLevel.Warning,
		"Inbox message {MessageId} routed to dead letter queue: {Reason}")]
	private partial void LogMessageRoutedToDlq(string messageId, string reason);

	[LoggerMessage(OutboxEventId.InboxCircuitBreakerOpen, LogLevel.Warning,
		"Circuit breaker open for message type {MessageType}, inbox message {MessageId} routed to DLQ")]
	private partial void LogCircuitBreakerOpen(string messageType, string messageId);

	[LoggerMessage(OutboxEventId.InboxRetryWithBackoff, LogLevel.Debug,
		"Inbox message {MessageId} retry attempt {Attempt}, backoff delay {DelayMs}ms")]
	private partial void LogRetryWithBackoff(string messageId, int attempt, double delayMs);
}
