// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using IBM.WMQ;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// Receives messages from an IBM MQ queue under a unit of work per message: each received message holds its
/// own queue-manager connection and syncpoint, so
/// <see cref="AcknowledgeAsync"/> commits (removes) exactly that message and <see cref="RejectAsync"/>
/// backs it out (redelivers) — true per-message ack/reject in any order. Outstanding units of work are
/// bounded by the configured outstanding limit. Settlement attempts release local handles, including
/// during <see cref="DisposeAsync"/>; failed native calls may leave the broker outcome unknown.
/// </summary>
internal sealed partial class IbmMqTransportReceiver : ITransportReceiver
{
	private readonly IIbmMqConnectionProvider _connectionProvider;
	private readonly IbmMqReceiveTuningOptions _receive;
	private readonly ILogger<IbmMqTransportReceiver> _logger;
	private readonly ConcurrentDictionary<string, UnitOfWork> _outstanding = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	public IbmMqTransportReceiver(
		IIbmMqConnectionProvider connectionProvider,
		string source,
		IbmMqReceiveTuningOptions receive,
		ILogger<IbmMqTransportReceiver> logger)
	{
		_connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_receive = receive ?? throw new ArgumentNullException(nameof(receive));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		cancellationToken.ThrowIfCancellationRequested();

		// Back-pressure: each outstanding (un-settled) message holds its own queue-manager connection, so
		// bound the cumulative outstanding set — not just the per-call batch — to protect the queue
		// manager's connection pool under slow acknowledgement. When saturated, return fewer (or zero)
		// messages until the caller acknowledges/rejects enough to free capacity.
		var remainingCapacity = _receive.MaxOutstandingUnitsOfWork - _outstanding.Count;
		if (remainingCapacity <= 0)
		{
			return Task.FromResult<IReadOnlyList<TransportReceivedMessage>>([]);
		}

		var limit = Math.Clamp(maxMessages, 1, _receive.MaxBatchSize);
		limit = Math.Min(limit, remainingCapacity);
		// The IBM MQ managed client is synchronous; the blocking get runs inline (the caller drives receive
		// from its own background pump). Returns a completed task — no thread-pool offload.
		return Task.FromResult<IReadOnlyList<TransportReceivedMessage>>(Drain(limit, cancellationToken));
	}

	/// <inheritdoc />
	public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		cancellationToken.ThrowIfCancellationRequested();
		Settle(GetReceiptHandle(message), commit: true);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		cancellationToken.ThrowIfCancellationRequested();

		if (requeue)
		{
			// REQUEUE: backing out the uncommitted syncpoint get returns the message to the input queue, and
			// the queue manager increments MQMD.BackoutCount so a consumer can bound its own retries. That
			// count is reported as TransportReceivedMessage.DeliveryCount.
			Settle(GetReceiptHandle(message), commit: false);
			return Task.CompletedTask;
		}

		// REJECT WITHOUT REQUEUE: the caller does not want this message delivered again, and a backout is
		// the opposite of that. This method used to back out for BOTH values of requeue and return success,
		// so a poison-message arm asking for no redelivery got immediate redelivery and was told it had
		// succeeded -- an unbounded loop in which a dead-letter decorator writes a fresh copy every pass.
		SettleToBackoutQueue(GetReceiptHandle(message), reason);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return serviceType.IsInstanceOfType(_connectionProvider) ? _connectionProvider : null;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;

		// Back out and close every outstanding unit of work so no message stays locked and no connection leaks.
		foreach (var id in _outstanding.Keys)
		{
			try
			{
				Settle(id, commit: false);
			}
#pragma warning disable CA1031 // Disposal must not throw, and must not abandon the units of work behind this one.
			catch (Exception)
			{
				// Deliberately swallowed HERE and nowhere else: disposal must not throw, and one queue
				// manager refusing a backout must not abandon the remaining units of work or leak their
				// connections. Native failures do not establish whether broker rollback completed.
				//
				// The catch is deliberately broad rather than TransportSettlementException alone. Settle
				// surfaces a refused settlement as that type, but the queue manager and the connection
				// close beneath it can fail in other ways, and any one of those escaping would break out
				// of this loop and leak every connection after it — the outcome this handler exists to
				// prevent.
			}
#pragma warning restore CA1031
		}

		return ValueTask.CompletedTask;
	}

	private List<TransportReceivedMessage> Drain(int limit, CancellationToken cancellationToken)
	{
		var received = new List<TransportReceivedMessage>(limit);

		for (var i = 0; i < limit; i++)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			// First get waits up to the configured interval; the rest return immediately so a partial batch
			// does not block for the full interval per empty slot.
			var waitMilliseconds = i == 0 ? _receive.WaitIntervalMilliseconds : 0;
			var message = TryGetOne(waitMilliseconds);
			if (message is null)
			{
				break;
			}

			received.Add(message);
		}

		return received;
	}

	private TransportReceivedMessage? TryGetOne(int waitMilliseconds)
	{
		IIbmMqQueueManager? queueManager = null;
		IIbmMqQueue? queue = null;
		try
		{
			queueManager = _connectionProvider.CreateQueueManager();
			queue = queueManager.AccessQueue(Source, MQC.MQOO_INPUT_AS_Q_DEF | MQC.MQOO_FAIL_IF_QUIESCING);

			var mqMessage = new MQMessage();
			var getOptions = new MQGetMessageOptions
			{
				Options = MQC.MQGMO_SYNCPOINT | MQC.MQGMO_WAIT | MQC.MQGMO_FAIL_IF_QUIESCING,
				WaitInterval = waitMilliseconds,
			};

			queue.Get(mqMessage, getOptions);

			// Enforce the configured inbound payload cap (fail-closed). An oversized message can never be
			// processed, so it is discarded (committed under syncpoint) rather than delivered — backing it out
			// would loop the queue manager redelivering an unprocessable payload.
			if (_receive.MaxPayloadBytes is { } maxPayloadBytes && mqMessage.MessageLength > maxPayloadBytes)
			{
				LogPayloadTooLargeRejected(Source, mqMessage.MessageLength, maxPayloadBytes);
				queueManager.Commit();
				SafeClose(queue, queueManager);
				return null;
			}

			var id = Convert.ToHexString(mqMessage.MessageId);
			var received = BuildReceived(id, mqMessage);
			// MQMD identity can recur across deliveries; each local unit of work needs its own receipt.
			var receiptHandle = Guid.NewGuid().ToString("N");
			received.ProviderData["ibmmq.receipt_handle"] = receiptHandle;
			_outstanding[receiptHandle] = new UnitOfWork(queueManager, queue, mqMessage);
			return received;
		}
		catch (MQException ex) when (ex.ReasonCode == MQC.MQRC_NO_MSG_AVAILABLE)
		{
			// No message within the wait window — release this idle unit of work.
			TryBackout(queueManager);
			SafeClose(queue, queueManager);
			return null;
		}
		catch (MQException ex)
		{
			LogReceiveFailed(Source, ex.ReasonCode, ex);
			TryBackout(queueManager);
			SafeClose(queue, queueManager);
			throw;
		}
	}

	private TransportReceivedMessage BuildReceived(string id, MQMessage mqMessage)
	{
		var body = mqMessage.ReadBytes(mqMessage.MessageLength);

		string? correlationId = null;
		if (mqMessage.CorrelationId is { Length: > 0 })
		{
			correlationId = System.Text.Encoding.UTF8.GetString(mqMessage.CorrelationId).TrimEnd('\0');
			if (correlationId.Length == 0)
			{
				correlationId = null;
			}
		}

		// IBM MQ carries the CloudEvents binary-mode ce-* attributes as message properties (RFH2) --
		// previously discarded here, which made binary-mode CloudEvents structurally undetectable on
		// receive. "%" is IBM MQ's documented wildcard for "every property name". Any
		// structured-mode marker a producer sets is captured the same way, in Properties.
		//
		// CORRECTION, and it matters because this comment previously named a property IBM MQ cannot accept:
		// the example given here used to be a hyphenated "content-type". MQ validates property names as
		// Java identifiers -- "." is permitted, a hyphen is not -- so setting that name fails with
		// MQRC_PROPERTY_NAME_ERROR (2442). The settable spelling is the underscore form, and it is declared
		// once on the sender as ContentTypePropertyName so both sides cannot drift.
		var properties = new Dictionary<string, object>(StringComparer.Ordinal);

		// The MQMD transfer format, under a name that says what it is. It used to be reported as the
		// message's ContentType, which is a different concept; keeping it here preserves it for the
		// deployments that switch on it without letting it impersonate a media type.
		if (!string.IsNullOrWhiteSpace(mqMessage.Format))
		{
			properties[MqFormatPropertyName] = mqMessage.Format;
		}

		var propertyNames = mqMessage.GetPropertyNames("%");
		while (propertyNames.MoveNext())
		{
			if (propertyNames.Current is not string name)
			{
				continue;
			}

			var value = mqMessage.GetObjectProperty(name);
			if (value is not null)
			{
				properties[name] = value;
			}
		}

		return new TransportReceivedMessage
		{
			Id = id,
			Body = body,
			ContentType = ResolveContentType(properties),
			CorrelationId = correlationId,
			Source = Source,
			DeliveryCount = mqMessage.BackoutCount + 1,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = properties,
		};
	}

	/// <summary>The property carrying MQ's own transfer-format tag, which is not a media type.</summary>
	internal const string MqFormatPropertyName = "ibmmq.format";

	/// <summary>
	/// The MIME content type the producer declared, or <see langword="null"/> when it declared none.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The MQMD wire-format tag is NOT a fallback for this field, and returning it was a lie.</b> That tag
	/// is drawn from MQ's own closed set of transfer formats (<c>MQSTR</c>, <c>MQHRF2</c>,
	/// <c>MQFMT_NONE</c>); it describes how the queue manager should convert the payload, not what the
	/// payload IS. Reporting it in a field whose contract is a media type hands every caller a value that
	/// can never parse as one, and it does so on EVERY message, because the tag is always present. A caller
	/// cannot tell that answer apart from a real one.
	/// </para>
	/// <para>
	/// <b>Null is the honest answer for a producer that declared nothing</b>, and it is what the rest of the
	/// framework already expects: the content type is optional on a transport message everywhere else. This
	/// also makes the structured-mode CloudEvents identifier meaningful here for the first time — that mode
	/// is recognised by its media type ALONE, so a transport that overwrote the media type with a
	/// wire-format tag could not carry one however it was encoded.
	/// </para>
	/// <para>
	/// The tag is not discarded. It stays available in <c>Properties</c> under its own name for the
	/// deployments that read it, where it is correctly labelled as MQ wire information rather than
	/// impersonating a media type.
	/// </para>
	/// </remarks>
	private static string? ResolveContentType(Dictionary<string, object> properties) =>
		properties.TryGetValue(IbmMqTransportSender.ContentTypePropertyName, out var declared)
			&& declared is string { Length: > 0 } contentType
				? contentType
				: null;

	private static string GetReceiptHandle(TransportReceivedMessage message)
	{
		if (message.ProviderData.TryGetValue("ibmmq.receipt_handle", out var handle)
			&& handle is string receiptHandle && !string.IsNullOrWhiteSpace(receiptHandle))
		{
			return receiptHandle;
		}
		throw CreateSettlementException("IBM MQ message does not contain a valid receipt handle in ProviderData.",
			new InvalidOperationException("Missing or malformed IBM MQ receipt handle."), TransportRedeliveryExpectation.Unspecified);
	}

	private void Settle(string id, bool commit)
	{
		var unitOfWork = ClaimUnitOfWork(id, commit ? "acknowledge" : "requeue");

		try
		{
			if (commit)
			{
				unitOfWork.QueueManager.Commit();
			}
			else
			{
				unitOfWork.QueueManager.Backout();
			}
		}
		catch (TransportSettlementException)
		{
			throw;
		}
		catch (Exception ex)
		{
			if (ex is MQException mqException)
			{
				LogSettleFailed(id, commit, mqException.ReasonCode, mqException);
			}

			// A failed native call does not prove broker rollback or redelivery.
			throw CreateSettlementException(
				$"IBM MQ could not {(commit ? "commit" : "back out")} the unit of work for message '{id}'.",
				ex, TransportRedeliveryExpectation.Unspecified);
		}
		finally
		{
			SafeClose(unitOfWork.Queue, unitOfWork.QueueManager);
		}
	}

	/// <summary>
	/// Takes exclusive ownership of the unit of work for <paramref name="id"/>, or throws.
	/// </summary>
	/// <remarks>
	/// The removal is the atomic step deciding which of two concurrent settlements owns the message. This
	/// used to be <c>if (!TryRemove(...)) return;</c> — so settling an unknown id, settling twice, or losing
	/// the race returned normally and told the caller a settlement had happened when nothing had. An
	/// operation that can decline and returns nothing makes "declined" and "acted" the same observation.
	/// </remarks>
	private UnitOfWork ClaimUnitOfWork(string id, string outcome)
	{
		if (_outstanding.TryRemove(id, out var unitOfWork))
		{
			return unitOfWork;
		}

		throw new TransportSettlementException(
			$"IBM MQ cannot {outcome} message '{id}': this receiver holds no outstanding unit of work for it. "
			+ "It was already settled, or it was received by a different receiver.")
		{
			TransportName = "IbmMq",

			// The unit of work is gone, so this receiver can no longer observe the message's fate: a
			// competing settlement may have committed it, backed it out, or moved it.
			RedeliveryExpectation = TransportRedeliveryExpectation.Unspecified,
			Retryability = SettlementRetryability.Permanent,
		};
	}

	/// <summary>
	/// Honours <c>RejectAsync(requeue: false)</c> by moving the message to the configured backout queue and
	/// committing, so the queue manager does not present it again.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The put and the get commit together.</b> The put is issued on a second queue handle opened from
	/// the <em>same</em> connection and carries <c>MQPMO_SYNCPOINT</c>, so it joins the unit of work the get
	/// already belongs to. A single <c>MQCMIT</c> then commits both. One resource manager, one transaction:
	/// the message cannot be both consumed and left, nor both moved and lost.
	/// </para>
	/// <para>
	/// <b>What that does and does not promise.</b> It is all-or-nothing, which is not the same as
	/// no-redelivery. On return, the message is on the backout queue and gone from the input queue. If the
	/// process or the connection dies before the commit, the queue manager rolls the whole unit of work back
	/// — the put is undone and the message returns to the input queue with <c>MQMD.BackoutCount</c>
	/// incremented, so a caller that asked for no redelivery gets one and is not told. That is not a defect
	/// that can be engineered away here: with one resource manager and a mortal caller, suppression can only
	/// be guaranteed for a settlement that completed. The backout count is the bound, and it is reported as
	/// <see cref="TransportReceivedMessage.DeliveryCount"/>.
	/// </para>
	/// </remarks>
	private void SettleToBackoutQueue(string id, string? reason)
	{
		var unitOfWork = ClaimUnitOfWork(id, "reject");

		if (string.IsNullOrWhiteSpace(_receive.BackoutQueueName))
		{
			// NO DESTINATION, SO THE OUTCOME CANNOT BE DELIVERED -- AND IS NOT FAKED.
			// Backing out is the only way to resolve the syncpoint, and it is the opposite of what the
			// caller asked for, so the caller is told rather than allowed to believe the message is gone.
			var backedOut = TryBackout(unitOfWork.QueueManager, out var backoutFailure);
			SafeClose(unitOfWork.Queue, unitOfWork.QueueManager);

			if (backoutFailure is not null)
			{
				throw CreateSettlementException(
					$"IBM MQ cannot reject message '{id}' without requeue because no backout queue is configured; rollback could not be completed.",
					backoutFailure, TransportRedeliveryExpectation.Unspecified);
			}

			throw new TransportSettlementException(
				$"IBM MQ cannot reject message '{id}' without requeue because no backout queue is configured. "
				+ $"Set {nameof(IbmMqReceiveTuningOptions)}.{nameof(IbmMqReceiveTuningOptions.BackoutQueueName)} "
				+ "to the input queue's BOQNAME. "
				+ (backedOut ? "Rollback completed." : "Rollback outcome is unknown."))
			{
				TransportName = "IbmMq",
				RedeliveryExpectation = backedOut ? TransportRedeliveryExpectation.Expected : TransportRedeliveryExpectation.Unspecified,

				// Configuration, not weather. Repeating the call changes nothing.
				Retryability = SettlementRetryability.Permanent,
			};
		}

		IIbmMqQueue? backoutQueue = null;
		var commitAttempted = false;
		try
		{
			backoutQueue = unitOfWork.QueueManager.AccessQueue(
				_receive.BackoutQueueName,
				MQC.MQOO_OUTPUT | MQC.MQOO_FAIL_IF_QUIESCING);

			var putOptions = new MQPutMessageOptions
			{
				// No MQPMO_NEW_MSG_ID: the descriptor's existing message id is kept, so the moved message
				// carries the same identity the consumer saw. A dead-letter store keyed on that identity can
				// therefore still recognise it, which is the obligation the dead-letter decorator states.
				Options = MQC.MQPMO_SYNCPOINT | MQC.MQPMO_FAIL_IF_QUIESCING,
			};

			backoutQueue.Put(unitOfWork.Message, putOptions);

			// Commits the put AND the original get.
			commitAttempted = true;
			unitOfWork.QueueManager.Commit();
			LogRejectedToBackoutQueue(id, Source, _receive.BackoutQueueName, reason ?? "unspecified");
		}
		catch (TransportSettlementException)
		{
			_ = TryBackout(unitOfWork.QueueManager);
			throw;
		}
		catch (Exception ex)
		{
			if (ex is MQException mqException)
			{
				LogBackoutQueuePutFailed(id, _receive.BackoutQueueName, mqException.ReasonCode, mqException);
			}

			var backedOut = TryBackout(unitOfWork.QueueManager);
			// Rollback cannot undo a commit accepted before its response was lost.
			var redelivery = !commitAttempted && backedOut
				? TransportRedeliveryExpectation.Expected : TransportRedeliveryExpectation.Unspecified;
			throw CreateSettlementException(
				$"IBM MQ could not move message '{id}' to backout queue "
				+ $"'{_receive.BackoutQueueName}'.", ex, redelivery);
		}
		finally
		{
			SafeClose(backoutQueue, null);
			SafeClose(unitOfWork.Queue, unitOfWork.QueueManager);
		}
	}

	private static TransportSettlementException CreateSettlementException(string description, Exception cause, TransportRedeliveryExpectation redelivery) =>
		new(description, cause)
		{
			TransportName = "IbmMq",
			RedeliveryExpectation = redelivery,
			// The atomic claim consumed this handle before native I/O; it cannot be retried.
			Retryability = SettlementRetryability.Permanent,
		};

	private static bool TryBackout(IIbmMqQueueManager? queueManager) => TryBackout(queueManager, out _);

	private static bool TryBackout(IIbmMqQueueManager? queueManager, out Exception? failure)
	{
		failure = null;
		try
		{
			if (queueManager is null)
			{
				return false;
			}
			queueManager.Backout();
			return true;
		}
#pragma warning disable CA1031 // Failure handling must preserve the primary settlement failure.
		catch (Exception ex)
		{
			failure = ex;
			return false;
		}
#pragma warning restore CA1031
	}

	private static void SafeClose(IIbmMqQueue? queue, IIbmMqQueueManager? queueManager)
	{
		try
		{
			queue?.Dispose();
		}
#pragma warning disable CA1031 // Each handle must close independently without masking the primary failure.
		catch (Exception)
		{
			// ignored — connection teardown follows
		}

		try
		{
			queueManager?.Dispose();
		}
		catch (Exception)
		{
			// ignored
		}
#pragma warning restore CA1031
	}

	/// <summary>
	/// An outstanding syncpoint: the connection it belongs to, the queue it was got from, and the message
	/// itself.
	/// </summary>
	/// <remarks>
	/// The message is retained because rejecting without requeue has to <b>put it somewhere else</b>, and a
	/// message that has been got under syncpoint cannot be re-read to obtain its content. Retaining it costs
	/// one message per outstanding unit of work, which is already bounded by
	/// <see cref="IbmMqReceiveTuningOptions.MaxOutstandingUnitsOfWork"/>.
	/// </remarks>
	private sealed record UnitOfWork(IIbmMqQueueManager QueueManager, IIbmMqQueue Queue, MQMessage Message);

	[LoggerMessage(EventId = 6110, Level = LogLevel.Error,
		Message = "IBM MQ receive failed from {Source} (reason code {ReasonCode}).")]
	private partial void LogReceiveFailed(string source, int reasonCode, Exception exception);

	[LoggerMessage(EventId = 6111, Level = LogLevel.Error,
		Message = "IBM MQ settle (commit={Commit}) failed for message {MessageId} (reason code {ReasonCode}).")]
	private partial void LogSettleFailed(string messageId, bool commit, int reasonCode, Exception exception);

	[LoggerMessage(EventId = 6113, Level = LogLevel.Warning,
		Message = "IBM MQ message {MessageId} from {Source} rejected without requeue and moved to backout queue {BackoutQueue} (reason {Reason}).")]
	private partial void LogRejectedToBackoutQueue(string messageId, string source, string backoutQueue, string reason);

	[LoggerMessage(EventId = 6114, Level = LogLevel.Error,
		Message = "IBM MQ could not move message {MessageId} to backout queue {BackoutQueue} (reason code {ReasonCode}); backed out instead.")]
	private partial void LogBackoutQueuePutFailed(string messageId, string backoutQueue, int reasonCode, Exception exception);

	[LoggerMessage(EventId = 6112, Level = LogLevel.Warning,
		Message = "IBM MQ message from {Source} rejected: payload {PayloadBytes} bytes exceeds MaxPayloadBytes ({MaxPayloadBytes}); discarded to avoid redelivery of an unprocessable message.")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, int maxPayloadBytes);
}
