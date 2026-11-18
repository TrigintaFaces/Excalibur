// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics;
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
using Excalibur.Dispatch.Outbox;
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
	private Channel<IOutboxMessage> _outboxMessages;
	private readonly int _queueCapacity;

	// Orchestration layer IOutboxStore for integration event outbox (restored from git history)
	private readonly IOutboxStore _outboxStore;

	private readonly DispatchJsonSerializer _serializer;
	private readonly IMessageFailureClassifier _failureClassifier;
	private readonly IBinaryEnvelopeDeserializer? _envelopeDeserializer;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<OutboxProcessor> _logger;
	private readonly BatchProcessingMetrics _batchMetrics;
	private readonly DynamicBatchSizeCalculator? _batchSizeCalculator;

	// Resilience components

	/// <summary>
	/// How long the dead-letter compensation may spend withdrawing an entry whose fenced mark was refused.
	/// </summary>
	/// <remarks>
	/// Deliberately generous relative to a single queue round trip and deliberately finite. The compensation
	/// runs detached from the drain's cancellation, so without a deadline of its own a queue that never
	/// answers would hold shutdown open indefinitely.
	/// </remarks>
	private static readonly TimeSpan DeadLetterCompensationTimeout = TimeSpan.FromSeconds(15);

	private readonly IDeadLetterQueue _deadLetterQueue;

	private readonly ITransportCircuitBreakerRegistry _circuitBreakerRegistry;
	private readonly IBackoffCalculator _backoffCalculator;
	private readonly DeliveryGuaranteeOptions _deliveryGuaranteeOptions;

	// Optional leader-election gate (Excalibur.Dispatch.ILeaderProcessingGate). When registered,
	// its FencingToken is captured once per run and threaded through every store operation so a superseded
	// leader's claim/mark is rejected by the store's server-side compare-and-swap rather than silently
	// racing a newer leader. Null when no leader election is configured (plain, unfenced claim).
	private readonly Excalibur.Dispatch.ILeaderProcessingGate? _leaderGate;

	// Fencing is DEFAULT-ON for a framework-protected outbox: true exactly when a leader gate is present AND
	// the consumer has NOT asserted single-active-writer ownership (OutboxDeliveryOptions.SingleActiveWriter).
	// Registering a leader election is the multi-instance signal, so its presence fences the drain by default;
	// AsSingleWriter() is the explicit, logged opt-out for a genuinely single-writer topology. When false, the
	// drain runs unfenced (either no leader election, or an explicit single-writer opt-out).
	private readonly bool _fencingActive;

	// Authority is captured once in ProcessorTenure, never reread to label an old claim.

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
	private async ValueTask<IEnumerable<OutboundMessage>> ClaimBatchAsync(ProcessorTenure tenure, int batchSize, CancellationToken cancellationToken)
	{
		if (!tenure.TryAdmit()) { return []; }
		try
		{
			if (_fencingActive && FencedStore is { } fenced && tenure.Token is { } token)
			{
				return await fenced.GetUnsentMessagesAsync(batchSize, token, cancellationToken).ConfigureAwait(false);
			}
			GuardActiveGateHasFencingToken(tenure);
			return await _outboxStore.GetUnsentMessagesAsync(batchSize, cancellationToken).ConfigureAwait(false);
		}
		catch (OutboxFenceRefusedException failure)
		{
			tenure.Refuse(failure);
			return [];
		}
	}

	/// <summary>
	/// Marks a message sent, presenting the leadership token when both a tenure and a fencing-capable store
	/// exist.
	/// </summary>
	private ValueTask<OutboxCompletionOutcome> MarkSentFencedAsync(ProcessorTenure tenure, OutboxClaimEvidence claim, CancellationToken cancellationToken)
	{
		if (_fencingActive)
		{
			GuardActiveGateHasFencingToken(tenure);
			return ((IFencedClaimScopedOutboxStore)_outboxStore.GetService(typeof(IFencedClaimScopedOutboxStore))!)
				.MarkSentAsync(claim.MessageId, new OutboxWriteAuthority(tenure.Token!.Value, claim.ClaimIdentity), cancellationToken);
		}
		return ((IClaimScopedOutboxStore)_outboxStore.GetService(typeof(IClaimScopedOutboxStore))!)
			.MarkSentAsync(claim.MessageId, claim.ClaimIdentity, cancellationToken);
	}

	/// <summary>
	/// Marks a message sent, records what the store did, and returns the outcome for the caller to act on.
	/// </summary>
	/// <returns>What the store did. Only <see cref="OutboxCompletionOutcome.Applied"/> means the mark landed,
	/// and only <see cref="OutboxCompletionOutcome.FenceRefused"/> means the caller must stop writing.</returns>
	/// <remarks>
	/// A tenure-scoped refusal is recorded before logging and stops new admissions across the run and
	/// later processors sharing its authority domain. Row-scoped outcomes cost only this completion and
	/// do not stop the run. Transport acceptance remains distinct from durable completion. Captured
	/// authority is never replaced by a newer live gate token when completing an older claim.
	/// </remarks>
	private async ValueTask<OutboxCompletionOutcome> MarkSentAndObserveAsync(ProcessorTenure tenure, OutboxClaimEvidence claim, CancellationToken cancellationToken)
	{
		var messageId = claim.MessageId;
		if (!tenure.TryAdmit()) { return OutboxCompletionOutcome.FenceRefused; }
		OutboxCompletionOutcome outcome;
		try { outcome = await MarkSentFencedAsync(tenure, claim, cancellationToken).ConfigureAwait(false); }
		catch (OutboxFenceRefusedException failure)
		{
			tenure.Refuse(failure);
			throw;
		}
		if (outcome is OutboxCompletionOutcome.FenceRefused) { tenure.Refuse(); }

		switch (outcome)
		{
			case OutboxCompletionOutcome.Applied:
				break;

			case OutboxCompletionOutcome.FenceRefused:
				LogFencedMarkSentRefusedOutcome(messageId);
				break;

			default:
				LogMarkSentNotApplied(messageId, outcome.ToString());
				break;
		}

		return outcome;
	}

	/// <summary>
	/// Composition guard: a leader gate that is <em>present</em> but yields no fencing token MUST fail closed
	/// on the fenced write path — it must never fall through to the unfenced drain. Draining without a fence
	/// under an active gate is the "looks fenced but isn't" split-brain window a superseded leader exploits.
	/// With a required fencing-token provider a leader always mints a token (<c>&gt;= 1</c>), so reaching this
	/// state under an active gate is a defect; refuse to drain rather than drain unfenced. When no gate is
	/// configured (<c>_leaderGate is null</c>) a null token is the legitimate unfenced path and drains normally.
	/// </summary>
	/// <remarks>
	/// Validate the captured run authority, not a later sample of the live gate. An active fenced run
	/// never falls back to an unfenced operation. Capture normally refuses a missing token before any
	/// claim; this guard retains the same fail-closed invariant at the mutation boundary.
	/// </remarks>
	private void GuardActiveGateHasFencingToken(ProcessorTenure tenure)
	{
		if (_fencingActive && tenure.Token is null)
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

	private readonly Lock _lifecycleLock = new();
	private readonly CancellationTokenSource _lifetimeCancellation = new();
	private TaskCompletionSource? _activeRun;
	private TaskCompletionSource? _disposal;

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
	/// <param name="deadLetterQueue"> Explicit dead letter queue for failed messages. Null is rejected; pass NullDeadLetterQueue.Instance to intentionally discard. </param>
	/// <param name="circuitBreakerRegistry">
	/// Required circuit breaker registry for transport resilience. It is required rather than optional
	/// because a drain that silently ran without a circuit breaker was indistinguishable from one that had
	/// a working breaker, and the difference only became visible under a failing transport.
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
	public OutboxProcessor(
		IOptions<OutboxDeliveryOptions> options,
		IOutboxStore outboxStore,
		DispatchJsonSerializer serializer,
		IServiceProvider serviceProvider,
		ILogger<OutboxProcessor> logger,
		ITransportCircuitBreakerRegistry circuitBreakerRegistry,
		IBinaryEnvelopeDeserializer? envelopeDeserializer = null,
		IDeadLetterQueue? deadLetterQueue = null,
		IBackoffCalculator? backoffCalculator = null,
		IOptions<DeliveryGuaranteeOptions>? deliveryGuaranteeOptions = null,
		Excalibur.Dispatch.ILeaderProcessingGate? leaderGate = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(outboxStore);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(circuitBreakerRegistry);
		ArgumentNullException.ThrowIfNull(logger);

		_deadLetterQueue = deadLetterQueue
			?? throw new InvalidOperationException(Microsoft.Extensions.DependencyInjection.OutboxServiceCollectionExtensions.MissingDeadLetterQueueMessage);

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
		_failureClassifier = serviceProvider.GetService(typeof(IMessageFailureClassifier)) as IMessageFailureClassifier
			?? new DefaultMessageFailureClassifier();
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

		// Explicit discard remains available and is observable.
		_circuitBreakerRegistry = circuitBreakerRegistry;

		if (deadLetterQueue is NullDeadLetterQueue)
		{
			LogDeadLetterQueueNotConfigured();
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
			outboxStore,
			requiresDeadLetterCompletion: true);

		if (_options.BatchProcessing.EnableDynamicBatchSizing)
		{
			_batchSizeCalculator = new DynamicBatchSizeCalculator(
				_options.BatchProcessing.MinBatchSize,
				_options.BatchProcessing.MaxBatchSize,
				_options.ConsumerBatchSize);
		}
	}

	/// <summary>
	/// Sets the processor identity. Repeating the same identity is allowed; replacing an established identity is rejected.
	/// Processing starts when DispatchPendingMessagesAsync is called. Sequential drains are supported; overlapping drains are rejected.
	/// </summary>
	/// <param name="dispatcherId"> Unique identifier for this dispatcher instance, used for message ownership and coordination. </param>
	/// <exception cref="ArgumentException"> Thrown when dispatcherId is null, empty, or whitespace. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when processor is already initialized or in invalid state. </exception>
	public void Init(string dispatcherId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherId);

		lock (_lifecycleLock)
		{
			ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);
			if (_dispatcherId is not null && !string.Equals(_dispatcherId, dispatcherId, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("A processor's dispatcher identity cannot change after initialization.");
			}
			_dispatcherId = dispatcherId;
		}
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
		TaskCompletionSource completion;
		CancellationTokenSource runCancellation;
		var budget = _options.PerRunTotal;
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budget);
		lock (_lifecycleLock)
		{
			ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);
			if (string.IsNullOrWhiteSpace(_dispatcherId))
			{
				throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, AttemptedToRunWithoutCallingInitFormat,
					nameof(DispatchPendingMessagesAsync), nameof(Init)));
			}
			if (_activeRun is not null)
			{
				throw new InvalidOperationException("An outbox drain is already running on this processor.");
			}
			runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellation.Token);
			_outboxMessages = Channel.CreateBounded<IOutboxMessage>(new BoundedChannelOptions(_queueCapacity)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleWriter = true,
				SingleReader = true,
				AllowSynchronousContinuations = false,
			});
			_producerStopped = false;
			completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			_activeRun = completion;
		}

		try
		{
			using var tenure = new ProcessorTenure(_fencingActive, _leaderGate, runCancellation.Token);
			if (!tenure.TryAdmit())
			{
				LogTenureStopped(tenure.Token);
				return 0;
			}
			// Neither worker may outlive the run, even when its sibling faults under backpressure.
			var producer = SuperviseAsync(() => ProducerLoopAsync(tenure, budget, runCancellation.Token), runCancellation);
			var consumerResult = 0;
			var consumer = SuperviseAsync(async () =>
			{
				consumerResult = await ConsumerLoopAsync(tenure, runCancellation.Token).ConfigureAwait(false);
			}, runCancellation);
			await Task.WhenAll(producer, consumer).ConfigureAwait(false);
			runCancellation.Token.ThrowIfCancellationRequested();
			if (tenure.IsStopped) { LogTenureStopped(tenure.Token); }
			return consumerResult;
		}
		catch (OutboxFencingTokenUnavailableException)
		{
			runCancellation.Token.ThrowIfCancellationRequested();
			LogTenureStopped(null);
			return 0;
		}
		finally
		{
			runCancellation.Dispose();
			lock (_lifecycleLock)
			{
				_activeRun = null;
				completion.SetResult();
			}
		}
	}

	private static async Task SuperviseAsync(Func<Task> worker, CancellationTokenSource cancellation)
	{
		await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
		try
		{
			await worker().ConfigureAwait(false);
		}
		catch (Exception failure)
		{
			try
			{
				await cancellation.CancelAsync().ConfigureAwait(false);
			}
			catch (Exception cancellationFailure)
			{
				throw new AggregateException(failure, cancellationFailure);
			}
			throw;
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		TaskCompletionSource completion;
		Task? active;
		bool ownsDisposal;
		lock (_lifecycleLock)
		{
			ownsDisposal = _disposal is null;
			_disposal ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			completion = _disposal;
			_disposedFlag = 1;
			active = _activeRun?.Task;
		}
		if (ownsDisposal)
		{
			try
			{
				try
				{
					await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);
				}
				finally
				{
					if (active is not null)
					{
						await active.ConfigureAwait(false);
					}
					_batchMetrics.Dispose();
					_lifetimeCancellation.Dispose();
				}
				completion.SetResult();
			}
			catch (Exception failure)
			{
				completion.SetException(failure);
			}
		}
		await completion.Task.ConfigureAwait(false);
		GC.SuppressFinalize(this);
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
	/// <param name="claim"> The acquisition evidence captured before message conversion. </param>
	/// <returns> An IOutboxMessage implementation. </returns>
	private IOutboxMessage ConvertToOutboxMessageWithEnvelopeSupport(OutboundMessage outboundMessage, OutboxClaimEvidence claim)
	{
		return new ClaimedOutboxMessage((OutboxMessage)ConvertClaimedEnvelope(outboundMessage), claim);
	}

	private sealed record ClaimedOutboxMessage : OutboxMessage
	{
		[SetsRequiredMembers]
		internal ClaimedOutboxMessage(OutboxMessage message, OutboxClaimEvidence claim) : base(message) => Claim = claim;
		internal OutboxClaimEvidence Claim { get; }
	}

	private static OutboxClaimEvidence ClaimOf(IOutboxMessage message) => message is ClaimedOutboxMessage owned
		? owned.Claim
		: throw new InvalidOperationException("Outbox worker input has no captured acquisition evidence.");

	private IOutboxMessage ConvertClaimedEnvelope(OutboundMessage outboundMessage)
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
					LastError = outboundMessage.LastError,
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
			LastError = outboundMessage.LastError,
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

	private async Task ProducerLoopAsync(ProcessorTenure tenure, int budget, CancellationToken cancellationToken)
	{
		try
		{
			var totalQueued = 0;
			var reachedLimit = false;

			// Guards against queueing the same message twice within this run. Keyed on MessageId, which
			// really is the outbox's unit of work. Cross-instance exclusion is not this set's job: it comes
			// from the store's reservation.
			var queuedThisRun = new HashSet<string>(StringComparer.Ordinal);

			while (!cancellationToken.IsCancellationRequested && !tenure.IsStopped && !reachedLimit)
			{
				var availableSlots = Math.Max(0, _queueCapacity - _outboxMessages.Reader.Count);
				var remainingMessages = budget - totalQueued;
				var batchSize = Math.Min(availableSlots, Math.Min(_options.ProducerBatchSize, remainingMessages));

				if (availableSlots == 0)
				{
					// Event-driven wait: block until channel has capacity instead of polling
					if (!await _outboxMessages.Writer.WaitToWriteAsync(tenure.QueueToken).ConfigureAwait(false))
					{
						// Channel completed, exit producer
						break;
					}

					continue;
				}

				var batch = await ReserveBatchRecordsAsync(tenure, batchSize, cancellationToken).ConfigureAwait(false);

				if (batch.Count == 0)
				{
					LogProducerIdleExiting(_dispatcherId!);

					break;
				}

				LogEnqueuingBatchRecords(batch.Count);

				var newMessages = 0;
				foreach (var outboxRecord in batch)
				{
					if (!tenure.TryAdmit()) { break; }
					if (queuedThisRun.Add(outboxRecord.MessageId))
					{
						await _outboxMessages.Writer.WriteAsync(outboxRecord, tenure.QueueToken).ConfigureAwait(false);
						newMessages++;
					}
				}

				totalQueued += batch.Count;
				reachedLimit = newMessages == 0 || totalQueued >= budget;

				BackgroundServiceMetrics.RecordMessagesProcessed(BackgroundServiceTypes.Outbox, BackgroundServiceOperations.Pending, batch.Count);
			}
		}
		catch (OperationCanceledException) when (tenure.QueueToken.IsCancellationRequested)
		{
			if (!tenure.IsStopped) { LogOutboxProducerCanceled(); }
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

	private async Task<int> ConsumerLoopAsync(ProcessorTenure tenure, CancellationToken cancellationToken)
	{
		var totalProcessedCount = 0;

		try
		{
			while (!cancellationToken.IsCancellationRequested && !tenure.IsStopped)
			{
				if (_producerStopped && _outboxMessages.Reader.Count == 0)
				{
					LogConsumerExiting();

					break;
				}

				var batchSize = _batchSizeCalculator?.CurrentBatchSize ?? _options.ConsumerBatchSize;
				var batch = await ChannelBatchUtilities.DequeueBatchAsync(_outboxMessages.Reader, batchSize, tenure.QueueToken)
					.ConfigureAwait(false);

				if (batch.Length == 0)
				{
					continue;
				}

				var stopwatch = ValueStopwatch.StartNew();
				var batchSuccessful = 0;

				if (_options.BatchProcessing.ParallelProcessingDegree > 1)
				{
					// Parallel batch processing
					var processedCount = await ProcessBatchParallelAsync(tenure, batch, cancellationToken).ConfigureAwait(false);
					totalProcessedCount += processedCount;
					batchSuccessful = processedCount;
				}
				else
				{
					// Sequential processing (backward compatibility)
					foreach (var record in batch)
					{
						if (await DispatchReservedRecordAsync(tenure, record, cancellationToken).ConfigureAwait(false))
						{
							totalProcessedCount++;
							batchSuccessful++;
						}
					}
				}

				// Record batch metrics
				var duration = stopwatch.Elapsed;
				if (!tenure.IsStopped)
				{
					_batchMetrics.RecordBatchCompleted(batch.Length, batchSuccessful,
						batch.Length - batchSuccessful, duration, ProcessorTypeTags);
				}

				BackgroundServiceMetrics.RecordMessagesProcessed(BackgroundServiceTypes.Outbox, BackgroundServiceOperations.Dispatch, totalProcessedCount);
			}
		}
		catch (OperationCanceledException) when (tenure.QueueToken.IsCancellationRequested)
		{
			if (!tenure.IsStopped) { LogConsumerCanceled(); }
		}
		catch (Exception ex)
		{
			LogErrorInConsumerLoop(ex);

			throw;
		}

		if (tenure.IsStopped) { return totalProcessedCount; }
		LogOutboxProcessingCompleted(totalProcessedCount);

		BackgroundServiceMetrics.RecordProcessingCycle(BackgroundServiceTypes.Outbox, totalProcessedCount > 0 ? BackgroundServiceResults.Success : BackgroundServiceResults.Empty);

		return totalProcessedCount;
	}

	private async Task<IReadOnlyCollection<IOutboxMessage>> ReserveBatchRecordsAsync(ProcessorTenure tenure, int batchSize, CancellationToken cancellationToken)
	{
		var records = await ClaimBatchAsync(tenure, batchSize, cancellationToken).ConfigureAwait(false);

		// Convert OutboundMessage to IOutboxMessage with envelope format support. Pre-size and foreach-add
		// instead of Select().ToList().AsReadOnly() to avoid the per-batch Select-iterator +
		// ReadOnlyCollection-wrapper allocations; the List is returned as the read-only interface.
		var capacity = records.TryGetNonEnumeratedCount(out var count) ? count : batchSize;
		var outboxMessages = new List<IOutboxMessage>(capacity);
		var claimed = new List<(OutboundMessage Message, OutboxClaimEvidence Claim)>(capacity);
		foreach (var record in records)
		{
			claimed.Add((record, new OutboxClaimEvidence(record.Id, record.DispatcherId!)));
		}
		foreach (var (record, claim) in claimed)
		{
			outboxMessages.Add(ConvertToOutboxMessageWithEnvelopeSupport(record, claim));
		}

		return outboxMessages;
	}

	private async Task<bool> DispatchReservedRecordAsync(ProcessorTenure tenure, IOutboxMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		if (!tenure.TryAdmit()) { return false; }

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
		if (await ResumePendingDispositionAsync(tenure, message, cancellationToken).ConfigureAwait(false))
		{
			return false;
		}
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
			await MarkFailedForClaimAsync(tenure, ClaimOf(message).MessageId, ClaimOf(message).ClaimIdentity, ErrorConstants.RetryAttempt, message.Attempts, applyBackoff: false, cancellationToken).ConfigureAwait(false);
			return false;
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

			await RouteObservedFailureAsync(tenure, message, DeadLetterReason.DeserializationFailed, ex, message.Attempts, "Poison", cancellationToken)
				.ConfigureAwait(false);

			return false;
		}

		// Set the instant the transport accepts the message, so the catch clauses below can tell a failure
		// TO DELIVER from a failure AFTER delivering. Without it both arrive at the same catch and a
		// successful delivery whose bookkeeping failed is recorded as max-retries-exceeded.
		var dispatched = false;
		DispatchAttemptFailure? failure = null;

		try
		{
			LogDispatchingOutboxRecord(message.MessageId, _dispatcherId!);

			// Execute dispatch through circuit breaker. It records the outcome -- success or failure --
			// itself; recording it again here would count every delivery failure twice and open the
			// circuit at half the configured threshold.
			if (!tenure.TryAdmit()) { return false; }
			failure = await circuitBreaker.ExecuteAsync(ct => DispatchAsync(message, prepared, ct),
				static outcome => outcome is not null, cancellationToken).ConfigureAwait(false);
			if (failure is null)
			{
				dispatched = true;
				LogSuccessfullyDispatchedOutboxRecord(message.MessageId, _dispatcherId!);
				BackgroundServiceMetrics.RecordProcessingDuration(BackgroundServiceTypes.Outbox, stopwatch.Elapsed.TotalMilliseconds);

				// The refusal is REPORTED now rather than thrown, so the "sent" log is conditional on the
				// store having actually applied the mutation. It used to be unconditional because the only
				// way past the line above was success; a reported refusal reaching it would log a mark that
				// never happened, which is the silent-success shape this member exists to remove.
				if (await MarkSentAndObserveAsync(tenure, ClaimOf(message), cancellationToken).ConfigureAwait(false)
					is OutboxCompletionOutcome.Applied)
				{
					LogMarkedOutboxRecordSent(message.MessageId);
				}
			}
		}
		catch (OperationCanceledException) when (!dispatched && cancellationToken.IsCancellationRequested)
		{
			// Shutdown/caller cancellation is not a failed delivery and must not consume its retry budget.
			// Leave the claim intact for ordinary lease recovery instead of recording a synthetic failure.
			throw;
		}
		catch (CircuitBreakerOpenException) when (!dispatched)
		{
			// Circuit opened mid-dispatch: same transient short-circuit as the pre-check. Leave
			// re-claimable with the attempt count UNCHANGED, never dead-letter (no attempt consumed).
			LogCircuitBreakerOpen(message.MessageType, message.MessageId);
			await MarkFailedForClaimAsync(tenure, ClaimOf(message).MessageId, ClaimOf(message).ClaimIdentity, ErrorConstants.RetryAttempt, message.Attempts, applyBackoff: false, cancellationToken).ConfigureAwait(false);
		}
		catch (OutboxFenceRefusedException ex)
		{
			tenure.Refuse(ex);
			// The fence refused this tenure's mark-sent because a newer leader has taken over. This is
			// NOT a delivery failure -- the message may already be delivered by the current leader, or
			// still pending for it -- so it must not be retried, backed off, or dead-lettered by a
			// superseded tenure. Stop new admissions for this captured tenure, make no alternate write for this message, and
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
			failure = new DispatchAttemptFailure(ex.Message, _failureClassifier.Classify(ex), ex);
		}
		if (failure is not null)
		{
			return await HandleFailedDispatchAsync(tenure, message, attempt, failure, activity, cancellationToken).ConfigureAwait(false);
		}

		return dispatched;
	}

	private async Task<bool> HandleFailedDispatchAsync(ProcessorTenure tenure, IOutboxMessage message, int attempt,
		DispatchAttemptFailure failure, Activity? activity, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var fault = failure.OriginalFault ?? new InvalidOperationException(failure.ErrorMessage);
		LogErrorDispatchingOutboxRecord(message.MessageId, _dispatcherId!, fault);
		activity?.SetStatus(ActivityStatusCode.Error, failure.ErrorMessage);
		BackgroundServiceMetrics.RecordMessagesFailed(BackgroundServiceTypes.Outbox, BackgroundServiceOperations.Dispatch, 1);
		if (failure.Kind == MessageFailureKind.Unknown)
		{
			await RetainUnknownFailureAsync(tenure, message, attempt, failure.ErrorMessage, cancellationToken).ConfigureAwait(false);
		}
		else if (GetFailedDispatchReason(failure, attempt) is { } reason)
		{
			await RouteObservedFailureAsync(tenure, message, reason, fault, attempt, failure.Kind?.ToString() ?? "Unknown", cancellationToken).ConfigureAwait(false);
		}
		else
		{
			await MarkFailedForClaimAsync(tenure, ClaimOf(message).MessageId, ClaimOf(message).ClaimIdentity, failure.ErrorMessage, attempt,
				applyBackoff: _deliveryGuaranteeOptions.EnableAutomaticRetry, cancellationToken).ConfigureAwait(false);
		}
		return false;
	}

	private async Task RetainUnknownFailureAsync(ProcessorTenure tenure, IOutboxMessage message, int attempts,
		string error, CancellationToken cancellationToken)
	{
		var evidence = new OutboxFailureEvidence
		{
			Attempts = attempts, Classification = "Unknown", Reason = nameof(DeadLetterReason.Unknown), Error = error,
		};
		// Applied releases this acquisition; refusal/unknown acknowledgement never authorizes
		// a queue copy, retirement, or a second write with released authority.
		using var tenant = TenantContextHolder.BeginScope(KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId);
		await MarkFailedForClaimAsync(tenure, ClaimOf(message).MessageId, ClaimOf(message).ClaimIdentity,
			evidence.Encode(), attempts, applyBackoff: false, cancellationToken).ConfigureAwait(false);
	}

	private DeadLetterReason? GetFailedDispatchReason(DispatchAttemptFailure failure, int attempt) => failure.Kind switch
	{
		MessageFailureKind.Permanent => DeadLetterReason.UnhandledException,
		MessageFailureKind.Poison => DeadLetterReason.PoisonMessage,
		_ when attempt >= _options.MaxAttempts => DeadLetterReason.MaxRetriesExceeded,
		_ => null,
	};

	/// <summary>
	/// Routes a message to the dead-letter queue without letting a routing fault escape to the caller.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This exists for the SEQUENTIAL drain path, which has no batching harness to contain a throw.</b>
	/// The parallel path defers its routing to a guarded loop, and the per-message delegate it runs under is
	/// wrapped by the batching harness, which records a throw against that one message and carries on. The
	/// sequential path has neither: it is a bare <c>foreach</c> over the claimed batch, so an exception
	/// escaping here unwinds the loop, STRANDS every record after this one, and skips the batch completion
	/// that follows -- messages this cycle already delivered are never marked sent and are delivered again.
	/// </para>
	/// <para>
	/// A fault while disposing of a failed message must not cause the redelivery of a successful one. Those
	/// messages are unrelated; only the control flow joined them.
	/// </para>
	/// <para>
	/// Cancellation is deliberately NOT swallowed. A routing fault means "this one message could not be
	/// dead-lettered, carry on with the rest"; cancellation means "stop draining", and continuing to the next
	/// record during shutdown would be wrong. Either way the message stays claimed and un-dead-lettered, so
	/// the next cycle retries it -- at worst a delayed dead letter, where propagating would have been a
	/// duplicate delivery of a different message.
	/// </para>
	/// </remarks>
	private async Task<DeadLetterRoutingOutcome> TryRouteToDeadLetterQueueAsync(ProcessorTenure tenure,
		IOutboxMessage message,
		DeadLetterReason reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		try
		{
			return await RouteToDeadLetterQueueAsync(tenure, message, reason, exception, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogDeadLetterRoutingFailed(message.MessageId, ex);
			return DeadLetterRoutingOutcome.Faulted;
		}
	}

	private enum DeadLetterRoutingOutcome { Applied, Refused, Faulted }

	private async Task RouteObservedFailureAsync(ProcessorTenure tenure, IOutboxMessage original, DeadLetterReason reason, Exception? fault,
		int attempts, string classification, CancellationToken cancellationToken)
	{
		// This owned projection carries the observed count while the original retains its claim authority.
		var envelope = original is OutboxMessage record ? record with { Attempts = attempts }
			: new OutboxMessage(original.MessageId, original.MessageType, original.MessageMetadata,
			original.MessageBody, original.CreatedAt, original.ExpiresAt)
		{
			Attempts = attempts, DispatcherId = original.DispatcherId, DispatcherTimeout = original.DispatcherTimeout,
			TenantId = original.TenantId, LastError = original.LastError,
		};
		var outcome = await TryRouteToDeadLetterQueueAsync(tenure, envelope, reason, fault, cancellationToken).ConfigureAwait(false);
		if (outcome != DeadLetterRoutingOutcome.Faulted)
		{
			return;
		}
		var evidence = new OutboxFailureEvidence
		{
			Attempts = attempts, Classification = classification, Reason = reason.ToString(),
			Error = fault?.Message ?? original.LastError ?? reason.ToString(),
		};
		using var tenant = TenantContextHolder.BeginScope(KeyedTenantPartition.FromStoredValue(original.TenantId).TenantId);
		try
		{
			await MarkFailedForClaimAsync(tenure, ClaimOf(original).MessageId, ClaimOf(original).ClaimIdentity, evidence.Encode(), attempts,
				applyBackoff: false, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) { LogDeadLetterRoutingFailed(original.MessageId, ex); }
	}

	private async Task<bool> ResumePendingDispositionAsync(ProcessorTenure tenure, IOutboxMessage message, CancellationToken cancellationToken)
	{
		if (OutboxFailureEvidence.TryRead(message.LastError, out var evidence))
		{
			if (evidence is { Classification: "Unknown", Reason: "Unknown" })
			{
				// Exact retained marker, before increment/max-budget handling. Unknown with
				// MaxRetriesExceeded remains its ordinary exhausted disposition.
				return true;
			}
			var reason = Enum.Parse<DeadLetterReason>(evidence!.Reason);
			await RouteObservedFailureAsync(tenure, message, reason, new InvalidOperationException(evidence.Error),
				message.Attempts, evidence.Classification, cancellationToken).ConfigureAwait(false);
			return true;
		}
		if (OutboxFailureEvidence.IsPending(message.LastError))
		{
			// An unreadable or future-version disposition never grants permission to deliver again.
			LogDeadLetterRoutingFailed(message.MessageId, new InvalidOperationException("Pending outbox disposition evidence is unreadable or unsupported."));
			return true;
		}
		if (message.Attempts < _options.MaxAttempts)
		{
			return false;
		}
		await RouteObservedFailureAsync(tenure, message, DeadLetterReason.MaxRetriesExceeded,
			new InvalidOperationException(message.LastError ?? "Max retries exceeded"), message.Attempts, "Unknown",
			cancellationToken).ConfigureAwait(false);
		return true;
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
	private async Task<DeadLetterRoutingOutcome> RouteToDeadLetterQueueAsync(ProcessorTenure tenure,
		IOutboxMessage message,
		DeadLetterReason reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		if (!tenure.TryAdmit()) { return DeadLetterRoutingOutcome.Refused; }
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
			enqueuedEntryId = await _deadLetterQueue.EnqueueAsync<IOutboxMessage>(
				CreateDeadLetterEnvelope((OutboxMessage)message),
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
		return await MarkOutboxMessageDeadLetteredAsync(tenure,
			ClaimOf(message),
			_deadLetterQueue is NullDeadLetterQueue ? $"DISCARDED (no DLQ): {reasonText}" : $"Moved to DLQ: {reasonText}",
			message.Attempts,
			enqueuedEntryId,
			cancellationToken).ConfigureAwait(false);
	}

	// Keep private work/authority records inside the worker. Existing DLQ consumers continue to see
	// the public envelope runtime type and require no serializer metadata for an internal derived type.
	private static OutboxMessage CreateDeadLetterEnvelope(OutboxMessage message) => new()
	{
		MessageId = message.MessageId,
		MessageType = message.MessageType,
		MessageMetadata = message.MessageMetadata,
		MessageBody = message.MessageBody,
		CreatedAt = message.CreatedAt,
		ExpiresAt = message.ExpiresAt,
		Attempts = message.Attempts,
		LastError = message.LastError,
		DispatcherId = message.DispatcherId,
		DispatcherTimeout = message.DispatcherTimeout,
		TenantId = message.TenantId,
		Destination = message.Destination,
		CorrelationId = message.CorrelationId,
		CausationId = message.CausationId,
		Priority = message.Priority,
		ScheduledAt = message.ScheduledAt,
		PartitionKey = message.PartitionKey,
		GroupKey = message.GroupKey,
		SequenceNumber = message.SequenceNumber,
		TargetTransports = message.TargetTransports,
		IsMultiTransport = message.IsMultiTransport,
	};

	/// <summary>
	/// Transitions an outbox message to the terminal <see cref="OutboxStatus.DeadLettered"/> status via the
	/// store's acquisition-scoped dead-letter capability.
	/// </summary>
	/// <remarks>
	/// The capability is guaranteed by the startup presence guard (a polling outbox registered with a store
	/// that lacks acquisition-scoped dead-letter completion fails fast). If the capability is
	/// nonetheless absent, this fails loud rather than silently leaving the message re-claimable.
	/// </remarks>
	private async ValueTask<DeadLetterRoutingOutcome> MarkOutboxMessageDeadLetteredAsync(ProcessorTenure tenure,
		OutboxClaimEvidence claim,
		string reason,
		int attemptCount,
		Guid? enqueuedEntryId,
		CancellationToken cancellationToken)
	{
		var messageId = claim.MessageId;
		if (!tenure.TryAdmit())
		{
			await CompensateDeadLetterEntryAsync(messageId, enqueuedEntryId, "TenureAdmissionRefused").ConfigureAwait(false);
			return DeadLetterRoutingOutcome.Refused;
		}
		// THE FENCED ROUTE IS PREFERRED, and on THIS member the stakes are higher than on any other. The
		// unfenced transition copies the message to the dead-letter table and DELETES the outbox row on a
		// message-id match alone, so a superseded tenure reaching its attempt ceiling destroys a row a live
		// successor still holds and has not delivered. Every other completion leaves the row recoverable.
		//
		// Both tenure and immutable acquisition evidence guard the terminal transition.
		if (_fencingActive
			&& tenure.Token is { } deadLetterToken
			&& _outboxStore.GetService(typeof(IFencedDeadLetterableOutboxStore))
				is IFencedDeadLetterableOutboxStore fencedDeadLetterable)
		{
			OutboxCompletionOutcome outcome;
			try
			{
				outcome = await fencedDeadLetterable.MarkDeadLetteredAsync(messageId, reason, attemptCount, new OutboxWriteAuthority(deadLetterToken, claim.ClaimIdentity), cancellationToken).ConfigureAwait(false);
			}
			catch (OutboxFenceRefusedException failure)
			{
				tenure.Refuse(failure);
				await CompensateDeadLetterEntryAsync(messageId, enqueuedEntryId, "FenceRefused").ConfigureAwait(false);
				LogFencedDeadLetterRefused(messageId, "FenceRefused");
				return DeadLetterRoutingOutcome.Refused;
			}
			if (outcome is OutboxCompletionOutcome.FenceRefused) { tenure.Refuse(); }

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

				// NOT the drain's token. See the remarks on the method: on shutdown that token is already
				// cancelled, so passing it makes the withdrawal unable to even attempt exactly when we have
				// just learned it is required.
				await CompensateDeadLetterEntryAsync(messageId, enqueuedEntryId, outcome.ToString())
					.ConfigureAwait(false);
				return DeadLetterRoutingOutcome.Refused;
			}

			return DeadLetterRoutingOutcome.Applied;
		}

		// Active fencing never falls through to the unfenced capability. A capability that disappears
		// after startup is a refused mutation; withdraw our own already-enqueued DLQ entry below.
		if (_fencingActive)
		{
			tenure.Stop();
			LogFencedDeadLetterRefused(messageId, "FencedCapabilityUnavailable");

			// NOT the drain's token, for the reason given on CompensateDeadLetterEntryAsync: on shutdown it
			// is already cancelled, which would disable the withdrawal exactly when it is required.
			await CompensateDeadLetterEntryAsync(messageId, enqueuedEntryId, "FencingTokenUnavailable")
				.ConfigureAwait(false);

			return DeadLetterRoutingOutcome.Refused;
		}

		// Discovered through GetService, never by casting. A cast sees only the OUTERMOST type, so a store
		// that implements this capability but sits behind any decorator answers "absent" -- and the
		// startup guard, which probes correctly, answers "present" for the same composition. The
		// disagreement surfaced here as a throw on the one path a retry-exhausted message depends on.
		if (_outboxStore.GetService(typeof(IClaimScopedDeadLetterableOutboxStore)) is not IClaimScopedDeadLetterableOutboxStore deadLetterable)
		{
			throw new InvalidOperationException(
				$"The outbox store '{_outboxStore.GetType().FullName}' does not implement IClaimScopedDeadLetterableOutboxStore; " +
				"the startup capability guard should have prevented this configuration. A retry-exhausted message " +
				"cannot be transitioned to the terminal DeadLettered status and would be re-claimed indefinitely.");
		}

		// The unfenced store refuses too, and until its contract could report that, this branch could not
		// see it. The dead-letter ENTRY was already written above, so an unseen refusal leaves the message
		// simultaneously delivered by whoever holds the claim and sitting unreplayed in the queue, where the
		// shipped redrive selects pending entries by filter -- an operator draining it then re-executes a
		// message that already succeeded, from our bookkeeping rather than any fault of theirs. That is the
		// same hazard the fenced branch documents at length above, and it needed the same withdrawal.
		var unfencedOutcome = await deadLetterable
			.MarkDeadLetteredAsync(messageId, reason, attemptCount, claim.ClaimIdentity, cancellationToken)
			.ConfigureAwait(false);

		// By name, never by excluding the refusals: a member added to the outcome later must not start life
		// as a success on this path.
		if (unfencedOutcome is not OutboxCompletionOutcome.Applied)
		{
			LogDeadLetterRefused(messageId, unfencedOutcome.ToString());

			// NOT the drain's token, for the reason given on CompensateDeadLetterEntryAsync: on shutdown it
			// is already cancelled, which would disable the withdrawal exactly when we have just learned it
			// is required.
			await CompensateDeadLetterEntryAsync(messageId, enqueuedEntryId, unfencedOutcome.ToString())
				.ConfigureAwait(false);

			return DeadLetterRoutingOutcome.Refused;
		}

		return DeadLetterRoutingOutcome.Applied;
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
		string outcome)
	{
		if (enqueuedEntryId is not { } entryId)
		{
			// Nothing was written: either no dead-letter queue is composed, or the enqueue did not run.
			return;
		}

		// Withdrawal lives on the ADMIN surface, not on IDeadLetterQueue. What makes this sound is the TYPE
		// TEST BELOW WITH ITS REPORTED FALLBACK -- not the fact that no decorator over IDeadLetterQueue
		// happens to exist in this framework today.
		//
		// That distinction is deliberate and it is a correction. This comment used to justify the pattern by
		// the absence of decorators, which is a claim about OUR tree and not about a consumer's composition:
		// the interface is PUBLIC, so a consumer may decorate it, and a decorator that forwards the queue
		// members without re-declaring the admin one would make the test fail -- correctly, degrading to the
		// reported-unavailable path. An implementation that is robust by construction should not be defended
		// by a premise a consumer can falsify, because the next reader reasonably concludes the test is
		// redundant and replaces it with a cast that then throws on a composition we invited.
		//
		// So: the test is load-bearing, and keep it. If a consumer supplies a queue without the admin
		// surface, withdrawal is unavailable and that is REPORTED rather than passed over in silence -- which
		// is also the first of the conditions the subsystem's guarantee document names for this invariant,
		// and the one a consumer is most likely to cause, because the queue is theirs to supply.
		if (_deadLetterQueue is not IDeadLetterQueueAdmin admin)
		{
			LogDeadLetterCompensationFailed(messageId, entryId, outcome, exception: null);

			return;
		}

		// THE COMPENSATION CARRIES ITS OWN DEADLINE AND IS NOT CANCELLABLE BY THE DRAIN. It used to take the
		// drain's cancellation token, which made this path deterministically inert on shutdown rather than
		// merely racy: the token is ALREADY cancelled by the time a shutdown reaches here, so the withdrawal
		// could not attempt at all. A compensation exists to restore an invariant after a decision has been
		// taken; making it cancellable by the signal that ended the work abandons the invariant at precisely
		// the moment it is known to be broken.
		//
		// A deadline rather than no limit: this runs on the way out, and an unbounded call to a remote queue
		// would hold shutdown open indefinitely. Bounded and detached is the shape that both completes in the
		// ordinary case and cannot wedge the host.
		using var compensationDeadline = new CancellationTokenSource(DeadLetterCompensationTimeout);

		try
		{
			var purged = await admin.PurgeAsync(entryId, compensationDeadline.Token).ConfigureAwait(false);

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
		catch (OperationCanceledException)
		{
			// The compensation's OWN deadline elapsed - the queue did not answer in time. The entry stays and
			// is reported, which is the same outcome as any other failed withdrawal. This is no longer the
			// shutdown case: shutdown cannot reach here, because the drain's token is not passed in.
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
	/// Typed and thrown tenure refusals block shared admission before logging. Missing authority stops
	/// this run without rejecting an unrelated later token. Claim loss, terminal state and absence remain
	/// row-scoped. Returning from this observer does not mean a mutation applied; it preserves the result
	/// distinction without throwing a routine tenure handover through delivery-failure handling.
	/// </remarks>
	private async ValueTask ObserveFencedCompletionAsync(ProcessorTenure tenure,
		ValueTask<OutboxCompletionOutcome> completion,
		string messageId)
	{
		OutboxCompletionOutcome outcome;
		try { outcome = await completion.ConfigureAwait(false); }
		catch (OutboxFenceRefusedException failure)
		{
			tenure.Refuse(failure);
			LogFencedFailureReportRefused(messageId);
			return;
		}
		if (outcome is OutboxCompletionOutcome.FenceRefused) { tenure.Refuse(); }

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

			case OutboxCompletionOutcome.AlreadyTerminal:
				LogFailureReportFoundTerminalMessage(messageId);
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

			case OutboxCompletionOutcome.AlreadyTerminal:
				LogFailureReportFoundTerminalMessage(messageId);
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

	/// <summary>Reports a failure or retry schedule with captured acquisition evidence and optional run tenure.</summary>
	private async ValueTask MarkFailedForClaimAsync(ProcessorTenure tenure,
		string messageId,
		string? claimIdentity,
		string errorMessage,
		int attempt,
		bool applyBackoff,
		CancellationToken cancellationToken)
	{
		if (!tenure.TryAdmit()) { return; }
		// Resolve actual payload-free capabilities through decorators. Missing claim evidence or
		// capability is a configuration fault, never permission to use a weaker completion API.
		var claimScoped = claimIdentity is { Length: > 0 }
			? _outboxStore.GetService(typeof(IClaimScopedOutboxStore)) as IClaimScopedOutboxStore
			: null;

		// A fenced run must present its captured tenure and claim together.
		if (claimIdentity is { Length: > 0 } claim
			&& _fencingActive
			&& tenure.Token is { } fencingToken
			&& _outboxStore.GetService(typeof(IFencedClaimScopedOutboxStore)) is IFencedClaimScopedOutboxStore fencedScoped)
		{
			var authority = new OutboxWriteAuthority(fencingToken, claim);

			// The schedule is computed here rather than in the store because the backoff policy is the
			// caller's, exactly as on the unfenced route; null means this failure takes no computed schedule.
			var fencedSchedule = applyBackoff
					? (DateTimeOffset?)(DateTimeOffset.UtcNow + _backoffCalculator.CalculateDelay(attempt))
					: null;

			try
			{
				await ObserveFencedCompletionAsync(tenure,
					fencedScoped.MarkFailedAsync(messageId, errorMessage, attempt, fencedSchedule, authority, cancellationToken),
					messageId).ConfigureAwait(false);
			}
			catch (OutboxFenceRefusedException failure)
			{
				tenure.Refuse(failure);
				LogFencedFailureReportRefused(messageId);
			}
			return;
		}

		// A fenced failure requires both its captured tenure and the persisted claim. Missing either
		// authority or the advertised capability is never permission to use an unfenced fallback.
		if (_fencingActive)
		{
			tenure.Stop();
			LogFencedFailureReportRefused(messageId);
			return;
		}

		if (claimScoped is null)
		{
			throw new InvalidOperationException("Outbox worker completion requires a nonblank captured claim and IClaimScopedOutboxStore.");
		}
		if (applyBackoff)
		{
			var nextAttemptAt = DateTimeOffset.UtcNow + _backoffCalculator.CalculateDelay(attempt);
			await MarkFailedForOneClaimAsync(claimScoped.MarkFailedWithBackoffAsync(
				messageId, errorMessage, attempt, nextAttemptAt, claimIdentity!, cancellationToken), messageId).ConfigureAwait(false);
		}
		else
		{
			await MarkFailedForOneClaimAsync(claimScoped.MarkFailedAsync(
				messageId, errorMessage, attempt, claimIdentity!, cancellationToken), messageId).ConfigureAwait(false);
		}
	}

	private async Task<int> ProcessBatchParallelAsync(ProcessorTenure tenure, IOutboxMessage[] batch, CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var acceptedCount = 0;
		var successfulIds = new ConcurrentBag<OutboxClaimEvidence>();
		// ApplyBackoff distinguishes a genuine delivery failure (attempt consumed -> apply the backoff
		// schedule) from a transient circuit-breaker-open short-circuit (attempt UNCHANGED -> no backoff).
		var failedToRetry = new ConcurrentBag<(string MessageId, string? ClaimIdentity, int AttemptCount, bool ApplyBackoff)>();
		var failedToDeadLetter =
			new ConcurrentBag<(string MessageId, IOutboxMessage Message, Exception? Exception, DeadLetterReason Reason, int Attempts, string Classification)>();

		var unknownToRetain = new ConcurrentBag<(IOutboxMessage Message, int Attempts, string Error)>();

		// Process messages in parallel
		_ = await Batching.ProcessBatchAsync(
			batch,
			async (message, ct) =>
			{
				if (!tenure.TryAdmit()) { return; }
				// Per-iteration tenant scope: re-establish this message's originating tenant as ambient so the
				// handler dispatch runs under the right tenant, and (MinimizedWindow) the immediate MarkSentAsync
				// scopes the store's composite (TenantId, Id) key. Wraps the single iteration only — each message
				// may carry a different tenant. Store-read tenant term: folded through the total conversion so
				// an untenanted row binds the reserved untenanted term rather than clearing the ambient,
				// which a multi-tenant store fails closed on.
				using var tenantScope = TenantContextHolder.BeginScope(
					KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId);

				if (await ResumePendingDispositionAsync(tenure, message, ct).ConfigureAwait(false))
				{
					return;
				}
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
					failedToRetry.Add((ClaimOf(message).MessageId, ClaimOf(message).ClaimIdentity, message.Attempts, ApplyBackoff: false));

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
					failedToDeadLetter.Add((message.MessageId, message, ex, DeadLetterReason.DeserializationFailed, message.Attempts, "Poison"));

					return; // Don't throw - a poison row is not a batch delivery failure.
				}

				// See the single-message path: this distinguishes a failure TO DELIVER from a failure AFTER
				// delivering, which otherwise reach the same catch.
				var dispatched = false;

				try
				{
					// Execute dispatch through circuit breaker. It records the outcome itself; see the
					// single-message path above.
					if (!tenure.TryAdmit()) { return; }
					var failure = await circuitBreaker.ExecuteAsync(token => DispatchSingleMessageAsync(message, prepared, token),
						static outcome => outcome is not null, ct).ConfigureAwait(false);
					if (failure is not null)
					{
						ct.ThrowIfCancellationRequested();
						RecordFailedDispatch(message, attempt, failure);
						return;
					}

					dispatched = true;
					_ = Interlocked.Increment(ref acceptedCount);

					// For MinimizedWindow, mark sent immediately after dispatch
					if (_options.DeliveryGuarantee == OutboxDeliveryGuarantee.MinimizedWindow)
					{
						// A REPORTED fence refusal gets the same treatment the thrown one gets in the
						// handler below: return with NO further write. The failure and retry paths in this
						// lambda require separate captured-authority admissions; a superseded tenure cannot use them
						// would mark another tenure's rows failed and restamp their next-attempt instants.
						// Returning is what skips them. A row-scoped outcome is already logged by the
						// observer and does not stop this batch.
						if (await MarkSentAndObserveAsync(tenure, ClaimOf(message), ct).ConfigureAwait(false)
							is OutboxCompletionOutcome.FenceRefused)
						{
							return;
						}
					}
					else
					{
						// For AtLeastOnce, collect for batch completion
						successfulIds.Add(ClaimOf(message));
					}
				}
				catch (OperationCanceledException) when (!dispatched && ct.IsCancellationRequested)
				{
					throw;
				}
				catch (CircuitBreakerOpenException) when (!dispatched)
				{
					// Circuit opened mid-dispatch: transient short-circuit. Leave for retry with the
					// attempt count UNCHANGED (no attempt consumed, never dead-letter). Not re-thrown.
					LogCircuitBreakerOpen(message.MessageType, message.MessageId);
					failedToRetry.Add((ClaimOf(message).MessageId, ClaimOf(message).ClaimIdentity, message.Attempts, ApplyBackoff: false));
				}
				catch (OutboxFenceRefusedException ex)
				{
					tenure.Refuse(ex);
					// MinimizedWindow's immediate MarkSentFencedAsync was refused: a newer leader has taken
					// over. Not a delivery failure -- must not enter failedToRetry or failedToDeadLetter,
					// both of which require their own valid captured-authority admission. Abort this message with no further
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
					ct.ThrowIfCancellationRequested();
					RecordFailedDispatch(message, attempt, new DispatchAttemptFailure(ex.Message, _failureClassifier.Classify(ex), ex));
					throw; // Re-throw for Batching to track
				}
			},
			_options.BatchProcessing.ParallelProcessingDegree,
			_options.BatchProcessing.BatchProcessingTimeout,
			cancellationToken).ConfigureAwait(false);

		void RecordFailedDispatch(IOutboxMessage message, int attempt, DispatchAttemptFailure failure)
		{
			var fault = failure.OriginalFault ?? new InvalidOperationException(failure.ErrorMessage);
			LogErrorDispatchingOutboxRecord(message.MessageId, _dispatcherId!, fault);
			if (failure.Kind == MessageFailureKind.Unknown)
			{
				unknownToRetain.Add((message, attempt, failure.ErrorMessage));
			}
			else if (GetFailedDispatchReason(failure, attempt) is { } reason)
			{
				failedToDeadLetter.Add((message.MessageId, message, fault, reason, attempt, failure.Kind?.ToString() ?? "Unknown"));
			}
			else
			{
				if (_deliveryGuaranteeOptions.EnableAutomaticRetry)
				{
					LogRetryWithBackoff(message.MessageId, attempt, _backoffCalculator.CalculateDelay(attempt).TotalMilliseconds);
				}
				failedToRetry.Add((ClaimOf(message).MessageId, ClaimOf(message).ClaimIdentity, attempt, ApplyBackoff: _deliveryGuaranteeOptions.EnableAutomaticRetry));
			}
		}

		foreach (var (message, attempts, error) in unknownToRetain)
		{
			if (!tenure.TryAdmit()) { break; }
			try
			{
				await RetainUnknownFailureAsync(tenure, message, attempts, error, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) { break; }
			catch (Exception ex) { LogDeadLetterRoutingFailed(message.MessageId, ex); }
		}

		// Route messages to DLQ.
		//
		// EVERY ITERATION IS GUARDED, AND THE REASON IS THE BLOCK BELOW RATHER THAN THIS ONE. An unguarded
		// throw out of this loop did two things, and the second is the expensive one: it stranded the
		// remaining dead letters, AND it skipped the batch-completion block that follows -- so messages this
		// cycle had ALREADY DELIVERED were never marked sent, and the next drain delivered them again. A
		// fault while disposing of a failed message must not cause the redelivery of a successful one; those
		// messages are unrelated, and only the control flow joined them.
		//
		// WHICH OF THE TWO CLAUSES BELOW IS LOAD-BEARING, measured rather than assumed, because a reader
		// planting a mutation here will otherwise aim at the wrong one. The property is carried by
		// TryRouteToDeadLetterQueueAsync, which converts every NON-cancellation fault into a returned
		// outcome before it can reach this loop -- so the general catch below is unreachable, and deleting
		// it changes no observable behaviour and reddens no arm. The catch (OperationCanceledException) IS
		// reachable: that one is deliberately rethrown to this loop, and breaking rather than propagating is
		// what stops a shutdown skipping the completion block. The general clause is kept as a backstop for
		// a future caller that reaches this loop without the Try wrapper; it is not what makes the property
		// hold today, and the arms for it mutate the wrapper instead.
		foreach (var (messageId, message, exception, reason, attempts, classification) in failedToDeadLetter)
		{
			if (!tenure.TryAdmit()) { break; }
			try
			{
				await RouteObservedFailureAsync(tenure, message, reason, exception, attempts, classification, cancellationToken).ConfigureAwait(false);
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
				await PerformBatchDatabaseOperationsAsync(tenure, successfulIdsOnly, [], new List<(OutboxClaimEvidence, int)>(), cancellationToken)
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
					foreach (var claim in successfulIdsOnly)
					{
						var id = claim.MessageId;
						if (!tenure.TryAdmit()) { return acceptedCount; }
						refusedId = id;

						// A REPORTED fence refusal returns from HERE for the same reason the thrown one
						// does, stated in the handler below: the failure-path writes after this point are
						// separately admitted. Returning retains the accepted transport count. A row-scoped outcome is
						// already logged by the observer and must NOT stop the loop -- the ids after it
						// belong to this live tenure and still need marking.
						if (await MarkSentAndObserveAsync(tenure, claim, cancellationToken).ConfigureAwait(false)
							is OutboxCompletionOutcome.FenceRefused)
						{
							return acceptedCount;
						}
					}
				}
				catch (OutboxFenceRefusedException ex)
				{
					tenure.Refuse(ex);
					// A newer leader has taken over. Return rather than throw, and return from HERE rather
					// than break out of the loop, because the failure-path writes below this point are
					// refused for the captured tenure, so the accepted count must be retained without retry writes
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
					// Accepted transports remain counted, but the rejected run cannot admit later claims,
					// messages or completion paths. Already admitted operations still settle and are joined.
					LogFencedMarkSentRefused(refusedId, ex);

					return acceptedCount;
				}
			}
		}

		// Handle failed messages for retry (applies to all guarantee levels)
		if (!failedToRetry.IsEmpty)
		{
			foreach (var (id, claimIdentity, attemptCount, applyBackoff) in failedToRetry)
			{
				if (!tenure.TryAdmit()) { break; }
				try
				{
					if (applyBackoff)
					{
						await MarkFailedForClaimAsync(tenure, id, claimIdentity, ErrorConstants.RetryAttempt, attemptCount, applyBackoff: true, cancellationToken)
							.ConfigureAwait(false);
					}
					else
					{
						await MarkFailedForClaimAsync(tenure, id, claimIdentity, ErrorConstants.RetryAttempt, attemptCount, applyBackoff: false, cancellationToken).ConfigureAwait(false);
					}
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
		if (_batchSizeCalculator != null && !tenure.IsStopped)
		{
			var successRate = batch.Length > 0 ? (double)acceptedCount / batch.Length : 0;
			_batchSizeCalculator.RecordBatchResult(batch.Length, stopwatch.Elapsed, successRate);
		}

		// Record detailed metrics
		if (!tenure.IsStopped)
		{
			_batchMetrics.RecordBatchCompleted(batch.Length, acceptedCount,
				batch.Length - acceptedCount, stopwatch.Elapsed, _batchCompletionTags);
		}

		return acceptedCount;
	}

	private async Task PerformBatchDatabaseOperationsAsync(ProcessorTenure tenure,
		List<OutboxClaimEvidence> successfulIds,
		List<(string MessageId, string? ClaimIdentity, int AttemptCount, bool ApplyBackoff)> failedToRetry,
		List<(OutboxClaimEvidence Claim, int AttemptCount)> failedToDeadLetter,
		CancellationToken cancellationToken)
	{
		var tasks = new List<Task>();

		if (successfulIds.Count > 0)
		{
			// Process successful messages in parallel -- individual try/catch prevents
			// a single DB failure from leaving the entire batch in inconsistent state.
			tasks.Add(Task.Factory.StartNew(async () =>
			{
				foreach (var claim in successfulIds)
				{
					var id = claim.MessageId;
					if (!tenure.TryAdmit()) { break; }
					try
					{
						// A tenure refusal stops later completion admissions; row-scoped outcomes continue.
						if (await MarkSentAndObserveAsync(tenure, claim, cancellationToken).ConfigureAwait(false) is OutboxCompletionOutcome.FenceRefused) { break; }
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						throw;
					}
					catch (OutboxFenceRefusedException ex)
					{
						tenure.Refuse(ex);
						// A newer leader has taken over. Still caught, because the missing-token composition
						// guard refuses by throwing this type and a consumer store may too, and because it
						// must never be confused with a genuine mark-sent failure -- it is the fence working
						// as designed.
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
					if (!tenure.TryAdmit()) { break; }
					try
					{
						if (applyBackoff)
						{
							await MarkFailedForClaimAsync(tenure, id, claimIdentity, ErrorConstants.RetryAttempt, attemptCount, applyBackoff: true, cancellationToken)
								.ConfigureAwait(false);
						}
						else
						{
							await MarkFailedForClaimAsync(tenure, id, claimIdentity, ErrorConstants.RetryAttempt, attemptCount, applyBackoff: false, cancellationToken).ConfigureAwait(false);
						}
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
				foreach (var (claim, attemptCount) in failedToDeadLetter)
				{
					var id = claim.MessageId;
					if (!tenure.TryAdmit()) { break; }
					try
					{
						// No entry id: this path marks the terminal status only. The external dead-letter write
						// for these messages happened in RouteToDeadLetterQueueAsync, which owns its own
						// compensation, so there is nothing for this call to withdraw.
						await MarkOutboxMessageDeadLetteredAsync(tenure,
							claim,
							ErrorConstants.MaxRetriesReachedMovedToDeadLetter,
							attemptCount,
							enqueuedEntryId: null,
							cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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


	[SuppressMessage("Style", "RCS1163:Unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency and future cancellation support")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "CancellationToken parameter required for async pattern consistency and future cancellation support")]
	private async Task<DispatchAttemptFailure?> DispatchSingleMessageAsync(IOutboxMessage message, PreparedDispatch prepared, CancellationToken cancellationToken)
	{
		using var activity = BackgroundServiceActivitySource.StartMessageDispatch(BackgroundServiceTypes.Outbox, message.MessageId);
		_ = activity?.SetTag("excalibur.outbox.dispatcher_id", _dispatcherId);

		LogDispatchingOutboxRecord(message.MessageId, _dispatcherId!);

		var failure = await DispatchAsync(message, prepared, cancellationToken).ConfigureAwait(false);
		if (failure is null)
		{
			LogSuccessfullyDispatchedOutboxRecord(message.MessageId, _dispatcherId!);
		}
		return failure;
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

	private async Task<DispatchAttemptFailure?> DispatchAsync(IOutboxMessage outboxMessage, PreparedDispatch prepared, CancellationToken cancellationToken)
	{
		try
		{
			await using var scope = _serviceProvider.CreateAsyncScope();

			// Seed the context directly from the strongly-typed metadata — no per-message dictionary alloc.
			var messageContext = DispatchContextInitializer.CreateFromMetadata(prepared.Metadata);
			messageContext.MessageId = outboxMessage.MessageId;

			var scopedDispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
			// This persisted attempt owns the retry budget. Middleware for this exact message/context
			// must not multiply it; independently dispatched nested messages retain their own policies.
			using var retryOwnership = RetryOwnershipScope.EnterClassified(prepared.Message, messageContext, _failureClassifier);
			var result = await scopedDispatcher.DispatchAsync(prepared.Message, messageContext, cancellationToken)
				.ConfigureAwait(false);

			// Check if the dispatch was successful
			if (result is { Succeeded: false })
			{
				cancellationToken.ThrowIfCancellationRequested();
				var errorMessage = result.ErrorMessage ?? ErrorConstants.MessageDispatchFailed;
				if (RetryOwnershipScope.TryGetKnownFailureKind(retryOwnership.CaptureAttempt(), result, out var kind, out var originalFault))
				{
					return new DispatchAttemptFailure(errorMessage, kind, originalFault);
				}
				return new DispatchAttemptFailure(errorMessage, Kind: null, OriginalFault: null);
			}
			return null;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex) when (cancellationToken.IsCancellationRequested)
		{
			// Observe the caller's cancellation before the surrounding circuit breaker sees
			// a transport wrapper (for example IOException wrapping cancellation) as a failure.
			throw new OperationCanceledException("The persisted dispatch attempt was canceled by its caller.", ex, cancellationToken);
		}
		catch (Exception ex)
		{
			LogDispatchFailed(outboxMessage.MessageId, ex);

			throw;
		}
	}

	// A failed result is data, not a fabricated typed exception. Null kind remains unestablished
	// and receives the bounded persisted budget without invoking a classifier on synthetic diagnostics.
	private sealed record DispatchAttemptFailure(string ErrorMessage, MessageFailureKind? Kind, Exception? OriginalFault);

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

	[LoggerMessage(OutboxEventId.OutboxFailureReportFoundTerminalMessage, LogLevel.Information,
		"Failure report for message {MessageId} wrote nothing: the message had already been sent or dead-lettered, so there is nothing to retry. Continuing with the rest of the batch.")]
	private partial void LogFailureReportFoundTerminalMessage(string messageId);

	[LoggerMessage(OutboxEventId.OutboxFencedDeadLetterRefused, LogLevel.Warning,
		"Dead-letter transition for message {MessageId} did not apply ({Outcome}); the outbox row was NOT destroyed. A fence refusal means a newer leadership tenure owns this message and will resolve it.")]
	private partial void LogFencedDeadLetterRefused(string messageId, string outcome);

	[LoggerMessage(OutboxEventId.OutboxDeadLetterRefused, LogLevel.Warning,
		"Dead-letter transition for message {MessageId} did not apply ({Outcome}); the outbox row still stands and withdrawal of any dead-letter entry this drain wrote was attempted where available; a separate error reports an unsuccessful withdrawal. The message is either gone, already terminal, or claimed by another drain, and whoever holds it will resolve it.")]
	private partial void LogDeadLetterRefused(string messageId, string outcome);

	[LoggerMessage(OutboxEventId.OutboxFencedFailureReportRefused, LogLevel.Warning,
		"Failure report for message {MessageId} was refused by the fence: a newer leadership tenure exists, so this tenure recorded nothing. New local admissions for this captured tenure are stopped; already admitted operations may settle.")]
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
		"NullDeadLetterQueue was explicitly selected. Failed outbox messages routed to dead letter will be discarded. Register a retaining IDeadLetterQueue implementation to preserve them for investigation.")]
	private partial void LogDeadLetterQueueNotConfigured();


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
		"Fenced mark-sent for message {MessageId} was refused: this tenure's fencing token is stale (a newer leader has taken over). Stopping new local admissions; already admitted operations may settle. The message is left claimed for the current leader to resolve.")]
	private partial void LogFencedMarkSentRefused(string messageId, Exception ex);

	[LoggerMessage(OutboxEventId.OutboxFencedMarkSentRefusedOutcome, LogLevel.Warning,
		"Fenced mark-sent for message {MessageId} was refused: this tenure's fencing token is stale (a newer leader has taken over). Stopping new local admissions; already admitted operations may settle. The message is left claimed for the current leader to resolve.")]
	private partial void LogFencedMarkSentRefusedOutcome(string messageId);

	[LoggerMessage(OutboxEventId.OutboxMarkSentNotApplied, LogLevel.Warning,
		"Mark-sent for message {MessageId} was not applied: the store reported {Outcome}. This concerns only this message -- the drain continues with the rest of the batch. The message was delivered; only the record of it did not land, so it becomes claimable again when its reservation ages out.")]
	private partial void LogMarkSentNotApplied(string messageId, string outcome);
}
