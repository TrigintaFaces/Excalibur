// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

using Excalibur.Dispatch.Delivery.BatchProcessing;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DeliveryMetadata = Excalibur.Dispatch.Messaging.MessageMetadata;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Provides the core implementation of the inbox pattern for reliable message processing with effectively-once processing. Delivery
/// remains at-least-once and handlers must be idempotent. This
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
	private readonly Channel<InboxWorkItem> _inboxMessages;
	private readonly int _queueCapacity;

	// Constant metric tag set — hoisted to avoid allocating a new dictionary on every batch completion.
	private static readonly Dictionary<string, object?> ProcessorTypeTags =
		new(StringComparer.Ordinal) { ["ProcessorType"] = "Inbox" };

	// Per-instance metric tag set (option-derived, stable for the processor's lifetime), built once in the
	// constructor rather than per batch. Treated as read-only by the metrics recorder.
	private readonly Dictionary<string, object?> _batchCompletionTags;
	private readonly IInboxStore _inboxStore;

	// Non-null only when the store declares the lease protocol. This is the drain's ONLY cross-instance
	// fence: the read that produced the batch takes no term, so without this a second processor selects
	// and dispatches the same entries. Null means the drain is unfenced and the single-instance consumer
	// obligation applies.
	private readonly ILeasedInboxStore? _leasedStore;
	private readonly IServiceProvider _serviceProvider;
	private readonly DispatchJsonSerializer _serializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly IBinaryEnvelopeDeserializer? _envelopeDeserializer;
	private readonly ILogger<InboxProcessor> _logger;
	private readonly BatchProcessingMetrics _batchMetrics;
	private readonly DynamicBatchSizeCalculator? _batchSizeCalculator;

	// Deduplication
	private readonly IDeduplicationStore? _deduplicationStore;

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


	/// <summary>
	/// Initializes a new instance of the <see cref="InboxProcessor" /> class with the specified dependencies and configuration options for
	/// message. processing operations.
	/// </summary>
	/// <param name="options"> Configuration options controlling inbox processing behavior and performance characteristics. </param>
	/// <param name="inboxStore"> The storage service for persistent inbox message operations. </param>
	/// <param name="serviceProvider"> Service provider for dependency injection and handler activation. </param>
	/// <param name="serializer"> JSON serializer for message serialization and deserialization operations. </param>
	/// <param name="logger"> Logger for diagnostic and operational messaging. </param>
	/// <param name="envelopeDeserializer"> Optional binary envelope deserializer for high-performance binary envelope support. </param>
	/// <param name="deadLetterQueue"> Optional dead letter queue for failed messages. Uses NullDeadLetterQueue if not provided. </param>
	/// <param name="circuitBreakerRegistry">
	/// Optional circuit breaker registry for message type resilience. Uses NullTransportCircuitBreakerRegistry if not provided.
	/// </param>
	/// <param name="backoffCalculator"> Optional backoff calculator for retry delays. Uses ExponentialBackoffCalculator if not provided. </param>
	/// <param name="deliveryGuaranteeOptions"> Optional delivery guarantee options. Uses default at-least-once semantics if not provided. </param>
	/// <param name="deduplicationStore"> Optional deduplication store for exactly-once message processing. When provided, duplicate messages are skipped. </param>
	/// <param name="payloadSerializer">
	/// Optional pluggable payload serializer. Must be the same one the inbox writes through: a payload it
	/// wrote names its serializer in a leading magic byte and is read back through that serializer, so a
	/// binary format such as MessagePack or Protobuf drains as written rather than being handed to the JSON
	/// reader. Entries written before one was configured are raw JSON and keep the JSON path.
	/// </param>
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
		DispatchJsonSerializer serializer,
		ILogger<InboxProcessor> logger,
		IBinaryEnvelopeDeserializer? envelopeDeserializer = null,
		IDeadLetterQueue? deadLetterQueue = null,
		ITransportCircuitBreakerRegistry? circuitBreakerRegistry = null,
		IBackoffCalculator? backoffCalculator = null,
		IOptions<DeliveryGuaranteeOptions>? deliveryGuaranteeOptions = null,
		IDeduplicationStore? deduplicationStore = null,
		IPayloadSerializer? payloadSerializer = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(inboxStore);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;

		_batchCompletionTags = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["ProcessorType"] = "Inbox",
			["ParallelDegree"] = _options.Capacity.ParallelProcessingDegree,
			["BatchOperationsEnabled"] = _options.BatchTuning.EnableBatchDatabaseOperations,
		};

		if (_options.Capacity.QueueCapacity < _options.Capacity.ProducerBatchSize)
		{
			throw new InvalidOperationException(
				Excalibur.Outbox.Resources.InboxProcessor_QueueCapacityCannotBeLessThanProducerBatchSize);
		}

		_inboxStore = inboxStore;

		// The drain's cross-instance fence, when the store offers one. Probed through the EFFECTIVE
		// capability rather than a bare type check, for the same reason the backoff probe is: a decorator
		// can implement ILeasedInboxStore in order to forward it while wrapping an inner store that cannot
		// lease. Selecting such a decorator here would report a lease that no store ever took, which is
		// worse than taking none -- an unfenced drain that believes it is fenced.
		var supportsLeasedClaim = inboxStore is IInboxStoreCapabilities leaseCapabilities
			? leaseCapabilities.SupportsLeasedClaim
			: inboxStore is ILeasedInboxStore;

		_leasedStore = supportsLeasedClaim ? inboxStore as ILeasedInboxStore : null;
		_serviceProvider = serviceProvider;
		_serializer = serializer;
		_payloadSerializer = payloadSerializer;
		_envelopeDeserializer = envelopeDeserializer;
		_logger = logger;
		_queueCapacity = _options.Capacity.QueueCapacity;
		_inboxMessages = Channel.CreateBounded<InboxWorkItem>(new BoundedChannelOptions(_options.Capacity.QueueCapacity)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false,
			AllowSynchronousContinuations = false,
		});
		_batchMetrics = new BatchProcessingMetrics($"InboxProcessor.{nameof(BatchProcessingMetrics)}");

		// Initialize deduplication (opt-in)
		_deduplicationStore = deduplicationStore;

		// Initialize resilience components -- warn when using silent no-op fallbacks
		_deadLetterQueue = deadLetterQueue ?? NullDeadLetterQueue.Instance;
		_circuitBreakerRegistry = circuitBreakerRegistry ?? NullTransportCircuitBreakerRegistry.Instance;

		if (deadLetterQueue is null)
		{
			LogDeadLetterQueueNotConfigured();
		}

		if (circuitBreakerRegistry is null)
		{
			LogCircuitBreakerNotConfigured();
		}
		_backoffCalculator = backoffCalculator ?? ExponentialBackoffCalculator.CreateForMessageQueue();
		_deliveryGuaranteeOptions = deliveryGuaranteeOptions?.Value ?? new DeliveryGuaranteeOptions();

		if (_options.BatchTuning.EnableDynamicBatchSizing)
		{
			_batchSizeCalculator = new DynamicBatchSizeCalculator(
				_options.BatchTuning.MinBatchSize,
				_options.BatchTuning.MaxBatchSize,
				_options.Capacity.ConsumerBatchSize);
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
	}

	/// <summary>
	/// Asynchronously processes all pending messages in the inbox using a producer-consumer pattern, ensuring effectively-once
	/// processing (a redelivered message is skipped, not reprocessed) and proper error handling throughout the operation.
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

		if (string.IsNullOrWhiteSpace(_dispatcherId))
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
		await DisposeCoreAsync().ConfigureAwait(false);
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

		// Carry the stored payload through as raw bytes. Encoding it as text here would corrupt any binary
		// payload (a configured payload serializer emits a magic-byte-prefixed binary encoding) and the
		// drain's deserializer reads the bytes directly.
		var body = entry.Payload ?? [];

		// Use object initializer syntax to set required properties
		return new InboxMessage
		{
			ExternalMessageId = entry.MessageId,
			MessageType = entry.MessageType,
			MessageMetadata = metadataJson,
			MessageBody = body,
			ReceivedAt = entry.ReceivedAt,
			Attempts = entry.RetryCount,
			TenantId = entry.TenantId,
		};
	}

	/// <summary>
	/// Deserializes a stored payload back through the serializer that wrote it.
	/// </summary>
	/// <param name="payload"> The stored payload bytes. </param>
	/// <param name="type"> The resolved message type. </param>
	/// <returns> The deserialized message, or <see langword="null" /> when the serializer yields none. </returns>
	/// <remarks>
	/// A payload written through <see cref="IPayloadSerializer" /> is magic-byte-prefixed and may be binary,
	/// so it is read back through the same facade rather than the JSON reader, which structurally cannot
	/// parse it. Entries written before a payload serializer was configured are raw UTF-8 JSON and are
	/// recognised by their opening brace or bracket, so a store holding both formats drains either way.
	/// </remarks>
	[RequiresUnreferencedCode("Uses DeserializeFromUtf8 with runtime type resolution from MessageTypeRegistry")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Serialization.DispatchJsonSerializer.DeserializeFromUtf8(ReadOnlySpan<Byte>, Type)")]
	private object? DeserializePayload(byte[] payload, Type type) =>
		_payloadSerializer is not null && !IsRawJsonPayload(payload)
			? _payloadSerializer.DeserializeObject(payload, type)
			: _serializer.DeserializeFromUtf8(payload, type);

	/// <summary>
	/// Describes why a stored payload could not be read, without echoing the payload itself.
	/// </summary>
	/// <param name="payload"> The stored payload bytes. </param>
	/// <returns> A diagnostic sentence naming the likely cause. </returns>
	private string DescribeUnreadablePayload(byte[] payload) =>
		payload is not { Length: > 0 }
			? "The stored payload is empty."
			: $"The stored payload begins 0x{payload[0]:X2} and is {payload.Length} bytes; it was read back through "
			  + (IsRawJsonPayload(payload) || _payloadSerializer is null
				  ? "the JSON reader. Either the bytes are not UTF-8 JSON, or they were written by a payload "
					+ "serializer that is no longer registered on this host."
				  : "the configured payload serializer, which could not read them. Either the serializer that "
					+ "wrote the entry is no longer registered on this host, or the payload is corrupt.");

	/// <summary>
	/// Reports whether a stored payload is raw UTF-8 JSON rather than a magic-byte-prefixed payload.
	/// </summary>
	/// <param name="payload"> The stored payload bytes. </param>
	/// <returns> <see langword="true" /> when the payload opens a JSON object or array. </returns>
	private static bool IsRawJsonPayload(byte[] payload) => payload is [(byte)'{' or (byte)'[', ..];

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
		if (_envelopeDeserializer is not null && IsEnvelopeFormat(entry.Payload))
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

				// Carry the envelope payload through as raw bytes, for the same reason as the legacy path.
				var body = envelope.Payload;

				return new InboxMessage
				{
					ExternalMessageId = envelope.MessageId.ToString(),
					MessageType = envelope.MessageType ?? entry.MessageType,
					MessageMetadata = metadataJson,
					MessageBody = body,
					ReceivedAt = envelope.Timestamp,
					Attempts = entry.RetryCount,
					TenantId = entry.TenantId,
				};
			}
		}

		// Fallback to legacy format conversion
		return ConvertToInboxMessageLegacy(entry);
	}

	/// <summary>
	/// Deserializes an inbox envelope from binary format using the registered envelope deserializer.
	/// </summary>
	/// <param name="data"> The serialized envelope data. </param>
	/// <returns> The deserialized envelope data, or null if no envelope deserializer is configured. </returns>
	private EnvelopeData? DeserializeEnvelope(ReadOnlySpan<byte> data)
	{
		return _envelopeDeserializer?.DeserializeInboxEnvelope(data);
	}

	private async ValueTask DisposeCoreAsync()
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

			// Guards against queueing the same unit of work twice within this run. The unit of work is the
			// composite (MessageId, HandlerType) — the pair the work item carries and every completion path
			// marks — because one message can have an entry per handler. Keyed on MessageId alone, only the
			// first handler's entry was queued per run and the rest waited for a later one.
			//
			// This set is per-processor and in-process: it excludes a duplicate WITHIN one run and nothing
			// wider. It is not a cross-instance fence and must not be read as one -- there is no store-side
			// reservation behind it. The drain's read is a plain query that takes no term on the rows it
			// returns, so a second processor selects the same entries and dispatches them too. That is the
			// drain's single-in-flight gap, stated in the package's architecture notes.
			var queuedThisRun = new HashSet<(string MessageId, string HandlerType)>();

			while (!cancellationToken.IsCancellationRequested && !reachedLimit)
			{
				var availableSlots = Math.Max(0, _queueCapacity - _inboxMessages.Reader.Count);
				var remainingMessages = _options.Capacity.PerRunTotal > 0 ? _options.Capacity.PerRunTotal - totalQueued : _options.Capacity.ProducerBatchSize;
				var batchSize = Math.Min(_options.Capacity.ProducerBatchSize, remainingMessages);

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

				var batch = await ReadRetryableEntriesAsync(batchSize, cancellationToken).ConfigureAwait(false);

				if (batch.Count == 0)
				{
					LogNoInboxRecord(_dispatcherId!);

					break;
				}

				LogEnqueuingBatch(batch.Count);

				foreach (var inboxRecord in batch)
				{
					if (queuedThisRun.Add((inboxRecord.MessageId, inboxRecord.HandlerType)))
					{
						var inboxMessage = ConvertToInboxMessageWithEnvelopeSupport(inboxRecord);
						var workItem = new InboxWorkItem(inboxMessage, inboxRecord.MessageId, inboxRecord.HandlerType);
						await _inboxMessages.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);
						totalQueued++;
					}
				}

				reachedLimit = _options.Capacity.PerRunTotal > 0 && totalQueued >= _options.Capacity.PerRunTotal;

				BackgroundServiceMetrics.RecordMessagesProcessed(BackgroundServiceTypes.Inbox, BackgroundServiceOperations.Pending, batch.Count);
			}
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
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

				var batchSize = _batchSizeCalculator?.CurrentBatchSize ?? _options.Capacity.ConsumerBatchSize;
				var batch = await ChannelBatchUtilities.DequeueBatchAsync(_inboxMessages.Reader, batchSize, cancellationToken)
					.ConfigureAwait(false);

				if (batch.Length == 0)
				{
					continue;
				}

				var stopwatch = ValueStopwatch.StartNew();

				// One drain path, whatever the parallel degree. A degree of 1 is the degree-1 case of the
				// batch path, not a separate algorithm: Batching.ProcessBatchAsync at degree 1 is a
				// sequential loop. The hand-rolled sequential branch this replaces dispatched the handler
				// and then recorded NOTHING -- no processed mark, no failure mark, no attempt, no
				// dead-letter, and no try -- so the entry stayed re-admittable and its handler ran again on
				// every pass, unbounded. It was reachable by default (the degree defaults to 1). Two
				// implementations of one invariant is what let them drift; there is now one.
				var processedCount = await ProcessBatchParallelAsync(batch, cancellationToken).ConfigureAwait(false);
				totalProcessedCount += processedCount;

				// Record batch metrics
				var duration = stopwatch.Elapsed;
				_batchMetrics.RecordBatchCompleted(
					batch.Length,
					batch.Length, // Will be updated with actual success/failure counts
					0,
					duration,
					ProcessorTypeTags);

				BackgroundServiceMetrics.RecordMessagesProcessed(BackgroundServiceTypes.Inbox, BackgroundServiceOperations.Dispatch, totalProcessedCount);
			}
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			LogConsumerCanceled();
		}
		catch (Exception ex)
		{
			LogConsumerError(ex);

			throw;
		}

		LogProcessingComplete(totalProcessedCount);

		BackgroundServiceMetrics.RecordProcessingCycle(BackgroundServiceTypes.Inbox, totalProcessedCount > 0 ? BackgroundServiceResults.Success : BackgroundServiceResults.Empty);

		return totalProcessedCount;
	}

	/// <summary>
	/// Reads the next batch of retry-eligible entries. This is a <b>plain query</b>: it takes no ownership
	/// term on the rows it returns and writes nothing.
	/// </summary>
	/// <remarks>
	/// The name says read, not reserve, because nothing here reserves. An earlier name promised a
	/// reservation this method never performed, which is the kind of claim a second drain relies on and
	/// does not get: two processors issuing this read select the same entries and both dispatch them.
	/// </remarks>
	private async Task<IReadOnlyCollection<InboxEntry>> ReadRetryableEntriesAsync(int batchSize, CancellationToken cancellationToken)
	{
		// Get failed entries for retry processing via admin interface.
		// The re-fetch ceiling MUST equal the dead-letter ceiling (_options.MaxAttempts, see the
		// attempt >= _options.MaxAttempts dead-letter branch); a hardcoded lower value would strand
		// entries that are excluded from re-fetch yet still below the dead-letter threshold.
		var admin = (IInboxStoreAdmin)_inboxStore;

		// Re-admission throttle (SA Option C — two-layer): the PRIMARY throttle is per-entry
		// NextAttemptAt persisted by IBackoffSchedulableInboxStore (real exponential backoff). This
		// olderThan value is the small always-on FAIL-SAFE floor -- the BASE retry delay, NOT a magic
		// 5-min window -- so "immediate re-admit" is structurally inexpressible for ANY store/decorator
		// (no tight loop), while being <= the smallest backoff step so it never dominates a sub-5-min step.
		var reAdmissionFloor = DateTimeOffset.UtcNow - _backoffCalculator.CalculateDelay(1);
		var records = await admin
			.GetAllTenantsFailedEntriesAsync(_options.MaxAttempts, reAdmissionFloor, batchSize, cancellationToken)
			.ConfigureAwait(false);

		return records.ToList().AsReadOnly();
	}

	/// <summary>
	/// Checks if a message is a duplicate using the deduplication store (if configured).
	/// Returns true if the message should be skipped.
	/// </summary>
	private async Task<bool> IsDuplicateAsync(string messageId, CancellationToken cancellationToken)
	{
		if (_deduplicationStore is null)
		{
			return false;
		}

		var isDuplicate = await _deduplicationStore.ContainsAsync(messageId, cancellationToken).ConfigureAwait(false);
		if (isDuplicate)
		{
			LogDuplicateDetected(messageId);
		}

		return isDuplicate;
	}

	/// <summary>
	/// Marks a message as processed in the deduplication store (if configured).
	/// </summary>
	private async Task MarkDeduplicatedAsync(string messageId, CancellationToken cancellationToken)
	{
		if (_deduplicationStore is not null)
		{
			await _deduplicationStore.AddAsync(messageId, null, cancellationToken).ConfigureAwait(false);
		}
	}

	[RequiresUnreferencedCode("Uses DeserializeAsync with runtime type resolution from MessageTypeRegistry")]
	private async Task<int> ProcessBatchParallelAsync(InboxWorkItem[] batch, CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();

		// Every store write for an entry now happens INSIDE that entry's own tenant scope, in the same
		// lambda invocation that dispatched it. Collecting outcomes into bags and draining them after the
		// loop put every mark outside the scope its dispatch ran under: on a multi-tenant host the ambient
		// tenant was unresolved by then, the store failed closed, and the batch was discarded with the
		// handlers already run -- on every pass. Keeping the mark next to the dispatch makes that
		// structural rather than a rule someone has to remember.
		var (successful, failed) = await Batching.ProcessBatchAsync(
			batch,
			async (workItem, ct) =>
			{
				var message = workItem.Message;
				var storeMessageId = workItem.MessageId;
				var handlerType = workItem.HandlerType;

				// The entry's tenant, read back off its row, so it goes through the total store-read
				// conversion. A raw null CLEARS the ambient, and a cleared ambient means "no tenant was
				// established" -- which a multi-tenant store fails closed on. An untenanted row is a
				// different state and binds the reserved untenanted term.
				using var tenantScope = TenantContextHolder.BeginScope(
					KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId);

				// THE DRAIN'S CROSS-INSTANCE FENCE.
				//
				// The read that produced this batch is a plain query: it took no term on these rows, so two
				// concurrently running processors hold the same entry here. The lease acquisition below is
				// what separates them, and it is the ONLY thing that does -- a single atomic compare-and-set
				// inside the store, evaluated against the store's own clock. Exactly one caller moves the
				// entry Failed -> Processing and receives a term; every other caller receives null and must
				// leave the entry alone. Neither a registration lifetime nor the in-process set above can
				// substitute for it: both fence one container and this race crosses hosts.
				//
				// A store that does not declare the lease protocol leaves the drain unfenced, and the
				// single-instance consumer obligation is what covers it. That is a real, documented gap --
				// not something this method may paper over by pretending it claimed the entry.
				LeaseToken? lease = null;

				if (_leasedStore is not null)
				{
					lease = await _leasedStore
						.TryAcquireLeaseAsync(storeMessageId, handlerType, _options.BatchTuning.BatchProcessingTimeout, ct)
						.ConfigureAwait(false);

					if (lease is null)
					{
						// Another processor holds a live term, or the entry reached terminal Processed between
						// the read and here. Either way this entry is not ours: dispatching it now is the
						// duplicate invocation the fence exists to prevent.
						LogDrainEntryHeldElsewhere(storeMessageId, handlerType);
						return;
					}
				}

				if (await IsDuplicateAsync(message.ExternalMessageId, ct).ConfigureAwait(false))
				{
					await FinalizeProcessedAsync(storeMessageId, handlerType, lease, ct).ConfigureAwait(false);
					return;
				}

				var attempt = message.Attempts + 1;
				var circuitBreaker = _circuitBreakerRegistry.GetOrCreate(message.MessageType);

				// Circuit breaker open is a TRANSIENT short-circuit, not a delivery failure: the message
				// never reached the handler, so it must not consume an attempt.
				if (circuitBreaker.State == CircuitState.Open)
				{
					LogCircuitBreakerOpen(message.MessageType, message.ExternalMessageId);
					await LeaveForRetryWithoutConsumingAnAttemptAsync(
						storeMessageId, handlerType, message.Attempts, ct).ConfigureAwait(false);

					return;
				}

				try
				{
					// The breaker records the outcome -- success or failure -- inside ExecuteAsync. Recording
					// it again here would count every delivery failure twice, opening the circuit at half
					// the configured threshold and overriding the breaker's own decision about which
					// exceptions count.
					await circuitBreaker.ExecuteAsync(async token =>
					{
						await DispatchSingleMessageAsync(message, token).ConfigureAwait(false);
						return true;
					}, ct).ConfigureAwait(false);

					await MarkDeduplicatedAsync(message.ExternalMessageId, ct).ConfigureAwait(false);
					await FinalizeProcessedAsync(storeMessageId, handlerType, lease, ct).ConfigureAwait(false);
				}
				catch (CircuitBreakerOpenException)
				{
					// Circuit opened mid-dispatch: transient, same as the pre-check above.
					LogCircuitBreakerOpen(message.MessageType, message.ExternalMessageId);
					await LeaveForRetryWithoutConsumingAnAttemptAsync(
						storeMessageId, handlerType, message.Attempts, ct).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogDispatchError(message.ExternalMessageId, _dispatcherId!, ex);

					if (attempt >= _options.MaxAttempts)
					{
						await RouteToDeadLetterQueueAsync(
							storeMessageId, handlerType, message, DeadLetterReason.MaxRetriesExceeded, ex, ct)
							.ConfigureAwait(false);
					}
					else
					{
						if (_deliveryGuaranteeOptions.EnableAutomaticRetry)
						{
							var backoffDelay = _backoffCalculator.CalculateDelay(attempt);
							LogRetryWithBackoff(message.ExternalMessageId, attempt, backoffDelay.TotalMilliseconds);
						}

						await FinalizeFailedAsync(storeMessageId, handlerType, attempt, ex.Message, ct)
							.ConfigureAwait(false);
					}

					throw; // Re-throw for Batching to track
				}
			},
			_options.Capacity.ParallelProcessingDegree,
			_options.BatchTuning.BatchProcessingTimeout,
			cancellationToken).ConfigureAwait(false);

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
			_batchCompletionTags);

		return successful.Count;
	}

	/// <summary>
	/// Commits the terminal processed transition for an entry, under the entry's own composite key and,
	/// when the drain holds one, under the ownership term it acquired before dispatching.
	/// </summary>
	/// <remarks>
	/// <para>
	/// With a term in hand this goes through the fenced finalize, which the store applies only while that
	/// term is still current. A caller whose lease lapsed presents a term matching no row and is refused;
	/// it must NOT then retry the write unfenced, because a lapsed term means another processor legitimately
	/// reclaimed the entry and an unfenced write would overwrite that processor's record.
	/// </para>
	/// <para>
	/// Without a term -- a store that does not declare the lease protocol -- this is an unfenced write, safe
	/// only while the drain runs single-instance. That is the consumer obligation the package's architecture
	/// notes record against the drain's single-in-flight property.
	/// </para>
	/// </remarks>
	private async Task FinalizeProcessedAsync(
		string messageId, string handlerType, LeaseToken? lease, CancellationToken cancellationToken)
	{
		if (_leasedStore is not null && lease is { } term)
		{
			var finalized = await _leasedStore
				.CompleteAsync(messageId, handlerType, term, cancellationToken).ConfigureAwait(false);

			if (!finalized)
			{
				// The term lapsed and the entry was reclaimed. The handler has already run, so the effect
				// stands; what this drain has lost is the right to record it. Falling back to the unfenced
				// mark here would clobber the reclaiming processor's record, so the loss is logged instead.
				LogDrainFinalizeLostTerm(messageId, handlerType);
			}

			return;
		}

		await _inboxStore.MarkProcessedAsync(messageId, handlerType, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Records a delivery failure against an entry, consuming exactly one attempt.
	/// </summary>
	/// <remarks>
	/// Routed through the backoff-aware mark so a store that schedules its own next attempt still does; the
	/// attempt count is carried explicitly rather than left to a store-side increment, so one drain of an
	/// entry consumes exactly one attempt.
	/// </remarks>
	private async Task FinalizeFailedAsync(
		string messageId, string handlerType, int attempt, string error, CancellationToken cancellationToken)
	{
		await MarkFailedForRetryAsync(messageId, handlerType, attempt, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Leaves an entry re-admittable after a transient short-circuit, WITHOUT consuming an attempt.
	/// </summary>
	/// <remarks>
	/// An open circuit breaker means the handler never ran, so the message must not pay for a failure it did
	/// not cause. The entry is returned to the failed state through the admin overload that sets the retry
	/// count <b>exactly</b>, rather than one that increments it. Leaving the entry in any non-failed state
	/// instead would strand it: the drain's read selects failed rows only, so an entry parked anywhere else
	/// is never selected again.
	/// </remarks>
	private async Task LeaveForRetryWithoutConsumingAnAttemptAsync(
		string messageId, string handlerType, int retryCount, CancellationToken cancellationToken)
	{
		var admin = (IInboxStoreAdmin)_inboxStore;
		await admin.MarkFailedAsync(
				messageId, handlerType, ErrorConstants.ProcessingFailedRetryAttempt, retryCount, cancellationToken)
			.ConfigureAwait(false);
	}

	private Task MarkFailedForRetryAsync(string messageId, string handlerType, int attempt, CancellationToken cancellationToken)
	{
		// If the store persists a per-entry next-attempt time, record now + CalculateDelay(attempt) so the
		// re-admission claim honors exponential backoff (the PRIMARY throttle). Stores that don't support it
		// fall back to the plain failed status (fail-open) and rely on the processor's small always-on
		// fail-safe floor (ReadRetryableEntriesAsync) to avoid a tight loop.
		//
		// Probed via the EFFECTIVE capability so a decorator that declares IBackoffSchedulableInboxStore in
		// order to forward it -- but wraps an inner store that cannot schedule -- is not selected here. A type
		// check ALONE was never sufficient: such a decorator satisfies it, forwards nothing, and performs THIS
		// fallback silently on the processor's behalf. The fail-open is documented as the processor's, taken
		// having observed the store is not schedulable; absorbed inside a decorator it is unobservable, and a
		// host cannot tell an honestly-unthrottled store from one whose backoff is being quietly discarded.
		var supportsBackoffScheduling = _inboxStore is IInboxStoreCapabilities backoffCapabilities
			? backoffCapabilities.SupportsBackoffScheduling
			: _inboxStore is IBackoffSchedulableInboxStore;

		if (supportsBackoffScheduling && _inboxStore is IBackoffSchedulableInboxStore schedulable)
		{
			var nextAttemptAt = DateTimeOffset.UtcNow + _backoffCalculator.CalculateDelay(attempt);
			return schedulable.MarkFailedWithBackoffAsync(
				messageId, handlerType, ErrorConstants.ProcessingFailedRetryAttempt, attempt, nextAttemptAt, cancellationToken).AsTask();
		}

		return _inboxStore.MarkFailedAsync(
			messageId, handlerType, ErrorConstants.ProcessingFailedRetryAttempt, cancellationToken).AsTask();
	}

	[RequiresUnreferencedCode("Uses DeserializeAsync with runtime type resolution from MessageTypeRegistry")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Inbox dispatch uses runtime deserialization for stored message payloads.")]
	private async Task DispatchSingleMessageAsync(IInboxMessage message, CancellationToken cancellationToken)
	{
		LogDispatchingMessage(message.ExternalMessageId, _dispatcherId!);
		await DispatchAsync(message, cancellationToken).ConfigureAwait(false);
		LogDispatchSuccess(message.ExternalMessageId, _dispatcherId!);
	}

	[RequiresUnreferencedCode("Uses DeserializeFromUtf8 with runtime type resolution from MessageTypeRegistry")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Serialization.DispatchJsonSerializer.DeserializeFromUtf8(ReadOnlySpan<Byte>, Type)")]
	private async Task DispatchAsync(IInboxMessage storedMessage, CancellationToken cancellationToken)
	{
		if (!MessageTypeRegistry.TryGetType(storedMessage.MessageType, out var type))
		{
			throw new TypeLoadException($"{ErrorConstants.TypeNotFoundInRegistry}: {storedMessage.MessageType}");
		}

		// Read the stored payload back through the serializer that wrote it, mirroring the outbox drain.
		object? deserializedBody;
		try
		{
			deserializedBody = DeserializePayload(storedMessage.MessageBody, type);
		}
		catch (Exception ex) when (ex is JsonException or SerializationException)
		{
			// Name what the bytes are and which reader was applied, rather than letting a bare parse error
			// consume the retry budget with nothing to diagnose from.
			throw new InvalidOperationException(
				$"{ErrorConstants.CouldNotDeserializeAsDispatchMessage}: {storedMessage.MessageType}. "
				+ $"{DescribeUnreadablePayload(storedMessage.MessageBody)}",
				ex);
		}

		if (deserializedBody is not IDispatchMessage dispatchMessage)
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

		await using var scope = _serviceProvider.CreateAsyncScope();
		// Seed the context directly from the strongly-typed metadata — no per-message dictionary alloc.
		var context = DispatchContextInitializer.CreateFromMetadata(meta);
		context.MessageId = storedMessage.ExternalMessageId;
		context.GetOrCreateIdentityFeature().ExternalId = storedMessage.ExternalMessageId;

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
		string storeMessageId,
		string handlerType,
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

		if (_deadLetterQueue is NullDeadLetterQueue)
		{
			LogMessageDiscardedNoDlq(message.ExternalMessageId, reasonText);
		}
		else
		{
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
		}

		// Mark the message as failed (moved to DLQ or discarded)
		await _inboxStore.MarkFailedAsync(
			storeMessageId,
			handlerType,
			_deadLetterQueue is NullDeadLetterQueue ? $"DISCARDED (no DLQ): {reasonText}" : $"Moved to DLQ: {reasonText}",
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// A drained inbox entry paired with the composite key the store holds it under.
	/// </summary>
	/// <remarks>
	/// The converted <see cref="IInboxMessage"/> cannot identify its own row: it carries the message type
	/// (the short type name) rather than the handler type (the fully qualified name), and on the envelope
	/// path its <c>ExternalMessageId</c> is read back out of the payload. Every store keys an entry by
	/// <c>(MessageId, HandlerType)</c>, so the finalize and retry marks must use the entry's own key,
	/// carried here from the <see cref="InboxEntry"/> the producer read.
	/// </remarks>
	/// <param name="Message">The message handed to the dispatch pipeline.</param>
	/// <param name="MessageId">The entry's message identifier, as the store is keyed.</param>
	/// <param name="HandlerType">The entry's handler type, as the store is keyed.</param>
	private sealed record InboxWorkItem(InboxMessage Message, string MessageId, string HandlerType);

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
		"Circuit breaker open for message type {MessageType}, inbox message {MessageId} left for retry (not dead-lettered)")]
	private partial void LogCircuitBreakerOpen(string messageType, string messageId);

	[LoggerMessage(OutboxEventId.InboxRetryWithBackoff, LogLevel.Debug,
		"Inbox message {MessageId} retry attempt {Attempt}, backoff delay {DelayMs}ms")]
	private partial void LogRetryWithBackoff(string messageId, int attempt, double delayMs);

	[LoggerMessage(OutboxEventId.InboxDuplicateDetected, LogLevel.Information,
		"Duplicate inbox message {MessageId} detected by deduplication store, skipping")]
	private partial void LogDuplicateDetected(string messageId);

	[LoggerMessage(OutboxEventId.InboxDrainEntryHeldElsewhere, LogLevel.Debug,
		"Retry drain skipped inbox entry {MessageId}/{HandlerType}: another processor holds the ownership term")]
	private partial void LogDrainEntryHeldElsewhere(string messageId, string handlerType);

	[LoggerMessage(OutboxEventId.InboxDrainFinalizeLostTerm, LogLevel.Warning,
		"Retry drain could not record the outcome for inbox entry {MessageId}/{HandlerType}: its ownership term lapsed and the entry was reclaimed. The handler already ran, so its effect stands; the entry will be retried by whichever processor now holds it.")]
	private partial void LogDrainFinalizeLostTerm(string messageId, string handlerType);

	[LoggerMessage(OutboxEventId.InboxDeadLetterQueueNotConfigured, LogLevel.Warning,
		"No IDeadLetterQueue registered. Failed inbox messages will be discarded silently. Register a dead letter queue implementation to preserve failed messages for investigation.")]
	private partial void LogDeadLetterQueueNotConfigured();

	[LoggerMessage(OutboxEventId.InboxCircuitBreakerNotConfigured, LogLevel.Warning,
		"No ITransportCircuitBreakerRegistry registered. Transport failures will not trigger circuit breakers. Register AddDispatchResilience() to enable transport protection.")]
	private partial void LogCircuitBreakerNotConfigured();

	[LoggerMessage(OutboxEventId.InboxMessageDiscardedNoDlq, LogLevel.Error,
		"INBOX MESSAGE LOST: Message {MessageId} failed ({Reason}) but no dead letter queue is configured. Message has been discarded permanently. Register an IDeadLetterQueue to prevent message loss.")]
	private partial void LogMessageDiscardedNoDlq(string messageId, string reason);
}
