// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using IBM.WMQ;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// Receives messages from an IBM MQ queue under a unit of work per message: each received message holds its
/// own queue-manager connection and syncpoint, so
/// <see cref="AcknowledgeAsync"/> commits (removes) exactly that message and <see cref="RejectAsync"/>
/// backs it out (redelivers) — true per-message ack/reject in any order. Outstanding units of work are
/// bounded by the caller's <c>maxMessages</c> and are always committed or backed out (never leaked),
/// including on <see cref="DisposeAsync"/> and cancellation.
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
		Settle(message.Id, commit: true);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		// Backing out an uncommitted syncpoint get redelivers the message. A non-requeue reject would route
		// to the backout-requeue/dead-letter queue; that DLQ path is a follow-on (tracked), so W2 backs out.
		cancellationToken.ThrowIfCancellationRequested();
		Settle(message.Id, commit: false);
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
			Settle(id, commit: false);
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
		MQQueueManager? queueManager = null;
		MQQueue? queue = null;
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
			_outstanding[id] = new UnitOfWork(queueManager, queue);
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

		return new TransportReceivedMessage
		{
			Id = id,
			Body = body,
			CorrelationId = correlationId,
			Source = Source,
			DeliveryCount = mqMessage.BackoutCount + 1,
			EnqueuedAt = DateTimeOffset.UtcNow,
		};
	}

	private void Settle(string id, bool commit)
	{
		if (!_outstanding.TryRemove(id, out var unitOfWork))
		{
			return;
		}

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
		catch (MQException ex)
		{
			LogSettleFailed(id, commit, ex.ReasonCode, ex);
		}
		finally
		{
			SafeClose(unitOfWork.Queue, unitOfWork.QueueManager);
		}
	}

	private static void TryBackout(MQQueueManager? queueManager)
	{
		try
		{
			queueManager?.Backout();
		}
		catch (MQException)
		{
			// best-effort during failure handling
		}
	}

	private static void SafeClose(MQQueue? queue, MQQueueManager? queueManager)
	{
		try
		{
			queue?.Close();
		}
		catch (MQException)
		{
			// ignored — connection teardown follows
		}

		try
		{
			queueManager?.Disconnect();
		}
		catch (MQException)
		{
			// ignored
		}
	}

	private sealed record UnitOfWork(MQQueueManager QueueManager, MQQueue Queue);

	[LoggerMessage(EventId = 6110, Level = LogLevel.Error,
		Message = "IBM MQ receive failed from {Source} (reason code {ReasonCode}).")]
	private partial void LogReceiveFailed(string source, int reasonCode, Exception exception);

	[LoggerMessage(EventId = 6111, Level = LogLevel.Error,
		Message = "IBM MQ settle (commit={Commit}) failed for message {MessageId} (reason code {ReasonCode}).")]
	private partial void LogSettleFailed(string messageId, bool commit, int reasonCode, Exception exception);

	[LoggerMessage(EventId = 6112, Level = LogLevel.Warning,
		Message = "IBM MQ message from {Source} rejected: payload {PayloadBytes} bytes exceeds MaxPayloadBytes ({MaxPayloadBytes}); discarded to avoid redelivery of an unprocessable message.")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, int maxPayloadBytes);
}
