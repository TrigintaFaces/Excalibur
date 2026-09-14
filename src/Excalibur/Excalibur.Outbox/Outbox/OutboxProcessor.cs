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
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Outbox;
using Excalibur.Outbox.Diagnostics;

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

	private readonly OutboxDeliveryOptions _options;
	private readonly Channel<IOutboxMessage> _outboxMessages;
	private readonly int _queueCapacity;

	// Orchestration layer IOutboxStore for integration event outbox (restored from git history)
	private readonly IOutboxStore _outboxStore;

	private readonly DispatchJsonSerializer _serializer;
	private readonly IBinaryEnvelopeDeserializer? _envelopeDeserializer;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<OutboxProcessor> _logger;
	private readonly BatchProcessingMetrics _batchMetrics;
	private readonly DynamicBatchSizeCalculator? _batchSizeCalculator;

	// Resilience components
	private readonly IDeadLetterQueue _deadLetterQueue;

	private readonly ITransportCircuitBreakerRegistry _circuitBreakerRegistry;
	private readonly IBackoffCalculator _backoffCalculator;
	private readonly DeliveryGuaranteeOptions _deliveryGuaranteeOptions;

	// Optional leader-election gate (Excalibur.Dispatch.ILeaderProcessingGate). When registered,
	// its FencingToken is read once per claim/mark call and threaded through to the store so a superseded
	// leader's claim/mark is rejected by the store's server-side compare-and-swap rather than silently
	// racing a newer leader. Null when no leader election is configured (plain, unfenced claim).
	private readonly Excalibur.Dispatch.ILeaderProcessingGate? _leaderGate;

	// Fencing is DEFAULT-ON for a framework-protected outbox: true exactly when a leader gate is present AND
	// the consumer has NOT asserted single-active-writer ownership (OutboxDeliveryOptions.SingleActiveWriter).
	// Registering a leader election is the multi-instance signal, so its presence fences the drain by default;
	// AsSingleWriter() is the explicit, logged opt-out for a genuinely single-writer topology. When false, the
	// drain runs unfenced (either no leader election, or an explicit single-writer opt-out).
	private readonly bool _fencingActive;

	private long? CurrentFencingToken => _leaderGate?.FencingToken;

	/// <summary>
	/// The store's fencing capability. Non-null exactly when the configured store -- or any store it is
	/// decorated by -- can enforce a leadership high-water mark. Resolved through the capability seam rather
	/// than by casting, because a cast sees only the outermost decorator and would silently report the
	/// capability absent, disabling the split-brain guard on every decorated deployment.
	/// </summary>
	private IFencedOutboxStore? FencedStore => _outboxStore.GetService(typeof(IFencedOutboxStore)) as IFencedOutboxStore;

	/// <summary>
	/// Claims a batch, presenting the leadership token when both a tenure and a fencing-capable store exist.
	/// </summary>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private ValueTask<IEnumerable<OutboundMessage>> ClaimBatchAsync(int batchSize, CancellationToken cancellationToken)
	{
		if (_fencingActive && FencedStore is { } fenced && CurrentFencingToken is { } token)
		{
			return fenced.GetUnsentMessagesAsync(batchSize, token, cancellationToken);
		}

		GuardActiveGateHasFencingToken();
		return _outboxStore.GetUnsentMessagesAsync(batchSize, cancellationToken);
	}

	/// <summary>
	/// Marks a message sent, presenting the leadership token when both a tenure and a fencing-capable store
	/// exist.
	/// </summary>
	private ValueTask MarkSentFencedAsync(string messageId, CancellationToken cancellationToken)
	{
		if (_fencingActive && FencedStore is { } fenced && CurrentFencingToken is { } token)
		{
			return fenced.MarkSentAsync(messageId, token, cancellationToken);
		}

		GuardActiveGateHasFencingToken();
		return _outboxStore.MarkSentAsync(messageId, cancellationToken);
	}

	/// <summary>
	/// Composition guard: a leader gate that is <em>present</em> but yields no fencing token MUST fail closed
	/// on the fenced write path — it must never fall through to the unfenced drain. Draining without a fence
	/// under an active gate is the "looks fenced but isn't" split-brain window a superseded leader exploits.
	/// With a required fencing-token provider a leader always mints a token (<c>&gt;= 1</c>), so reaching this
	/// state under an active gate is a defect; refuse to drain rather than drain unfenced. When no gate is
	/// configured (<c>_leaderGate is null</c>) a null token is the legitimate unfenced path and drains normally.
	/// </summary>
	private void GuardActiveGateHasFencingToken()
	{
		if (_fencingActive && CurrentFencingToken is null)
		{
			throw new OutboxFencingTokenUnavailableException(
				"The outbox leader gate is active but no fencing token is available for the current tenure; " +
				"refusing to drain unfenced. A fenced leader election must present a monotonic fencing token " +
				"(a fencing-token provider is a required dependency). Draining without one would let a " +
				"superseded leader claim and complete messages it no longer owns.");
		}
	}

	// Constant metric tag set — hoisted to avoid allocating a new dictionary on every batch completion.
	private static readonly Dictionary<string, object?> ProcessorTypeTags =
		new(StringComparer.Ordinal) { ["ProcessorType"] = "Outbox" };

	// Per-instance metric tag set (values derive from options; stable for the processor's lifetime), built
	// once in the constructor rather than per batch. Treated as read-only by the metrics recorder.
	private readonly Dictionary<string, object?> _batchCompletionTags;

	private int _disposedFlag;

	private Task? _producerTask;

	private Task<int>? _consumerTask;

	private volatile bool _producerStopped;

	private string? _dispatcherId;


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
	/// <param name="envelopeDeserializer"> Optional binary envelope deserializer for high-performance binary envelope support. </param>
	/// <param name="deadLetterQueue"> Optional dead letter queue for failed messages. Uses NullDeadLetterQueue if not provided. </param>
	/// <param name="circuitBreakerRegistry">
	/// Optional circuit breaker registry for transport resilience. Uses NullTransportCircuitBreakerRegistry if not provided.
	/// </param>
	/// <param name="backoffCalculator">
	/// Optional backoff calculator for retry delays. Uses ExponentialBackoffCalculator.Default if not provided.
	/// </param>
	/// <param name="deliveryGuaranteeOptions"> Optional delivery guarantee options. Uses default at-least-once semantics if not provided. </param>
	/// <param name="leaderGate">
	/// Optional leader-election processing gate. When provided, its fencing token is stamped on every
	/// claim/mark-sent call so a superseded leader's mutations are rejected by the store. Null when no
	/// leader election is configured (plain, unfenced claim).
	/// </param>
	/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when configuration validation fails. </exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public OutboxProcessor(
		IOptions<OutboxDeliveryOptions> options,
		IOutboxStore outboxStore,
		DispatchJsonSerializer serializer,
		IServiceProvider serviceProvider,
		ILogger<OutboxProcessor> logger,
		IBinaryEnvelopeDeserializer? envelopeDeserializer = null,
		IDeadLetterQueue? deadLetterQueue = null,
		ITransportCircuitBreakerRegistry? circuitBreakerRegistry = null,
		IBackoffCalculator? backoffCalculator = null,
		IOptions<DeliveryGuaranteeOptions>? deliveryGuaranteeOptions = null,
		Excalibur.Dispatch.ILeaderProcessingGate? leaderGate = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(outboxStore);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;

		_batchCompletionTags = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["ProcessorType"] = "Outbox",
			["ParallelDegree"] = _options.BatchProcessing.ParallelProcessingDegree,
			["BatchOperationsEnabled"] = _options.EnableBatchDatabaseOperations,
		};

		if (_options.QueueCapacity < _options.ProducerBatchSize)
		{
			throw new InvalidOperationException(
				Excalibur.Outbox.Resources.OutboxProcessor_QueueCapacityCannotBeLessThanProducerBatchSize);
		}

		_outboxStore = outboxStore;
		_serializer = serializer;
		_envelopeDeserializer = envelopeDeserializer;
		_serviceProvider = serviceProvider;
		_logger = logger;
		_queueCapacity = options.Value.QueueCapacity;
		_outboxMessages = Channel.CreateBounded<IOutboxMessage>(new BoundedChannelOptions(options.Value.QueueCapacity)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false,
			AllowSynchronousContinuations = false,
		});
		_batchMetrics = new BatchProcessingMetrics($"OutboxProcessor.{nameof(BatchProcessingMetrics)}");

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
		_leaderGate = leaderGate;

		// Default-ON fencing for a framework-protected outbox: a registered leader election is the
		// multi-instance signal, so the drain is fenced unless the consumer explicitly asserts single-active-
		// writer ownership via AsSingleWriter(). Either unfenced path is logged at startup so the downgrade is
		// observable, never silent.
		_fencingActive = leaderGate is not null && !_options.SingleActiveWriter;

		if (leaderGate is not null && _options.SingleActiveWriter)
		{
			LogOutboxUnfencedBySingleWriterOptOut();
		}
		else if (leaderGate is null)
		{
			LogOutboxRunningUnfenced();
		}

		// Fail closed, never silently at drain time. When fencing is active the consumer expects
		// split-brain protection; a store that cannot enforce a fencing high-water mark cannot provide it. The
		// old contract let such a store accept a token and discard it, so the deployment looked fenced and was
		// not. Refuse to start instead. (Skipped under an explicit single-active-writer opt-out, which the
		// consumer has taken responsibility for and which is logged above.)
		//
		// The predicate is the presence of the ELECTION, not of the gate: a gate-keyed predicate is
		// supplied by the thing it guards, so a host that registered an election through a path that never
		// wired the gate would read as single-instance and pass silently while draining unfenced.
		//
		// This is the defense-in-depth check for the partitioned / directly-constructed drain. The DEFAULT
		// drain (OutboxBackgroundService -> IOutboxPublisher) never constructs an OutboxProcessor, so the
		// same invariant is ALSO enforced at host startup by OutboxPrerequisiteValidator via the shared
		// OutboxFencingStartupInvariant helper -- covering every drain path. Both call one source of truth.
		OutboxFencingStartupInvariant.EnsureFencingCapableStore(
			OutboxFencingStartupInvariant.IsLeaderElectionRegistered(serviceProvider),
			leaderGate,
			_options.SingleActiveWriter,
			outboxStore);

		if (_options.BatchProcessing.EnableDynamicBatchSizing)
		{
			_batchSizeCalculator = new DynamicBatchSizeCalculator(
				_options.BatchProcessing.MinBatchSize,
				_options.BatchProcessing.MaxBatchSize,
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
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public void Init(string dispatcherId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherId);

		_dispatcherId = dispatcherId;
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
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
		await DisposeCoreAsync().ConfigureAwait(false);
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
		if (_envelopeDeserializer is not null && IsEnvelopeFormat(outboundMessage.Payload))
		{
			// Skip the format marker byte and deserialize the envelope
			var envelopeData = outboundMessage.Payload.AsSpan(1);
			var envelope = DeserializeEnvelope(envelopeData);

			if (envelope is not null)
			{
				// Convert envelope back to IOutboxMessage with the original payload
				var headersJson = envelope.Metadata is not null
					? JsonSerializer.Serialize(envelope.Metadata, CoreMessageJsonContext.Default.DictionaryStringString)
					: "{}";

				return new OutboxMessage(
					messageId: envelope.MessageId.ToString(),
					messageType: envelope.MessageType ?? outboundMessage.MessageType,
					messageMetadata: headersJson,
					messageBody: envelope.Payload ?? [],
					createdAt: envelope.Timestamp,
					expiresAt: outboundMessage.ScheduledAt)
				{
					Attempts = outboundMessage.RetryCount,
					// The identity the store stamped when it claimed this row. Dropping it made the
					// claim-scoped completion members unreachable: the processor selects that route
					// only when an identity is present, so every store fell back to the unscoped
					// write and no claim guard was ever evaluated.
					DispatcherId = outboundMessage.DispatcherId,
					DispatcherTimeout = null,
					TenantId = outboundMessage.TenantId,
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
			messageBody: outboundMessage.Payload,
			createdAt: outboundMessage.CreatedAt,
			expiresAt: outboundMessage.ScheduledAt)
		{
			Attempts = outboundMessage.RetryCount,
			// Carried, not dropped. The comment here claimed OutboundMessage has no such
			// property; it does (OutboundMessage.DispatcherId). The SAME comment sits on the
			// line below, where it is TRUE -- which is why checking it against its neighbour
			// confirmed it every time and the defect survived every reading.
			DispatcherId = outboundMessage.DispatcherId,
			DispatcherTimeout = null, // OutboundMessage doesn't have this property
			TenantId = outboundMessage.TenantId,
		};

	/// <summary>
	/// Deserializes an outbox envelope from binary format using the registered envelope deserializer.
	/// </summary>
	/// <param name="data"> The serialized envelope data. </param>
	/// <returns> The deserialized envelope data, or null if no envelope deserializer is configured. </returns>
	private EnvelopeData? DeserializeEnvelope(ReadOnlySpan<byte> data)
	{
		return _envelopeDeserializer?.DeserializeOutboxEnvelope(data);
	}

	/// <summary>
	/// Disposes of resources used by the <see cref="OutboxProcessor" />.
	/// </summary>
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

			_ = _outboxMessages.Writer.TryComplete();

			_batchMetrics?.Dispose();
		}
		catch (Exception ex)
		{
			LogErrorDisposingAsyncResources(ex);
		}
	}

	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task ProducerLoopAsync(CancellationToken cancellationToken)
	{
		try
		{
			var totalQueued = 0;
			var reachedLimit = false;

			// Guards against queueing the same message twice within this run. Keyed on MessageId, which
			// really is the outbox's unit of work. Cross-instance exclusion is not this set's job: it comes
			// from the store's reservation.
			var queuedThisRun = new HashSet<string>(StringComparer.Ordinal);

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
					LogProducerIdleExiting(_dispatcherId!);

					break;
				}

				LogEnqueuingBatchRecords(batch.Count);

				foreach (var outboxRecord in batch)
				{
					if (queuedThisRun.Add(outboxRecord.MessageId))
					{
						await _outboxMessages.Writer.WriteAsync(outboxRecord, cancellationToken).ConfigureAwait(false);
						totalQueued++;
					}
				}

				reachedLimit = _options.PerRunTotal > 0 && totalQueued >= _options.PerRunTotal;

				BackgroundServiceMetrics.RecordMessagesProcessed(BackgroundServiceTypes.Outbox, BackgroundServiceOperations.Pending, batch.Count);
			}
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
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

				if (_options.BatchProcessing.ParallelProcessingDegree > 1)
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
					ProcessorTypeTags);

				BackgroundServiceMetrics.RecordMessagesProcessed(BackgroundServiceTypes.Outbox, BackgroundServiceOperations.Dispatch, totalProcessedCount);
			}
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			LogConsumerCanceled();
		}
		catch (Exception ex)
		{
			LogErrorInConsumerLoop(ex);

			throw;
		}

		LogOutboxProcessingCompleted(totalProcessedCount);

		BackgroundServiceMetrics.RecordProcessingCycle(BackgroundServiceTypes.Outbox, totalProcessedCount > 0 ? BackgroundServiceResults.Success : BackgroundServiceResults.Empty);

		return totalProcessedCount;
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox batch reservation converts outbound records using runtime serialization.")]
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task<IReadOnlyCollection<IOutboxMessage>> ReserveBatchRecordsAsync(int batchSize, CancellationToken cancellationToken)
	{
		var records = await ClaimBatchAsync(batchSize, cancellationToken).ConfigureAwait(false);

		// Convert OutboundMessage to IOutboxMessage with envelope format support. Pre-size and foreach-add
		// instead of Select().ToList().AsReadOnly() to avoid the per-batch Select-iterator +
		// ReadOnlyCollection-wrapper allocations; the List is returned as the read-only interface.
		var capacity = records.TryGetNonEnumeratedCount(out var count) ? count : batchSize;
		var outboxMessages = new List<IOutboxMessage>(capacity);
		foreach (var record in records)
		{
			outboxMessages.Add(ConvertToOutboxMessageWithEnvelopeSupport(record));
		}

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

		// Re-establish this message's originating tenant as ambient for the whole per-message body so the
		// handler dispatch AND every tenant-aware store mark (sent/failed/dead-lettered) run under the right
		// tenant, scoping the store's composite (TenantId, Id) key correctly.
		//
		// The tenant term is read back off a stored row, so it goes through the total store-read conversion
		// rather than being passed raw. A raw null CLEARS the ambient, and a cleared ambient is "no tenant
		// was established" -- which a multi-tenant store fails closed on (TenantRequiredException), aborting
		// the mark after the handler already ran. An untenanted row is a different state with its own term.
		using var tenantScope = TenantContextHolder.BeginScope(
			KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId);

		var stopwatch = ValueStopwatch.StartNew();
		var attempt = message.Attempts + 1;

		// Get circuit breaker for the transport (uses message type as transport name for now)
		var circuitBreaker = _circuitBreakerRegistry.GetOrCreate(message.MessageType);

		// Circuit breaker open is a TRANSIENT short-circuit, not a delivery failure: the message never
		// reached the transport. Leave it re-claimable with its attempt count UNCHANGED (no attempt
		// consumed, never dead-lettered) so the next poll retries once the breaker recovers. Genuine
		// MaxAttempts-exhausted failures still dead-letter via the catch(Exception) path below.
		if (circuitBreaker.State == CircuitState.Open)
		{
			LogCircuitBreakerOpen(message.MessageType, message.MessageId);
			await MarkFailedForClaimAsync(message.MessageId, message.DispatcherId, ErrorConstants.RetryAttempt, message.Attempts, applyBackoff: false, cancellationToken).ConfigureAwait(false);
			return;
		}

		using var activity = BackgroundServiceActivitySource.StartMessageDispatch(BackgroundServiceTypes.Outbox, message.MessageId);
		_ = activity?.SetTag("excalibur.outbox.dispatcher_id", _dispatcherId);
		_ = activity?.SetTag("messaging.message_type", message.MessageType);

		// Decode BEFORE entering the breaker. A row that cannot be decoded is terminal for that row and
		// never reached the transport, so it must not count against the circuit; dead-letter it directly.
		PreparedDispatch prepared;
		try
		{
			prepared = await PrepareDispatchAsync(message).ConfigureAwait(false);
		}
		catch (OutboxPoisonMessageException ex)
		{
			LogErrorDispatchingOutboxRecord(message.MessageId, _dispatcherId!, ex);

			activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
			BackgroundServiceMetrics.RecordMessagesFailed(BackgroundServiceTypes.Outbox, BackgroundServiceOperations.Dispatch, 1);

			await RouteToDeadLetterQueueAsync(message, DeadLetterReason.DeserializationFailed, ex, cancellationToken)
				.ConfigureAwait(false);

			return;
		}

		// Set the instant the transport accepts the message, so the catch clauses below can tell a failure
		// TO DELIVER from a failure AFTER delivering. Without it both arrive at the same catch and a
		// successful delivery whose bookkeeping failed is recorded as max-retries-exceeded.
		var dispatched = false;

		try
		{
			LogDispatchingOutboxRecord(message.MessageId, _dispatcherId!);

			// Execute dispatch through circuit breaker. It records the outcome -- success or failure --
			// itself; recording it again here would count every delivery failure twice and open the
			// circuit at half the configured threshold.
			await circuitBreaker.ExecuteAsync(async ct =>
			{
				await DispatchAsync(message, prepared, ct).ConfigureAwait(false);
				return true;
			}, cancellationToken).ConfigureAwait(false);

			dispatched = true;

			LogSuccessfullyDispatchedOutboxRecord(message.MessageId, _dispatcherId!);

			BackgroundServiceMetrics.RecordProcessingDuration(BackgroundServiceTypes.Outbox, stopwatch.Elapsed.TotalMilliseconds);

			await MarkSentFencedAsync(message.MessageId, cancellationToken).ConfigureAwait(false);

			LogMarkedOutboxRecordSent(message.MessageId);
		}
		catch (CircuitBreakerOpenException)
		{
			// Circuit opened mid-dispatch: same transient short-circuit as the pre-check. Leave
			// re-claimable with the attempt count UNCHANGED, never dead-letter (no attempt consumed).
			LogCircuitBreakerOpen(message.MessageType, message.MessageId);
			await MarkFailedForClaimAsync(message.MessageId, message.DispatcherId, ErrorConstants.RetryAttempt, message.Attempts, applyBackoff: false, cancellationToken).ConfigureAwait(false);
		}
		catch (OutboxFenceRefusedException ex)
		{
			// The fence refused this tenure's mark-sent because a newer leader has taken over. This is
			// NOT a delivery failure -- the message may already be delivered by the current leader, or
			// still pending for it -- so it must not be retried, backed off, or dead-lettered by a
			// superseded tenure. Abort the drain cycle for this message with NO further store write and
			// leave it exactly as claimed; the live leader resolves it once this reservation ages out.
			LogFencedMarkSentRefused(message.MessageId, ex);
		}
		catch (Exception ex) when (dispatched)
		{
			// THE MESSAGE WAS DELIVERED. Only the store write recording that fact failed, and the two are
			// not the same event. Dead-lettering here would file a delivered message as undeliverable --
			// the precise inversion of what a dead letter means -- and on a store that deletes the row on
			// mark-sent the ordinary causes are mundane: a re-issued mark-sent after a lost ack finds no
			// row and the contract requires the store to throw.
			//
			// Leave the row exactly as claimed, with no further write. It becomes claimable again when the
			// reservation ages out, and the next drain either completes the mark or redelivers. A duplicate
			// delivery is INSIDE the published at-least-once guarantee, which obliges consumers to be
			// idempotent; a dead letter naming a delivered message is outside every guarantee we make.
			//
			// This is the same shape as the fence-refusal clause above, for the same reason: an exception
			// raised after the transport has accepted the message is not evidence about the delivery.
			LogMarkSentFailedAfterDelivery(message.MessageId, ex);
		}
		catch (Exception ex)
		{
			LogErrorDispatchingOutboxRecord(message.MessageId, _dispatcherId!, ex);

			activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
			BackgroundServiceMetrics.RecordMessagesFailed(BackgroundServiceTypes.Outbox, BackgroundServiceOperations.Dispatch, 1);

			// Check if max retries exceeded
			if (attempt >= _options.MaxAttempts)
			{
				// Route to dead letter queue
				await RouteToDeadLetterQueueAsync(message, DeadLetterReason.MaxRetriesExceeded, ex, cancellationToken)
					.ConfigureAwait(false);
			}
			else if (_deliveryGuaranteeOptions.EnableAutomaticRetry)
			{
				// Apply the computed backoff: the claim query won't re-deliver until NextAttemptAt elapses
				// (for stores that support it; others fall back to the plain failed status).
				await MarkFailedForClaimAsync(message.MessageId, message.DispatcherId, ex.Message, attempt, applyBackoff: true, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				// Automatic retry disabled, mark as failed
				await MarkFailedForClaimAsync(message.MessageId, message.DispatcherId, ex.Message, attempt, applyBackoff: false, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Routes a failed message to the dead letter queue.
	/// </summary>
	/// <remarks>
	/// The tenant is established here, from the message being filed, rather than inherited from whatever
	/// scope the caller happens to be in. The single-message path wraps its whole body in a scope and so
	/// was correct; the batch path opens its scope inside the parallel lambda, defers failures to a list,
	/// and drains that list afterwards, by which time the scope is disposed. A dead letter from one tenant
	/// was then filed under the untenanted sentinel -- silently, because the destination column is not
	/// nullable and the sentinel satisfies it. Establishing the scope at the point of use makes the
	/// caller's scope irrelevant, so no future caller can reintroduce the gap by deferring the work.
	/// </remarks>
	private async Task RouteToDeadLetterQueueAsync(
		IOutboxMessage message,
		DeadLetterReason reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		// Store-read tenant term: folded through the total conversion so an untenanted row binds the reserved
		// untenanted term rather than clearing the ambient, which a multi-tenant store fails closed on.
		using var tenantScope = TenantContextHolder.BeginScope(
			KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId);

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

		Guid? enqueuedEntryId = null;

		if (_deadLetterQueue is NullDeadLetterQueue)
		{
			LogMessageDiscardedNoDlq(message.MessageId, reasonText);
		}
		else
		{
			LogMessageRoutedToDlq(message.MessageId, reasonText);

			var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["MessageType"] = message.MessageType,
				["DispatcherId"] = _dispatcherId ?? string.Empty,
				["Attempts"] = message.Attempts.ToString(CultureInfo.InvariantCulture),
				["CreatedAt"] = message.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
			};

			// The id is CAPTURED, not discarded. The external write happens before the fenced mark that
			// makes it true, and that order is deliberate -- a fault between the two must leave the message
			// in the dead-letter queue rather than marked terminal and present nowhere. But it means a
			// REFUSED mark leaves an entry describing a message this tenure no longer owns, and withdrawing
			// it needs the id this call returns.
			enqueuedEntryId = await _deadLetterQueue.EnqueueAsync(
				message,
				reason,
				cancellationToken,
				exception,
				metadata).ConfigureAwait(false);
		}

		// Transition the message to the terminal DeadLettered status (moved to DLQ or discarded). Unlike a
		// retryable Failed status, DeadLettered is excluded from every store's claim predicate. Exclusion alone
		// does not keep it there: a later completion reported against the message must also refuse to move it
		// back, which is the store's obligation under IOutboxStore.MarkFailedAsync rather than something this
		// processor enforces.
		await MarkOutboxMessageDeadLetteredAsync(
			message.MessageId,
			_deadLetterQueue is NullDeadLetterQueue ? $"DISCARDED (no DLQ): {reasonText}" : $"Moved to DLQ: {reasonText}",
			enqueuedEntryId,
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Transitions an outbox message to the terminal <see cref="OutboxStatus.DeadLettered"/> status via the
	/// store's <see cref="IDeadLetterableOutboxStore"/> capability.
	/// </summary>
	/// <remarks>
	/// The capability is guaranteed by the startup presence guard (a polling outbox registered with a store
	/// that does not implement <see cref="IDeadLetterableOutboxStore"/> fails fast). If the capability is
	/// nonetheless absent, this fails loud rather than silently leaving the message re-claimable.
	/// </remarks>
	private async ValueTask MarkOutboxMessageDeadLetteredAsync(
		string messageId,
		string reason,
		Guid? enqueuedEntryId,
		CancellationToken cancellationToken)
	{
		// THE FENCED ROUTE IS PREFERRED, and on THIS member the stakes are higher than on any other. The
		// unfenced transition copies the message to the dead-letter table and DELETES the outbox row on a
		// message-id match alone, so a superseded tenure reaching its attempt ceiling destroys a row a live
		// successor still holds and has not delivered. Every other completion leaves the row recoverable.
		//
		// No claim term here, by ruling: the harm is cross-tenure and a fencing token refuses exactly that.
		// Requiring a per-claim identity as well would restrict the protection to the single store that
		// records one, and buy only the same-process stale-cycle case, which is not what makes this lossy.
		if (_fencingActive
			&& CurrentFencingToken is { } deadLetterToken
			&& _outboxStore.GetService(typeof(IFencedDeadLetterableOutboxStore))
				is IFencedDeadLetterableOutboxStore fencedDeadLetterable)
		{
			var outcome = await fencedDeadLetterable
				.MarkDeadLetteredAsync(messageId, reason, deadLetterToken, cancellationToken)
				.ConfigureAwait(false);

			// Reported, not thrown: this runs inside the drain's own exception handling, where a sibling
			// handler cannot catch. A refusal means the outbox row was NOT destroyed, which is the safe
			// direction FOR THAT ROW -- the live tenure still owns the message and will resolve it.
			//
			// IT IS NOT THE SAFE DIRECTION FOR THE EXTERNAL WRITE THAT ALREADY HAPPENED. The dead-letter
			// entry was enqueued moments ago; the row it describes is now claimable by the live tenure and
			// may be DELIVERED SUCCESSFULLY. That leaves the message simultaneously delivered and sitting in
			// the dead-letter queue unreplayed, where the shipped redrive path selects pending entries by
			// FILTER -- so an operator draining the queue re-executes a message that already succeeded, from
			// our own bookkeeping rather than from any fault of theirs.
			//
			// A dead-letter dedup key cannot help: there is exactly one entry and it is the wrong entry.
			// Withdrawing it is the only thing that restores the invariant, and a refusal is precisely the
			// case where withdrawal is SOUND -- we learned synchronously that our mark did not take, so
			// nothing else is relying on the entry. That is what distinguishes this from a crash between the
			// two writes, where the entry must survive because nobody is left to know it should not.
			if (outcome is not OutboxCompletionOutcome.Applied)
			{
				LogFencedDeadLetterRefused(messageId, outcome.ToString());

				await CompensateDeadLetterEntryAsync(messageId, enqueuedEntryId, outcome.ToString(), cancellationToken)
					.ConfigureAwait(false);
			}

			return;
		}

		// Discovered through GetService, never by casting. A cast sees only the OUTERMOST type, so a store
		// that implements this capability but sits behind any decorator answers "absent" -- and the
		// startup guard, which probes correctly, answers "present" for the same composition. The
		// disagreement surfaced here as a throw on the one path a retry-exhausted message depends on.
		if (_outboxStore.GetService(typeof(IDeadLetterableOutboxStore)) is not IDeadLetterableOutboxStore deadLetterable)
		{
			throw new InvalidOperationException(
				$"The outbox store '{_outboxStore.GetType().FullName}' does not implement IDeadLetterableOutboxStore; " +
				"the startup capability guard should have prevented this configuration. A retry-exhausted message " +
				"cannot be transitioned to the terminal DeadLettered status and would be re-claimed indefinitely.");
		}

		await deadLetterable.MarkDeadLetteredAsync(messageId, reason, cancellationToken).ConfigureAwait(false);
	}


	/// <summary>
	/// Withdraws a dead-letter entry this tenure wrote, after the fenced mark that would have made it true
	/// was refused.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The invariant being restored is that a message is never simultaneously DELIVERED and ACTIONABLE in
	/// the dead-letter queue. A refused mark leaves the outbox row claimable, so the live tenure may deliver
	/// it; the entry this tenure already wrote would then invite an operator redrive of a message that
	/// succeeded.
	/// </para>
	/// <para>
	/// Failure to withdraw is reported at ERROR and not thrown. Throwing would abort the drain of unrelated
	/// messages, and the entry is recoverable by hand: it names the message, and a delivered message is
	/// visible as absent from the outbox. An unwithdrawn entry is the state this code path had before, so a
	/// failed compensation is no worse than not attempting one.
	/// </para>
	/// </remarks>
	private async ValueTask CompensateDeadLetterEntryAsync(
		string messageId,
		Guid? enqueuedEntryId,
		string outcome,
		CancellationToken cancellationToken)
	{
		if (enqueuedEntryId is not { } entryId)
		{
			// Nothing was written: either no dead-letter queue is composed, or the enqueue did not run.
			return;
		}

		// Withdrawal lives on the ADMIN surface, not on IDeadLetterQueue. A plain cast is sound here and is
		// NOT the mistake the store-capability probe above avoids: no decorator over IDeadLetterQueue exists
		// in this framework, and both shipped implementations declare the admin interface alongside the
		// queue one. If a consumer supplies a queue without it, withdrawal is simply unavailable and that is
		// reported rather than passed over in silence.
		if (_deadLetterQueue is not IDeadLetterQueueAdmin admin)
		{
			LogDeadLetterCompensationFailed(messageId, entryId, outcome, exception: null);

			return;
		}

		try
		{
			var purged = await admin.PurgeAsync(entryId, cancellationToken).ConfigureAwait(false);

			if (purged)
			{
				LogDeadLetterEntryCompensated(messageId, entryId, outcome);
			}
			else
			{
				// The queue reports it did not remove the entry. Treated exactly as a failure: the reason
				// this runs is that the entry must not remain, and "it was already gone" is indistinguishable
				// here from "it is still there", so the louder reading is the safe one.
				LogDeadLetterCompensationFailed(messageId, entryId, outcome, exception: null);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Shutdown. The entry stays; see the remarks.
			LogDeadLetterCompensationFailed(messageId, entryId, outcome, exception: null);
		}
		catch (Exception ex)
		{
			LogDeadLetterCompensationFailed(messageId, entryId, outcome, ex);
		}
	}

	/// <summary>
	/// Awaits a FENCED claim-scoped completion and records what the store did with it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Separate from the unfenced observer because one extra outcome is reachable here, and it is the one
	/// that is not about this message.</b> <see cref="OutboxCompletionOutcome.FenceRefused"/> means a newer
	/// tenure exists, so every remaining write this caller would make is unfenced. Logging it beside a lost
	/// claim would file a tenure-wide stand-down as a per-row skip.
	/// </para>
	/// <para>
	/// It is recorded and not thrown, for the same reason every other refusal on this path is: the callers
	/// sit inside their own <c>catch</c>, where a sibling clause cannot run. The drain-wide consequence of a
	/// superseded tenure is carried by the fenced mark-sent, which is the member that already aborts the
	/// cycle; duplicating that abort here would unwind through an exception handler that cannot catch it.
	/// </para>
	/// </remarks>
	private async ValueTask ObserveFencedCompletionAsync(
		ValueTask<OutboxCompletionOutcome> completion,
		string messageId)
	{
		var outcome = await completion.ConfigureAwait(false);

		switch (outcome)
		{
			case OutboxCompletionOutcome.Applied:
				break;

			case OutboxCompletionOutcome.FenceRefused:
				LogFencedFailureReportRefused(messageId);
				break;

			case OutboxCompletionOutcome.ClaimLost:
				LogClaimLostOnFailureReport(messageId);
				break;

			case OutboxCompletionOutcome.MessageNotFound:
				LogFailureReportFoundNoMessage(messageId);
				break;

			default:
				LogFailureReportOutcomeUnrecognised(messageId, outcome.ToString());
				break;
		}
	}

	/// <summary>
	/// Awaits a claim-scoped completion and records what the store actually did with it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>A lost claim is ROW-scoped, and it reaches this method as a returned value rather than as a
	/// throw.</b> Every caller of this method sits inside its own <c>catch</c>, where a sibling <c>catch</c>
	/// clause on the same <c>try</c> cannot run — so an exception raised here would escape the entire drain
	/// cycle and abandon every message the caller still legitimately holds. A refusal on one message has to
	/// cost that message and nothing else, which a return value gives and an exception cannot.
	/// </para>
	/// <para>
	/// The outcomes are kept apart deliberately. <see cref="OutboxCompletionOutcome.ClaimLost"/> means
	/// this message was re-claimed while the tenure is intact;
	/// <see cref="OutboxCompletionOutcome.MessageNotFound"/> means there was nothing left to write to. Both
	/// are non-events for the batch and neither is a reason to stop, but they have opposite diagnoses, so
	/// logging them as one would hide a re-claim behind a cleanup. Anything else — including the value no
	/// store chooses — is treated as NOT applied and logged at warning, so a member added to the
	/// enumeration after this build was compiled cannot pass through here as a success.
	/// </para>
	/// </remarks>
	private async ValueTask MarkFailedForOneClaimAsync(
		ValueTask<OutboxCompletionOutcome> completion,
		string messageId)
	{
		// SUCCESS IS THE CASE THAT MUST BE NAMED. Deciding "applied" by falling through the refusals means
		// every value added to this enumeration later — and every value this build was not compiled
		// against — begins life as a silent success. Matching Applied explicitly and treating everything
		// else as not-applied is what makes a future member arrive loud instead of unnoticed.
		var outcome = await completion.ConfigureAwait(false);

		switch (outcome)
		{
			case OutboxCompletionOutcome.Applied:
				break;

			case OutboxCompletionOutcome.ClaimLost:
				LogClaimLostOnFailureReport(messageId);
				break;

			case OutboxCompletionOutcome.MessageNotFound:
				LogFailureReportFoundNoMessage(messageId);
				break;

			default:
				// Reached by an unrecognised value and by the zero value, which no store chooses. Warning
				// rather than information: the two arms above are ordinary operational events, whereas
				// arriving here means the store said something this build cannot interpret, and the only
				// safe reading of that is that the failure was NOT recorded.
				LogFailureReportOutcomeUnrecognised(messageId, outcome.ToString());
				break;
		}
	}

	/// <summary>
	/// Marks a message failed and, when the store supports the per-message backoff schedule
	/// (<see cref="IBackoffSchedulableOutboxStore"/>), records the computed next-attempt time so the claim
	/// query throttles re-delivery. Stores without the capability fall back to the plain failed status
	/// (today's behavior) -- the fail-open pattern, mirroring the optional dead-letter capability.
	/// </summary>
	private async ValueTask MarkFailedForClaimAsync(
		string messageId,
		string? claimIdentity,
		string errorMessage,
		int attempt,
		bool applyBackoff,
		CancellationToken cancellationToken)
	{
		// Capability discovery, never a cast: a cast sees only the outermost decorator and is lossy through
		// the composition. A store that does not offer the claim-scoped surface falls back to the unscoped
		// members, which is the pre-existing behaviour rather than a downgrade.
		var claimScoped = claimIdentity is { Length: > 0 }
			? _outboxStore.GetService(typeof(IClaimScopedOutboxStore)) as IClaimScopedOutboxStore
			: null;

		// THE FENCED ROUTE IS PREFERRED, and it is reachable only when all three facts hold together: the
		// store offers the combined capability, a tenure is active with a token, and the message carries the
		// claim it was handed under. Any one of them missing means this caller cannot make the stronger
		// assertion, so it falls to the route that asserts what it can. A third probe is the honest price of
		// three genuinely different implementor populations -- the alternative is a store throwing from a
		// member its interface says it has.
		if (claimIdentity is { Length: > 0 } claim
			&& _fencingActive
			&& CurrentFencingToken is { } fencingToken
			&& _outboxStore.GetService(typeof(IFencedClaimScopedOutboxStore)) is IFencedClaimScopedOutboxStore fencedScoped)
		{
			var authority = new OutboxWriteAuthority(fencingToken, claim);

			// The schedule is computed here rather than in the store because the backoff policy is the
			// caller's, exactly as on the unfenced route; null means this failure takes no computed schedule.
			var fencedSchedule = applyBackoff
				&& _outboxStore.GetService(typeof(IBackoffSchedulableOutboxStore)) is IBackoffSchedulableOutboxStore
					? (DateTimeOffset?)(DateTimeOffset.UtcNow + _backoffCalculator.CalculateDelay(attempt))
					: null;

			await ObserveFencedCompletionAsync(
				fencedScoped.MarkFailedAsync(
					messageId, errorMessage, attempt, fencedSchedule, authority, cancellationToken),
				messageId).ConfigureAwait(false);
			return;
		}

		if (applyBackoff
			&& _outboxStore.GetService(typeof(IBackoffSchedulableOutboxStore)) is IBackoffSchedulableOutboxStore schedulable)
		{
			var nextAttemptAt = DateTimeOffset.UtcNow + _backoffCalculator.CalculateDelay(attempt);

			// The backoff route is the one a genuine delivery failure takes when automatic retry is enabled,
			// so it must carry the claim too. Scoping only the plain completion would leave the dominant
			// failure path unscoped behind a capability that reads as though it closed it.
			if (claimScoped is not null)
			{
				await MarkFailedForOneClaimAsync(
					claimScoped.MarkFailedWithBackoffAsync(
						messageId, errorMessage, attempt, nextAttemptAt, claimIdentity!, cancellationToken),
					messageId).ConfigureAwait(false);
				return;
			}

			await schedulable.MarkFailedWithBackoffAsync(
				messageId, errorMessage, attempt, nextAttemptAt, cancellationToken).ConfigureAwait(false);
			return;
		}

		if (claimScoped is not null)
		{
			await MarkFailedForOneClaimAsync(
				claimScoped.MarkFailedAsync(messageId, errorMessage, attempt, claimIdentity!, cancellationToken),
				messageId).ConfigureAwait(false);
			return;
		}

		await _outboxStore.MarkFailedAsync(messageId, errorMessage, attempt, cancellationToken)
			.ConfigureAwait(false);
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification =
			"Outbox message dispatch requires runtime type resolution from MessageTypeRegistry for polymorphic message types - reflection is intentional")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox dispatch uses runtime deserialization for stored message payloads.")]
	private async Task<int> ProcessBatchParallelAsync(IOutboxMessage[] batch, CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var successfulIds = new ConcurrentBag<string>();
		// ApplyBackoff distinguishes a genuine delivery failure (attempt consumed -> apply the backoff
		// schedule) from a transient circuit-breaker-open short-circuit (attempt UNCHANGED -> no backoff).
		var failedToRetry = new ConcurrentBag<(string MessageId, string? ClaimIdentity, int AttemptCount, bool ApplyBackoff)>();
		var failedToDeadLetter =
			new ConcurrentBag<(string MessageId, IOutboxMessage Message, Exception? Exception, DeadLetterReason Reason)>();

		// Process messages in parallel
		var (successful, failed) = await Batching.ProcessBatchAsync(
			batch,
			async (message, ct) =>
			{
				// Per-iteration tenant scope: re-establish this message's originating tenant as ambient so the
				// handler dispatch runs under the right tenant, and (MinimizedWindow) the immediate MarkSentAsync
				// scopes the store's composite (TenantId, Id) key. Wraps the single iteration only — each message
				// may carry a different tenant. Store-read tenant term: folded through the total conversion so
				// an untenanted row binds the reserved untenanted term rather than clearing the ambient,
				// which a multi-tenant store fails closed on.
				using var tenantScope = TenantContextHolder.BeginScope(
					KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId);

				var attempt = message.Attempts + 1;

				// Get circuit breaker for the transport
				var circuitBreaker = _circuitBreakerRegistry.GetOrCreate(message.MessageType);

				// Circuit breaker open is a TRANSIENT short-circuit, not a delivery failure (this is the
				// high-volume batch path where dead-lettering causes bulk loss on a transient outage).
				// Leave for retry with the attempt count UNCHANGED — no attempt consumed, never
				// dead-lettered. Don't throw: this isn't a delivery failure for batch-failure tracking.
				if (circuitBreaker.State == CircuitState.Open)
				{
					LogCircuitBreakerOpen(message.MessageType, message.MessageId);
					failedToRetry.Add((message.MessageId, message.DispatcherId, message.Attempts, ApplyBackoff: false));

					return; // Don't throw - we've handled this case
				}

				// Decode BEFORE entering the breaker. A row that cannot be decoded is terminal for that
				// row and never reached the transport, so it must not count against the circuit --
				// otherwise one corrupt row in a batch opens it for every healthy message behind it.
				PreparedDispatch prepared;
				try
				{
					prepared = await PrepareDispatchAsync(message).ConfigureAwait(false);
				}
				catch (OutboxPoisonMessageException ex)
				{
					LogErrorDispatchingOutboxRecord(message.MessageId, _dispatcherId!, ex);
					failedToDeadLetter.Add((message.MessageId, message, ex, DeadLetterReason.DeserializationFailed));

					return; // Don't throw - a poison row is not a batch delivery failure.
				}

				// See the single-message path: this distinguishes a failure TO DELIVER from a failure AFTER
				// delivering, which otherwise reach the same catch.
				var dispatched = false;

				try
				{
					// Execute dispatch through circuit breaker. It records the outcome itself; see the
					// single-message path above.
					await circuitBreaker.ExecuteAsync(async token =>
					{
						await DispatchSingleMessageAsync(message, prepared, token).ConfigureAwait(false);
						return true;
					}, ct).ConfigureAwait(false);

					dispatched = true;

					// For MinimizedWindow, mark sent immediately after dispatch
					if (_options.DeliveryGuarantee == OutboxDeliveryGuarantee.MinimizedWindow)
					{
						await MarkSentFencedAsync(message.MessageId, ct).ConfigureAwait(false);
					}
					else
					{
						// For AtLeastOnce, collect for batch completion
						successfulIds.Add(message.MessageId);
					}
				}
				catch (CircuitBreakerOpenException)
				{
					// Circuit opened mid-dispatch: transient short-circuit. Leave for retry with the
					// attempt count UNCHANGED (no attempt consumed, never dead-letter). Not re-thrown.
					LogCircuitBreakerOpen(message.MessageType, message.MessageId);
					failedToRetry.Add((message.MessageId, message.DispatcherId, message.Attempts, ApplyBackoff: false));
				}
				catch (OutboxFenceRefusedException ex)
				{
					// MinimizedWindow's immediate MarkSentFencedAsync was refused: a newer leader has taken
					// over. Not a delivery failure -- must not enter failedToRetry or failedToDeadLetter,
					// both of which perform an UNFENCED store write. Abort this message with no further
					// write and leave it as claimed for the live leader to resolve. Not re-thrown: a stale
					// fence refusal is not a batch delivery failure for tracking purposes.
					LogFencedMarkSentRefused(message.MessageId, ex);

					return;
				}
				catch (Exception ex) when (dispatched)
				{
					// THE MESSAGE WAS DELIVERED -- only the MinimizedWindow mark-sent that follows it failed.
					// See the single-message path for the full argument. Not added to failedToDeadLetter, not
					// added to failedToRetry, and NOT re-thrown: neither a dead letter nor an attempt-consuming
					// retry is an honest record of a delivery that happened, and this is not a batch delivery
					// failure for tracking purposes.
					LogMarkSentFailedAfterDelivery(message.MessageId, ex);

					return;
				}
				catch (Exception ex)
				{
					LogErrorDispatchingOutboxRecord(message.MessageId, _dispatcherId!, ex);

					if (attempt >= _options.MaxAttempts)
					{
						failedToDeadLetter.Add((message.MessageId, message, ex, DeadLetterReason.MaxRetriesExceeded));
					}
					else if (_deliveryGuaranteeOptions.EnableAutomaticRetry)
					{
						// Genuine delivery failure: apply the computed backoff schedule when marking failed.
						LogRetryWithBackoff(message.MessageId, attempt, _backoffCalculator.CalculateDelay(attempt).TotalMilliseconds);

						failedToRetry.Add((message.MessageId, message.DispatcherId, attempt, ApplyBackoff: true));
					}
					else
					{
						failedToRetry.Add((message.MessageId, message.DispatcherId, attempt, ApplyBackoff: false));
					}

					throw; // Re-throw for Batching to track
				}
			},
			_options.BatchProcessing.ParallelProcessingDegree,
			_options.BatchProcessing.BatchProcessingTimeout,
			cancellationToken).ConfigureAwait(false);

		// Route messages to DLQ.
		//
		// EVERY ITERATION IS GUARDED, AND THE REASON IS THE BLOCK BELOW RATHER THAN THIS ONE. An unguarded
		// throw out of this loop did two things, and the second is the expensive one: it stranded the
		// remaining dead letters, AND it skipped the batch-completion block that follows -- so messages this
		// cycle had ALREADY DELIVERED were never marked sent, and the next drain delivered them again. A
		// fault while disposing of a failed message must not cause the redelivery of a successful one; those
		// messages are unrelated, and only the control flow joined them.
		foreach (var (messageId, message, exception, reason) in failedToDeadLetter)
		{
			try
			{
				await RouteToDeadLetterQueueAsync(message, reason, exception, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Shutdown. Stop routing -- but fall through rather than propagate, because the completion
				// block below is what prevents the already-delivered messages from being sent twice, and it
				// is more important that they are marked than that the rest of this batch is dead-lettered.
				break;
			}
			catch (Exception ex)
			{
				// The message stays claimed and un-dead-lettered, so the next cycle retries it. That is the
				// correct outcome: it is at worst a delayed dead-letter, where propagating would have been a
				// duplicate delivery of a different message.
				LogDeadLetterRoutingFailed(messageId, ex);
			}
		}

		// Branch on DeliveryGuarantee setting for batch completion
		var successfulIdsOnly = successfulIds.ToList();

		// For MinimizedWindow, messages were already marked individually in the dispatch loop
		if (_options.DeliveryGuarantee != OutboxDeliveryGuarantee.MinimizedWindow && successfulIdsOnly.Count > 0)
		{
			// AtLeastOnce: complete the successfully-published batch.
			if (_options.EnableBatchDatabaseOperations)
			{
				// AtLeastOnce with batch operations
				// The retry set is deliberately NOT passed here. Every guarantee level marks it below, in
				// the one loop that runs unconditionally -- passing it here as well marked each failed
				// message twice per cycle. The attempt count is passed absolutely, so the second write was
				// invisible in attempt accounting; NextAttemptAt is not, and the second write recomputed the
				// backoff from the later instant, pushing redelivery out by roughly one whole interval.
				await PerformBatchDatabaseOperationsAsync(successfulIdsOnly, [], new List<(string, int)>(), cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				// AtLeastOnce without batch operations - fall back to individual operations
				// Tracks WHICH id the refusal landed on, so the warning names the message actually refused
				// rather than the first in the batch. The distinction matters to whoever reads the log: the
				// ids before it were marked sent successfully and the ones after it were never attempted.
				var refusedId = string.Empty;
				try
				{
					foreach (var id in successfulIdsOnly)
					{
						refusedId = id;
						await MarkSentFencedAsync(id, cancellationToken).ConfigureAwait(false);
					}
				}
				catch (OutboxFenceRefusedException ex)
				{
					// A newer leader has taken over. Return rather than throw, and return from HERE rather
					// than break out of the loop, because the failure-path writes below this point are
					// UNFENCED: a superseded tenure reaching them would mark another tenure's rows failed
					// and restamp their next-attempt instants. Returning is what skips them.
					//
					// Letting this propagate instead is what the caller used to do, and it was wrong three
					// ways: it reported a routine leadership handover as two ERROR-level events where every
					// other refusal site emits one warning, so the fence working as designed was
					// indistinguishable from a drain failure; it abandoned the remaining already-published
					// ids unmarked, producing avoidable redeliveries; and on the manual-trigger path, which
					// has no background service above it to swallow the exception, it surfaced to consumer
					// code as an invocation failure on a normal handover.
					//
					// Nothing is lost by stopping here. No state is changed, the unmarked ids remain claimed
					// and become claimable again when their claim ages out, and the next claim presents the
					// same stale token, which a fenced store answers with zero rows rather than a throw --
					// so the loop drains empty and the gate skips the cycle. The attempt counts not consumed
					// belong to a tenure that no longer owns those rows; the live leader consumes its own.
					//
					// THIS DOES NOT MAKE THE DRAIN CYCLE FENCED. It repairs the one branch where the
					// unfenced writes happen to sit after the point the refusal is learned. On the other
					// paths they run before it, or concurrently with it, and no arrangement of catch clauses
					// reaches them -- that needs the failure-path members to take a token of their own.
					LogFencedMarkSentRefused(refusedId, ex);

					return successful.Count;
				}
			}
		}

		// Handle failed messages for retry (applies to all guarantee levels)
		if (!failedToRetry.IsEmpty)
		{
			foreach (var (id, claimIdentity, attemptCount, applyBackoff) in failedToRetry)
			{
				try
				{
					if (applyBackoff)
					{
						await MarkFailedForClaimAsync(id, claimIdentity, ErrorConstants.RetryAttempt, attemptCount, applyBackoff: true, cancellationToken)
							.ConfigureAwait(false);
					}
					else
					{
						await MarkFailedForClaimAsync(id, claimIdentity, ErrorConstants.RetryAttempt, attemptCount, applyBackoff: false, cancellationToken).ConfigureAwait(false);
					}
				}
				catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
				{
					throw;
				}
#pragma warning disable CA1031 // One message's status update must not abandon the rest of the retry set
				catch (Exception ex)
				{
					// Previously this loop had no isolation and the batch path did. Now that the batch path
					// no longer marks the retry set, this is the only site -- so it carries the isolation, or
					// de-duplicating would have quietly traded a latency defect for a lost-update one.
					LogErrorMarkingOutboxMessage(id, "Failed", ex);
				}
#pragma warning restore CA1031
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
			_batchCompletionTags);

		return successful.Count;
	}

	private async Task PerformBatchDatabaseOperationsAsync(
		List<string> successfulIds,
		List<(string MessageId, string? ClaimIdentity, int AttemptCount, bool ApplyBackoff)> failedToRetry,
		List<(string MessageId, int AttemptCount)> failedToDeadLetter,
		CancellationToken cancellationToken)
	{
		var tasks = new List<Task>();

		if (successfulIds.Count > 0)
		{
			// Process successful messages in parallel -- individual try/catch prevents
			// a single DB failure from leaving the entire batch in inconsistent state.
			tasks.Add(Task.Factory.StartNew(async () =>
			{
				foreach (var id in successfulIds)
				{
					try
					{
						await MarkSentFencedAsync(id, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						throw;
					}
					catch (OutboxFenceRefusedException ex)
					{
						// A newer leader has taken over. Already no further store write follows in this
						// loop (correct), but caught specifically so this is never confused with a genuine
						// mark-sent failure -- it is the fence working as designed.
						LogFencedMarkSentRefused(id, ex);
					}
#pragma warning disable CA1031 // Individual message status update failure must not prevent processing remaining messages
					catch (Exception ex)
					{
						LogErrorMarkingOutboxMessage(id, "Sent", ex);
					}
#pragma warning restore CA1031
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
		}

		if (failedToRetry.Count > 0)
		{
			// Process retry messages in parallel
			tasks.Add(Task.Factory.StartNew(async () =>
			{
				foreach (var (id, claimIdentity, attemptCount, applyBackoff) in failedToRetry)
				{
					try
					{
						if (applyBackoff)
						{
							await MarkFailedForClaimAsync(id, claimIdentity, ErrorConstants.RetryAttempt, attemptCount, applyBackoff: true, cancellationToken)
								.ConfigureAwait(false);
						}
						else
						{
							await MarkFailedForClaimAsync(id, claimIdentity, ErrorConstants.RetryAttempt, attemptCount, applyBackoff: false, cancellationToken).ConfigureAwait(false);
						}
					}
					catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
					{
						throw;
					}
#pragma warning disable CA1031 // Individual message status update failure must not prevent processing remaining messages
					catch (Exception ex)
					{
						LogErrorMarkingOutboxMessage(id, "Failed", ex);
					}
#pragma warning restore CA1031
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
		}

		if (failedToDeadLetter.Count > 0)
		{
			// Process dead letter messages in parallel
			tasks.Add(Task.Factory.StartNew(async () =>
			{
				foreach (var (id, _) in failedToDeadLetter)
				{
					try
					{
						// No entry id: this path marks the terminal status only. The external dead-letter write
						// for these messages happened in RouteToDeadLetterQueueAsync, which owns its own
						// compensation, so there is nothing for this call to withdraw.
						await MarkOutboxMessageDeadLetteredAsync(
							id,
							ErrorConstants.MaxRetriesReachedMovedToDeadLetter,
							enqueuedEntryId: null,
							cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
					{
						throw;
					}
#pragma warning disable CA1031 // Individual message status update failure must not prevent processing remaining messages
					catch (Exception ex)
					{
						LogErrorMarkingOutboxMessage(id, "DeadLettered", ex);
					}
#pragma warning restore CA1031
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
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
	private async Task DispatchSingleMessageAsync(IOutboxMessage message, PreparedDispatch prepared, CancellationToken cancellationToken)
	{
		using var activity = BackgroundServiceActivitySource.StartMessageDispatch(BackgroundServiceTypes.Outbox, message.MessageId);
		_ = activity?.SetTag("excalibur.outbox.dispatcher_id", _dispatcherId);

		LogDispatchingOutboxRecord(message.MessageId, _dispatcherId!);

		await DispatchAsync(message, prepared, cancellationToken).ConfigureAwait(false);

		LogSuccessfullyDispatchedOutboxRecord(message.MessageId, _dispatcherId!);
	}

	/// <summary>
	/// A staged row decoded into the message and metadata the dispatcher needs.
	/// </summary>
	private readonly record struct PreparedDispatch(IDispatchMessage Message, DeliveryMetadata Metadata);

	/// <summary>
	/// Decodes a staged row into a dispatchable message, ahead of any transport attempt.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The staged body is the serialized MESSAGE, written by the outbox writer as its own runtime type.
	/// It is not a nested envelope: the identifier, the type name and the metadata all arrive on the
	/// <see cref="IOutboxMessage"/> the store conversion already produced, so they are read from there
	/// rather than reconstructed out of the body.
	/// </para>
	/// <para>
	/// This runs OUTSIDE the transport circuit breaker, and that placement is load-bearing. A body this
	/// process cannot decode will not decode on the next attempt and never reached a transport, so it is
	/// evidence about the row rather than about transport health. Decoding inside the breaker lets one
	/// corrupt row count against the circuit and stall delivery of every healthy message behind it.
	/// </para>
	/// </remarks>
	/// <exception cref="OutboxPoisonMessageException"> Thrown when the row cannot be decoded. </exception>
	[RequiresUnreferencedCode("Uses DeserializeAsync with runtime type resolution from MessageTypeRegistry")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Serialization.DispatchJsonSerializer.DeserializeAsync(String, Type)")]
	private async Task<PreparedDispatch> PrepareDispatchAsync(IOutboxMessage outboxMessage)
	{
		try
		{
			if (outboxMessage.MessageBody.Length == 0)
			{
				BackgroundServiceMetrics.RecordProcessingError(BackgroundServiceTypes.Outbox, "empty_message");

				throw new InvalidOperationException(ErrorConstants.CouldNotDeserializeAsDispatchMessage);
			}

			if (!MessageTypeRegistry.TryGetType(outboxMessage.MessageType, out var type))
			{
				throw new TypeLoadException($"{ErrorConstants.TypeNotFoundInRegistry}: {outboxMessage.MessageType}");
			}

			if (_serializer.DeserializeFromUtf8(outboxMessage.MessageBody, type) is not IDispatchMessage
				dispatchMessage)
			{
				throw new InvalidOperationException(
					$"{ErrorConstants.CouldNotDeserializeAsDispatchMessage}: {outboxMessage.MessageType}");
			}

			var deserializedMetadata =
				await _serializer.DeserializeAsync(outboxMessage.MessageMetadata, typeof(DeliveryMetadata)).ConfigureAwait(false);
			if (deserializedMetadata is not DeliveryMetadata deliveryMetadata)
			{
				throw new InvalidOperationException(
					$"{ErrorConstants.FailedToDeserializeMessageMetadata}: {outboxMessage.MessageId}");
			}

			return new PreparedDispatch(dispatchMessage, deliveryMetadata);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
#pragma warning disable CA1031 // Every non-cancellation decode failure is the same terminal condition for this row.
		catch (Exception ex)
		{
			throw new OutboxPoisonMessageException(outboxMessage.MessageId, ex.Message, ex);
		}
#pragma warning restore CA1031
	}

	private async Task DispatchAsync(IOutboxMessage outboxMessage, PreparedDispatch prepared, CancellationToken cancellationToken)
	{
		try
		{
			await using var scope = _serviceProvider.CreateAsyncScope();

			// Seed the context directly from the strongly-typed metadata — no per-message dictionary alloc.
			var messageContext = DispatchContextInitializer.CreateFromMetadata(prepared.Metadata);
			messageContext.MessageId = outboxMessage.MessageId;

			var scopedDispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
			var result = await scopedDispatcher.DispatchAsync(prepared.Message, messageContext, cancellationToken)
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
			LogDispatchFailed(outboxMessage.MessageId, ex);

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

	[LoggerMessage(OutboxEventId.OutboxDeadLetterRoutingFailed, LogLevel.Error,
		"Failed to route message {MessageId} to the dead letter queue. It remains claimed and will be retried "
		+ "on a later cycle; the rest of this batch is unaffected.")]
	private partial void LogDeadLetterRoutingFailed(string messageId, Exception exception);

	[LoggerMessage(OutboxEventId.OutboxCircuitBreakerOpen, LogLevel.Warning,
		"Circuit breaker open for transport {TransportName}, message {MessageId} left for retry (not dead-lettered)")]
	private partial void LogCircuitBreakerOpen(string transportName, string messageId);

	[LoggerMessage(OutboxEventId.OutboxClaimLostOnFailureReport, LogLevel.Information,
		"Failure report for message {MessageId} was declined: the claim it reported against is no longer held. Continuing with the rest of the batch.")]
	private partial void LogClaimLostOnFailureReport(string messageId);

	[LoggerMessage(OutboxEventId.OutboxFailureReportFoundNoMessage, LogLevel.Information,
		"Failure report for message {MessageId} wrote nothing: no such message remained when the store ran the statement. Continuing with the rest of the batch.")]
	private partial void LogFailureReportFoundNoMessage(string messageId);

	[LoggerMessage(OutboxEventId.OutboxFencedDeadLetterRefused, LogLevel.Warning,
		"Dead-letter transition for message {MessageId} did not apply ({Outcome}); the outbox row was NOT destroyed. A fence refusal means a newer leadership tenure owns this message and will resolve it.")]
	private partial void LogFencedDeadLetterRefused(string messageId, string outcome);

	[LoggerMessage(OutboxEventId.OutboxFencedFailureReportRefused, LogLevel.Warning,
		"Failure report for message {MessageId} was refused by the fence: a newer leadership tenure exists, so this tenure recorded nothing. It should stand down rather than continue draining.")]
	private partial void LogFencedFailureReportRefused(string messageId);

	[LoggerMessage(OutboxEventId.OutboxFailureReportOutcomeUnrecognised, LogLevel.Warning,
		"Failure report for message {MessageId} returned outcome {Outcome}, which this build does not recognise. Treating the failure as NOT recorded; the message stays claimed until its reservation lapses.")]
	private partial void LogFailureReportOutcomeUnrecognised(string messageId, string outcome);

	[LoggerMessage(OutboxEventId.OutboxRetryWithBackoff, LogLevel.Debug,
		"Message {MessageId} retry attempt {Attempt}, backoff delay {DelayMs}ms")]
	private partial void LogRetryWithBackoff(string messageId, int attempt, double delayMs);

	[LoggerMessage(OutboxEventId.OutboxErrorMarkingMessage, LogLevel.Error,
		"Error marking outbox message {MessageId} as {TargetStatus} during batch completion")]
	private partial void LogErrorMarkingOutboxMessage(string messageId, string targetStatus, Exception ex);

	[LoggerMessage(OutboxEventId.OutboxDeadLetterQueueNotConfigured, LogLevel.Warning,
		"No IDeadLetterQueue registered. Failed outbox messages will be discarded silently. Register a dead letter queue implementation to preserve failed messages for investigation.")]
	private partial void LogDeadLetterQueueNotConfigured();

	[LoggerMessage(OutboxEventId.OutboxCircuitBreakerNotConfigured, LogLevel.Warning,
		"No ITransportCircuitBreakerRegistry registered. Transport failures will not trigger circuit breakers. Register AddDispatchResilience() to enable transport protection.")]
	private partial void LogCircuitBreakerNotConfigured();

	[LoggerMessage(OutboxEventId.OutboxMessageDiscardedNoDlq, LogLevel.Error,
		"OUTBOX MESSAGE LOST: Message {MessageId} failed ({Reason}) but no dead letter queue is configured. Message has been discarded permanently. Register an IDeadLetterQueue to prevent message loss.")]
	private partial void LogMessageDiscardedNoDlq(string messageId, string reason);

	[LoggerMessage(OutboxEventId.OutboxUnfencedBySingleWriterOptOut, LogLevel.Warning,
		"Outbox drain is running UNFENCED by an explicit single-active-writer opt-out (AsSingleWriter), even though a leader election is registered. This is safe only if exactly one process drains this outbox; a genuinely multi-writer deployment reopens the split-brain window.")]
	private partial void LogOutboxUnfencedBySingleWriterOptOut();

	[LoggerMessage(OutboxEventId.OutboxRunningUnfenced, LogLevel.Information,
		"Outbox drain is running UNFENCED because no leader election is registered. This is safe only when exactly one process drains this outbox. Register a leader election for multi-instance deployments to fence the drain against a superseded leader.")]
	private partial void LogOutboxRunningUnfenced();

	[LoggerMessage(OutboxEventId.OutboxDeadLetterEntryCompensated, LogLevel.Warning,
		"Withdrew dead-letter entry {EntryId} for message {MessageId}: the fenced mark that would have made it true was refused ({Outcome}), so the message is still owned by the live tenure and may yet be delivered. Leaving the entry would have invited an operator redrive of a message that succeeded.")]
	private partial void LogDeadLetterEntryCompensated(string messageId, Guid entryId, string outcome);

	[LoggerMessage(OutboxEventId.OutboxDeadLetterCompensationFailed, LogLevel.Error,
		"Could not withdraw dead-letter entry {EntryId} for message {MessageId} after its fenced mark was refused ({Outcome}). The entry describes a message this tenure no longer owns and the live tenure may deliver it, so a redrive of that entry would duplicate a successful delivery. Remove the entry by hand after confirming the message was delivered.")]
	private partial void LogDeadLetterCompensationFailed(string messageId, Guid entryId, string outcome, Exception? exception);

	[LoggerMessage(OutboxEventId.OutboxMarkSentFailedAfterDelivery, LogLevel.Error,
		"Message {MessageId} was DELIVERED, but the store write recording that fact failed. The delivery stands and the message is NOT dead-lettered: filing a delivered message as undeliverable would be worse than the duplicate that may follow. The row is left claimed; when its reservation ages out the next drain completes the mark or redelivers, which the at-least-once guarantee permits and consumers are obliged to tolerate.")]
	private partial void LogMarkSentFailedAfterDelivery(string messageId, Exception ex);

	[LoggerMessage(OutboxEventId.OutboxFencedMarkSentRefused, LogLevel.Warning,
		"Fenced mark-sent for message {MessageId} was refused: this tenure's fencing token is stale (a newer leader has taken over). Aborting with no further store write; the message is left claimed for the current leader to resolve.")]
	private partial void LogFencedMarkSentRefused(string messageId, Exception ex);
}
