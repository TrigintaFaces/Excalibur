// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Text.Json;

using Excalibur.Data.InMemory.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Outbox.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IOutboxStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides thread-safe message storage using ConcurrentDictionary.
/// Messages are keyed by their unique message ID.
/// </para>
/// <para>
/// This store is intended for testing scenarios only. Data is lost on application restart.
/// </para>
/// <para>
/// <strong>Tenant-partitioned, not tenant-scoped.</strong> The constructor takes no <see cref="ITenantContext"/>
/// -- every message carries its own <c>TenantId</c>, canonicalised on <c>StageMessageAsync</c> and handed
/// back unchanged on drain, exactly as the persisting providers do. <see cref="ITenantPartitionedStore"/>
/// declares that mechanism explicitly so <c>AddTenantAwareStore</c> can attest it: a constructor with no
/// <see cref="ITenantContext"/> parameter is not, by itself, evidence of row-partitioning, so the marker
/// interface states the claim this store's own behaviour otherwise leaves silent.
/// </para>
/// </remarks>
public sealed partial class InMemoryOutboxStore : IOutboxPayloadTransformStore, IClaimScopedOutboxStore, IClaimScopedDeadLetterableOutboxStore, IFencedOutboxStore, IFencedClaimScopedOutboxStore, IFencedDeadLetterableOutboxStore, IFencedOutboxStoreDiagnostics, IOutboxStoreAdmin, IDeadLetterableOutboxStore, IBackoffSchedulableOutboxStore, ITenantPartitionedStore, IAsyncDisposable, IDisposable
{
	private readonly ConcurrentDictionary<string, OutboundMessage> _messages = new(StringComparer.Ordinal);

	/// <summary>
	/// Side-map of claim leases, keyed by message ID. Kept separate from <see cref="OutboundMessage"/> (the
	/// public/shared domain model) rather than adding lease fields to it directly.
	/// </summary>
	private readonly ConcurrentDictionary<string, (DateTimeOffset LeasedAt, string LeasedBy)> _leases = new(StringComparer.Ordinal);

	/// <summary>
	/// Side-map of failure-anchored visibility floors, keyed by message ID. A failed (sub-ceiling) message
	/// is re-claimable only once <c>now &gt;= floor</c>, where the floor is stamped at the failure instant
	/// (not the lease). This is the canonical <c>MarkFailedAsync</c> re-claimability contract: never
	/// re-claimable in the same drain cycle (no zero-backoff hot-loop), never terminally dropped
	/// (at-least-once). Kept off the shared <see cref="OutboundMessage"/> model, mirroring <see cref="_leases"/>.
	/// </summary>
	private readonly ConcurrentDictionary<string, DateTimeOffset> _nextAttempt = new(StringComparer.Ordinal);

	/// <summary>
	/// Guards the select-and-lease sequence in <see cref="GetUnsentMessagesAsync(int, CancellationToken)"/> so that choosing the
	/// claimable batch and recording its leases happens atomically -- otherwise two concurrent callers could
	/// both select the same eligible messages before either records a lease.
	/// </summary>
	private readonly Lock _claimLock = new();

	/// <summary>
	/// The highest outbox fencing token observed so far, used to fail-closed reject mark-sent calls and
	/// exclude claims from a superseded (stale) leader. Guarded by <see cref="_claimLock"/>.
	/// </summary>
	private long _fencingHighWaterMark;

	private readonly TimeProvider _timeProvider;
	private readonly InMemoryOutboxOptions _options;
	private readonly ILogger<InMemoryOutboxStore> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// <see langword="internal"/>: consumers register the store via <c>AddInMemoryOutboxStore()</c> and resolve it
	/// through its public interfaces; the framework's own DI factory is the only constructor caller.
	/// </remarks>
	internal InMemoryOutboxStore(
		IOptions<InMemoryOutboxOptions> options,
		ILogger<InMemoryOutboxStore> logger)
		: this(options, logger, TimeProvider.System)
	{
	}

	internal InMemoryOutboxStore(
		IOptions<InMemoryOutboxOptions> options,
		ILogger<InMemoryOutboxStore> logger,
		TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(timeProvider);

		_options = options.Value;
		_logger = logger;
		_timeProvider = timeProvider;
	}

	/// <inheritdoc/>
	public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Enforce capacity limits
		EnforceCapacityLimit();

		// Canonicalise the partition on the way in, exactly as a persisting provider's INSERT does. The
		// untenanted partition has ONE representation on the drain contract — the reserved sentinel — so a
		// message staged with no tenant is drained carrying that value rather than a null. Without this fold
		// a consumer handler written as `msg.TenantId is null` would re-establish a different partition
		// depending on which store happened to be underneath it.
		var stored = Snapshot(message);
		stored.TenantId = KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId;

		if (!_messages.TryAdd(stored.Id, stored))
		{
			throw new InvalidOperationException($"Message with ID '{message.Id}' already exists in the outbox.");
		}

		LogMessageStaged(message.Id, message.MessageType, message.Destination);

		return default;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ValidateEnqueueInput(message, context);
		var outbound = PrepareEnqueueMessage(message, context);
		AcceptEnqueue(outbound);
		return default;
	}

	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	async ValueTask IOutboxPayloadTransformStore.EnqueueWithPayloadTransformAsync(
		IDispatchMessage message, IMessageContext context,
		Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload,
		CancellationToken cancellationToken)
	{
		ValidateEnqueueInput(message, context);
		ArgumentNullException.ThrowIfNull(transformPayload);
		cancellationToken.ThrowIfCancellationRequested();
		var outbound = PrepareEnqueueMessage(message, context);
		var transformed = await transformPayload(outbound.Payload, outbound.TenantId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("transformPayload returned a null payload; the message was not enqueued.");
		// The callback owns its result; never keep its mutable array as accepted store state.
		outbound.Payload = (byte[])transformed.Clone();
		cancellationToken.ThrowIfCancellationRequested();
		ObjectDisposedException.ThrowIf(_disposed, this);
		AcceptEnqueue(outbound);
	}

	private void ValidateEnqueueInput(IDispatchMessage message, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ObjectDisposedException.ThrowIf(_disposed, this);
	}

	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private static OutboundMessage PrepareEnqueueMessage(IDispatchMessage message, IMessageContext context)
	{
		var messageType = message.GetType().FullName ?? message.GetType().Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());
		return OutboundMessage.FromContext(messageType, payload, messageType, context);
	}

	private void AcceptEnqueue(OutboundMessage outbound)
	{
		EnforceCapacityLimit();
		if (!_messages.TryAdd(outbound.Id, outbound))
		{
			throw new InvalidOperationException($"Failed to enqueue message with ID '{outbound.Id}'.");
		}
		LogMessageEnqueued(outbound.Id, outbound.MessageType);
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
		GetUnsentMessagesCore(batchSize, fencingToken: null, cancellationToken);

	/// <inheritdoc />
	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, long fencingToken, CancellationToken cancellationToken) =>
		GetUnsentMessagesCore(batchSize, fencingToken, cancellationToken);

	private ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesCore(int batchSize, long? fencingToken, CancellationToken cancellationToken)
	{
		var result = ClaimUnsentMessages(batchSize, fencingToken, out var refusedHighWater, cancellationToken);
		if (refusedHighWater is { } highWater)
		{
			ReportClaimFenceRefusal(fencingToken!.Value, highWater);
		}
		return result;
	}

	private ValueTask<IEnumerable<OutboundMessage>> ClaimUnsentMessages(int batchSize, long? fencingToken,
		out long? refusedHighWater, CancellationToken cancellationToken)
	{
		refusedHighWater = null;
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Atomically select-and-lease under a single lock: choosing the claimable batch and recording its
		// leases happens as one step, so two concurrent callers (pollers in the same process) can never
		// both select the same eligible message -- mirrors the SQL Server / MongoDB atomic-claim contract.
		lock (_claimLock)
		{
			var now = _timeProvider.GetUtcNow();
			var leaseCutoff = now - TimeSpan.FromSeconds(_options.LeaseTimeoutSeconds);

			if (fencingToken.HasValue)
			{
				if (fencingToken.Value < _fencingHighWaterMark)
				{
					refusedHighWater = _fencingHighWaterMark;
					// Presented token is stale (superseded leader): exclude all rows from the claim rather
					// than throwing -- this is a set-based operation.
					return new ValueTask<IEnumerable<OutboundMessage>>(Array.Empty<OutboundMessage>());
				}

				_fencingHighWaterMark = Math.Max(_fencingHighWaterMark, fencingToken.Value);
			}

			// Single pass: _claimLock serialises claimers but NOT producers, so a two-pass count-then-fill
			// can find more claimable messages in pass 2 than pass 1 sized the array for.
			var candidateList = new List<OutboundMessage>();
			foreach (var m in _messages.Values)
			{
				if (IsClaimable(m, now, leaseCutoff))
				{
					candidateList.Add(m);
				}
			}

			if (candidateList.Count == 0)
			{
				return new ValueTask<IEnumerable<OutboundMessage>>(Array.Empty<OutboundMessage>());
			}

			var candidates = candidateList.ToArray();

			Array.Sort(candidates, static (a, b) =>
			{
				var priorityCompare = a.Priority.CompareTo(b.Priority);
				return priorityCompare != 0 ? priorityCompare : a.CreatedAt.CompareTo(b.CreatedAt);
			});

			var resultSize = Math.Min(batchSize, candidates.Length);
			var claimed = new OutboundMessage[resultSize];
			for (var i = 0; i < resultSize; i++)
			{
				var identity = Guid.NewGuid().ToString("N");
				_leases[candidates[i].Id] = (now, identity);
				// Each acquisition owns its evidence. Reclaim must never rewrite an older caller's stamp.
				claimed[i] = Snapshot(candidates[i]);
				claimed[i].DispatcherId = identity;
			}

			return new ValueTask<IEnumerable<OutboundMessage>>(claimed);
		}
	}

	// Own the envelope, byte buffer and top-level bags. Arbitrary header values retain their
	// declared object semantics; this does not promise a deep clone of consumer object graphs.
	private static OutboundMessage Snapshot(OutboundMessage message)
	{
		var snapshot = new OutboundMessage
		{
			Id = message.Id,
			MessageType = message.MessageType,
			Destination = message.Destination,
			CreatedAt = message.CreatedAt,
			ScheduledAt = message.ScheduledAt,
			SentAt = message.SentAt,
			Status = message.Status,
			RetryCount = message.RetryCount,
			LastError = message.LastError,
			LastAttemptAt = message.LastAttemptAt,
			DispatcherId = message.DispatcherId,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			TenantId = message.TenantId,
			Priority = message.Priority,
			PartitionKey = message.PartitionKey,
			GroupKey = message.GroupKey,
			SequenceNumber = message.SequenceNumber,
			TargetTransports = message.TargetTransports,
			IsMultiTransport = message.IsMultiTransport,
			Payload = (byte[])message.Payload.Clone(),
			Headers = new Dictionary<string, object>(message.Headers, StringComparer.Ordinal),
		};
		foreach (var delivery in message.TransportDeliveries)
		{
			snapshot.TransportDeliveries.Add(new OutboundMessageTransport
			{
				Id = delivery.Id,
				MessageId = delivery.MessageId,
				TransportName = delivery.TransportName,
				Destination = delivery.Destination,
				Status = delivery.Status,
				CreatedAt = delivery.CreatedAt,
				AttemptedAt = delivery.AttemptedAt,
				SentAt = delivery.SentAt,
				RetryCount = delivery.RetryCount,
				LastError = delivery.LastError,
				TransportMetadata = delivery.TransportMetadata,
			});
		}
		return snapshot;
	}

	/// <summary>
	/// Determines whether a message is eligible to be claimed: staged, due (not scheduled for the future),
	/// and either unleased or its lease has gone stale (a crash-recovery reclaim).
	/// </summary>
	private bool IsClaimable(OutboundMessage message, DateTimeOffset now, DateTimeOffset leaseCutoff)
	{
		// Staged (never attempted) and Failed (attempted, sub-retry-ceiling — still owed at-least-once
		// delivery) are both eligible; Sent/Sending/DeadLettered are not. A Failed message re-enters the
		// claimable set only once its post-failure lease has aged past the coarse lease-timeout floor
		// below (never in the same drain cycle — no zero-backoff hot-loop), and it is never terminally
		// dropped (at-least-once). This is the canonical MarkFailedAsync re-claimability contract shared
		// across the outbox family; fine-grained backoff is the separate MarkFailedWithBackoffAsync path.
		if ((message.Status is not OutboxStatus.Staged and not OutboxStatus.Failed)
			|| (message.ScheduledAt != null && message.ScheduledAt > now))
		{
			return false;
		}

		// R1 failure-anchored floor: a failed message is not re-claimable until its post-failure visibility
		// window elapses (no zero-backoff hot-loop). Enforced by this store-owned timestamp, never a
		// dispatcher-side clock delta.
		if (_nextAttempt.TryGetValue(message.Id, out var nextAttempt) && now < nextAttempt)
		{
			return false;
		}

		return !_leases.TryGetValue(message.Id, out var lease) || lease.LeasedAt <= leaseCutoff;
	}

	/// <inheritdoc/>
	public Task<long?> GetFencingHighWaterAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		lock (_claimLock)
		{
			// ponytail: 0 is the field's default and doubles as "never fenced" -- a legitimate token is
			// virtually always a positive monotonic counter from a leader election, so this is not
			// distinguished from a real high-water of exactly 0 with a separate bool. Upgrade if that
			// ever matters.
			return Task.FromResult<long?>(_fencingHighWaterMark == 0 ? null : _fencingHighWaterMark);
		}
	}

	/// <inheritdoc/>
	public Task ResetFencingHighWaterAsync(long newHighWater, bool force, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		lock (_claimLock)
		{
			if (!force && newHighWater < _fencingHighWaterMark)
			{
				throw new InvalidOperationException(
					$"Refusing to lower the fencing high-water mark from {_fencingHighWaterMark} to {newHighWater} " +
					"without force: true. Lowering it re-admits a leader whose token is now below the (lowered) " +
					"high-water, which is the split-brain the fence exists to prevent.");
			}

			_fencingHighWaterMark = newHighWater;
			return Task.CompletedTask;
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		// The unfenced member reports by THROWING, and that asymmetry is the contract rather than an
		// oversight: IOutboxStore.MarkSentAsync returns void, so it has nowhere to put an outcome, and a
		// caller on this path presented no token and therefore cannot be a superseded tenure. The refusals
		// reachable here are a missing row and an already-terminal one, both of which this member has always
		// raised, with the three distinct messages the conformance kit asserts. Translating them back from
		// the shared core is what keeps those messages identical.
		var outcome = await MarkSentCore(messageId, fencingToken: null, cancellationToken).ConfigureAwait(false);
		switch (outcome)
		{
			case OutboxCompletionOutcome.Applied:
				break;

			case OutboxCompletionOutcome.MessageNotFound:
				throw new InvalidOperationException($"Message with ID '{messageId}' not found.");

			default:
				throw new InvalidOperationException(
					_messages.TryGetValue(messageId, out var terminal) && terminal.Status == OutboxStatus.DeadLettered
						? $"Message with ID '{messageId}' was dead-lettered and cannot be marked as sent."
						: $"Message with ID '{messageId}' is already marked as sent.");
		}
	}

	/// <inheritdoc />
	public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken) =>
		MarkSentCore(messageId, fencingToken, cancellationToken);

	private ValueTask<OutboxCompletionOutcome> MarkSentCore(string messageId, long? fencingToken, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ObjectDisposedException.ThrowIf(_disposed, this);

		// ONE lock region spans the fence check, the high-water advance, and the mutation they guard —
		// matching the claim path above, and for the same reason. Splitting them is not a style question:
		// with the fence in one critical section and the mutation in another, a fresher tenure can advance
		// the high-water in the gap between them, and the superseded caller then applies a write the fence
		// had already been told to refuse. Its check passed, truthfully, against a value that no longer
		// held by the time it wrote. The advance and the write it authorises must be indivisible, so the
		// mutation applies only if THIS caller's token was the one the high-water accepted.
		//
		// The global lock also subsumes the per-message lock this path used to take: holding it excludes
		// every other mark-sent, not merely the ones naming the same message, so a second mutual-exclusion
		// domain would add nothing here except the illusion that the two were coordinated.
		lock (_claimLock)
		{
			if (fencingToken.HasValue)
			{
				// REPORTED, NOT THROWN. The fence is judged first and its refusal is TENURE-scoped: a newer
				// tenure exists, so every remaining write this caller would make is unfenced and it must
				// stand down rather than carry on with its batch. Judging the row's state first would hand
				// a superseded tenure a row-scoped answer, which says the opposite.
				if (fencingToken.Value < _fencingHighWaterMark)
				{
					return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.FenceRefused);
				}

				_fencingHighWaterMark = Math.Max(_fencingHighWaterMark, fencingToken.Value);
			}

			if (!_messages.TryGetValue(messageId, out var message))
			{
				return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.MessageNotFound);
			}

			// Sent and DeadLettered are both terminal, and both report AlreadyTerminal rather than ClaimLost:
			// a terminal message is owned by nobody, so reporting it as re-claimed describes a decision
			// nobody made. This store keeps the row, so unlike the delete-on-sent stores it can tell the
			// difference and must.
			if (message.Status is OutboxStatus.Sent or OutboxStatus.DeadLettered)
			{
				return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.AlreadyTerminal);
			}

			message.MarkSent();
			message.SentAt = _timeProvider.GetUtcNow();
			_ = _leases.TryRemove(messageId, out _);
			_ = _nextAttempt.TryRemove(messageId, out _);
		}

		LogMessageSent(messageId);

		return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied);
	}

	/// <inheritdoc/>
	public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
		=> MarkFailedCore(messageId, errorMessage, retryCount, nextAttemptAt: null);

	/// <inheritdoc/>
	public ValueTask MarkFailedWithBackoffAsync(string messageId, string errorMessage, int retryCount,
		DateTimeOffset nextAttemptAt, CancellationToken cancellationToken)
		=> MarkFailedCore(messageId, errorMessage, retryCount, nextAttemptAt);

	private ValueTask MarkFailedCore(string messageId, string errorMessage, int retryCount, DateTimeOffset? nextAttemptAt)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Every read-then-mutate below runs under the SAME lock the claim path and the fenced completion
		// overloads take. Without it two completions on one message -- one fenced, one not -- interleave
		// between the terminal-status test and the mutation, and the terminal exclusion above stops
		// excluding anything: a failure report evaluated before a concurrent send commits still writes
		// Failed afterwards, which returns a DELIVERED message to the claimable set to be sent again.
		// The guard is only as atomic as the section containing it.
		lock (_claimLock)
		{
			if (!_messages.TryGetValue(messageId, out var message))
			{
				// Silent return for missing messages per conformance tests expectation
				return default;
			}

			// TERMINAL-STATUS exclusion. Sent and DeadLettered are final, and no completion may move a message
			// out of either -- not a superseded caller's and not the CURRENT claim holder's. Without this a
			// failure reported after a successful send returns the message to the Failed status, which IS in the
			// claim predicate, so a delivered message is delivered again. Silent like the missing case: the
			// report is stale rather than erroneous.
			if (message.Status is OutboxStatus.Sent or OutboxStatus.DeadLettered)
			{
				return default;
			}

			// This legacy overload carries no acquisition evidence. Claim-scoped workers must use
			// the authority overload; an implicit process identity cannot prove ownership of a lease.
			ApplyFailureUnderClaimLock(messageId, message, errorMessage, retryCount, nextAttemptAt);
		}

		return default;
	}

	// Called only after route-specific ownership/fencing guards while _claimLock is held.
	// State, schedule and lease release must be one transition: a successor cannot claim in a gap.
	private void ApplyFailureUnderClaimLock(string messageId, OutboundMessage message, string errorMessage, int retryCount, DateTimeOffset? requestedNextAttempt)
	{
		var now = _timeProvider.GetUtcNow();
		var floor = now + TimeSpan.FromSeconds(_options.FailureBackoffFloorSeconds);
		var priorRetryCount = message.RetryCount;
		message.MarkFailed(errorMessage);
		message.RetryCount = Math.Max(priorRetryCount, retryCount);
		message.LastAttemptAt = now;
		_nextAttempt[messageId] = requestedNextAttempt is { } requested && requested > floor ? requested : floor;
		_ = _leases.TryRemove(messageId, out _);
		LogMessageFailed(messageId, errorMessage, retryCount);
	}

	/// <inheritdoc/>
	public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);
		ArgumentOutOfRangeException.ThrowIfNegative(attemptCount);
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Under the same lock as every other completion on this type, for the reason stated on
		// MarkFailedAsync: a terminal-status test that is not in the same critical section as the
		// mutation it guards can be true when read and false when acted on.
		lock (_claimLock)
		{
			if (!_messages.TryGetValue(messageId, out var message))
			{
				// Reported, not silent: the caller has already written the dead-letter entry, so a
				// refusal it cannot see strands that entry against a row that is not here.
				return ValueTask.FromResult(OutboxCompletionOutcome.MessageNotFound);
			}

			// Terminal exclusion, mirroring MarkFailedAsync. A dead-letter applied to an already-sent message
			// records a delivery failure that did not happen; repeated on an already-dead-lettered one it writes
			// a second dead-letter for one message. Binds the current claim holder as much as a stale caller.
			if (message.Status is OutboxStatus.Sent or OutboxStatus.DeadLettered)
			{
				return ValueTask.FromResult(OutboxCompletionOutcome.AlreadyTerminal);
			}

			message.Status = OutboxStatus.DeadLettered;
			message.RetryCount = Math.Max(message.RetryCount, attemptCount);
			message.LastError = reason;

			// Clear the claim lease and failure-visibility floor for hygiene. DeadLettered is
			// terminal — the claim predicate already excludes it, so no floor is needed to keep it out.
			_ = _leases.TryRemove(messageId, out _);
			_ = _nextAttempt.TryRemove(messageId, out _);

		}

		return ValueTask.FromResult(OutboxCompletionOutcome.Applied);
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Single pass: concurrent producers can grow _messages between two passes, so a count-then-fill
		// would overflow the array it sized.
		var candidateList = new List<OutboundMessage>();
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Failed &&
				(maxRetries <= 0 || m.RetryCount < maxRetries) &&
				(!olderThan.HasValue || m.LastAttemptAt < olderThan.Value))
			{
				candidateList.Add(Snapshot(m));
			}
		}

		if (candidateList.Count == 0)
		{
			return new ValueTask<IEnumerable<OutboundMessage>>(Array.Empty<OutboundMessage>());
		}

		var candidates = candidateList.ToArray();

		Array.Sort(candidates, static (a, b) =>
		{
			var retryCompare = a.RetryCount.CompareTo(b.RetryCount);
			return retryCompare != 0 ? retryCompare : Nullable.Compare(a.LastAttemptAt, b.LastAttemptAt);
		});

		var resultSize = Math.Min(batchSize, candidates.Length);
		var failed = resultSize == candidates.Length
			? candidates
			: candidates.AsSpan(0, resultSize).ToArray();

		return new ValueTask<IEnumerable<OutboundMessage>>(failed);
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Single pass: concurrent producers can grow _messages between two passes, so a count-then-fill
		// would overflow the array it sized.
		var candidateList = new List<OutboundMessage>();
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Staged && m.ScheduledAt.HasValue && m.ScheduledAt.Value <= scheduledBefore)
			{
				candidateList.Add(Snapshot(m));
			}
		}

		if (candidateList.Count == 0)
		{
			return new ValueTask<IEnumerable<OutboundMessage>>(Array.Empty<OutboundMessage>());
		}

		var candidates = candidateList.ToArray();

		Array.Sort(candidates, static (a, b) => Nullable.Compare(a.ScheduledAt, b.ScheduledAt));

		var resultSize = Math.Min(batchSize, candidates.Length);
		var scheduled = resultSize == candidates.Length
			? candidates
			: candidates.AsSpan(0, resultSize).ToArray();

		return new ValueTask<IEnumerable<OutboundMessage>>(scheduled);
	}

	/// <inheritdoc/>
	public ValueTask<int> CleanupAllTenantsSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Use array-based approach to avoid ToList() allocation
		var candidateCount = 0;
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Sent && m.SentAt.HasValue && m.SentAt.Value < olderThan)
			{
				candidateCount++;
			}
		}

		if (candidateCount == 0)
		{
			LogMessagesCleanedUp(0, olderThan);
			return new ValueTask<int>(0);
		}

		var toRemove = new OutboundMessage[Math.Min(candidateCount, batchSize)];
		var idx = 0;
		foreach (var m in _messages.Values)
		{
			if (idx >= toRemove.Length)
			{
				break;
			}

			if (m.Status == OutboxStatus.Sent && m.SentAt.HasValue && m.SentAt.Value < olderThan)
			{
				toRemove[idx++] = m;
			}
		}

		var count = 0;
		for (var i = 0; i < idx; i++)
		{
			var message = toRemove[i];
			if (_messages.TryRemove(message.Id, out _))
			{
				_ = _leases.TryRemove(message.Id, out _);
				_ = _nextAttempt.TryRemove(message.Id, out _);
				count++;
			}
		}

		LogMessagesCleanedUp(count, olderThan);

		return new ValueTask<int>(count);
	}

	/// <inheritdoc/>
	public ValueTask<OutboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Single-pass statistics without ToList() allocations
		var now = _timeProvider.GetUtcNow();
		var stagedCount = 0;
		var sendingCount = 0;
		var sentCount = 0;
		var failedCount = 0;
		var scheduledCount = 0;
		OutboundMessage? oldestUnsent = null;
		OutboundMessage? oldestFailed = null;

		foreach (var message in _messages.Values)
		{
			switch (message.Status)
			{
				case OutboxStatus.Staged:
					stagedCount++;
					if (message.ScheduledAt.HasValue)
					{
						scheduledCount++;
					}
					else if (oldestUnsent == null || message.CreatedAt < oldestUnsent.CreatedAt)
					{
						// Unsent = staged without schedule, or scheduled and due
						oldestUnsent = message;
					}

					// Check scheduled messages that are due
					if (message.ScheduledAt.HasValue && message.ScheduledAt <= now &&
						(oldestUnsent == null || message.CreatedAt < oldestUnsent.CreatedAt))
					{
						oldestUnsent = message;
					}

					break;

				case OutboxStatus.Sending:
					sendingCount++;
					break;

				case OutboxStatus.Sent:
					sentCount++;
					break;

				case OutboxStatus.Failed:
					failedCount++;
					if (oldestFailed == null || message.CreatedAt < oldestFailed.CreatedAt)
					{
						oldestFailed = message;
					}

					break;
			}
		}

		return new ValueTask<OutboxStatistics>(new OutboxStatistics
		{
			StagedMessageCount = stagedCount,
			SendingMessageCount = sendingCount,
			SentMessageCount = sentCount,
			FailedMessageCount = failedCount,
			ScheduledMessageCount = scheduledCount,
			OldestUnsentMessageAge = oldestUnsent != null ? now - oldestUnsent.CreatedAt : null,
			OldestFailedMessageAge = oldestFailed != null ? now - oldestFailed.CreatedAt : null,
			CapturedAt = now
		});
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_messages.Clear();
		_leases.Clear();
		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	private void EnforceCapacityLimit()
	{
		if (_options.MaxMessages > 0 && _messages.Count >= _options.MaxMessages)
		{
			EvictOldestTerminalMessage();
		}
	}

	/// <summary>
	/// Reclaims one message to make room, choosing only among messages whose delivery is already OVER.
	/// </summary>
	/// <remarks>
	/// Eviction must FAIL CLOSED, never silently drop a message that is still owed delivery. The store's
	/// contract is at-least-once: every staged message reaches its transport at least once. A message that
	/// is Staged, Sending (currently leased to a claimer), Failed or PartiallyFailed is still owed that
	/// delivery, so removing it delivers it ZERO times -- the guarantee broken at its floor, silently, with
	/// the claimer that holds the lease left to report against a message that no longer exists. Only Sent and
	/// DeadLettered are terminal, and only they are candidates. If neither exists the store is full of
	/// undelivered work and the staging call THROWS, so the caller (whose transaction has not yet committed)
	/// learns the message was not accepted instead of believing it was queued.
	///
	/// No lock is needed around the choose-then-remove: both terminal states are absorbing -- nothing
	/// transitions out of Sent or DeadLettered -- so a candidate chosen by the scan cannot become
	/// delivery-owed before the removal, and a concurrent transition INTO a terminal state can only add
	/// candidates. This mirrors the in-memory inbox, whose eviction fails closed rather than dropping a live
	/// deduplication record.
	/// </remarks>
	private void EvictOldestTerminalMessage()
	{
		if (!TryGetEvictionCandidateId(out var messageId))
		{
			throw new InvalidOperationException(
				$"The in-memory outbox is at capacity ({_options.MaxMessages}) and every message is still awaiting " +
				"delivery. Evicting one would discard a message that has never been sent. Increase MaxMessages, or " +
				"drain and clean up sent messages more frequently.");
		}

		_ = _messages.TryRemove(messageId, out _);
		_ = _leases.TryRemove(messageId, out _);
		_ = _nextAttempt.TryRemove(messageId, out _);
	}

	/// <summary>
	/// Selects the message to reclaim: the oldest Sent message, else the oldest DeadLettered one.
	/// </summary>
	/// <remarks>
	/// There is deliberately NO "oldest message overall" fallback. That fallback was the defect: with no Sent
	/// message present it returned whatever was oldest -- routinely an unsent, currently-leased message --
	/// and eviction dropped it. Restricting the scan to the two terminal states makes "evict something still
	/// owed delivery" unrepresentable here rather than merely unlikely.
	/// </remarks>
	private bool TryGetEvictionCandidateId(out string messageId)
	{
		OutboundMessage? oldestSentMessage = null;
		DateTimeOffset? oldestSentAt = null;
		OutboundMessage? oldestDeadLetteredMessage = null;

		foreach (var message in _messages.Values)
		{
			switch (message.Status)
			{
				case OutboxStatus.Sent when oldestSentMessage is null || Nullable.Compare(message.SentAt, oldestSentAt) < 0:
					oldestSentMessage = message;
					oldestSentAt = message.SentAt;
					break;

				case OutboxStatus.DeadLettered when oldestDeadLetteredMessage is null || message.CreatedAt < oldestDeadLetteredMessage.CreatedAt:
					oldestDeadLetteredMessage = message;
					break;

				default:
					break;
			}
		}

		var candidate = oldestSentMessage ?? oldestDeadLetteredMessage;
		if (candidate is null)
		{
			messageId = string.Empty;
			return false;
		}

		messageId = candidate.Id;
		return true;
	}

	#region High-Performance Logging

	[LoggerMessage(DataInMemoryEventId.OutboxMessageStaged, LogLevel.Debug,
		"Staged message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogMessageStaged(string messageId, string messageType, string destination);

	[LoggerMessage(DataInMemoryEventId.OutboxMessageEnqueued, LogLevel.Debug, "Enqueued message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataInMemoryEventId.OutboxMessageSent, LogLevel.Debug, "Marked message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataInMemoryEventId.OutboxMessageFailed, LogLevel.Warning,
		"Marked message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataInMemoryEventId.OutboxCleanedUp, LogLevel.Information, "Cleaned up {Count} sent messages older than {OlderThan}")]
	private partial void LogMessagesCleanedUp(int count, DateTimeOffset olderThan);

	#endregion High-Performance Logging
}
