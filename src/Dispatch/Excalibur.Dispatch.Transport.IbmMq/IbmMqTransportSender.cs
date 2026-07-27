// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using IBM.WMQ;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// Sends messages to an IBM MQ queue. Each send opens a queue manager connection, puts the message under a
/// unit of work, commits, and disconnects — so a failed put never leaves an uncommitted message.
/// </summary>
internal sealed partial class IbmMqTransportSender : ITransportSender
{
	private readonly IIbmMqConnectionProvider _connectionProvider;
	private readonly ILogger<IbmMqTransportSender> _logger;
	private volatile bool _disposed;

	public IbmMqTransportSender(
		IIbmMqConnectionProvider connectionProvider,
		string destination,
		ILogger<IbmMqTransportSender> logger)
	{
		_connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
		Destination = destination ?? throw new ArgumentNullException(nameof(destination));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Destination { get; }

	/// <inheritdoc />
	public Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ObjectDisposedException.ThrowIf(_disposed, this);
		cancellationToken.ThrowIfCancellationRequested();

		// The IBM MQ managed client API is synchronous — there is no async I/O to await, so the put runs
		// inline and completes synchronously (callers drive sends from their own pump/background task).
		return Task.FromResult(PutSingle(message));
	}

	/// <inheritdoc />
	public async Task<BatchSendResult> SendBatchAsync(
		IReadOnlyList<TransportMessage> messages,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var results = new List<SendResult>(messages.Count);
		foreach (var message in messages)
		{
			cancellationToken.ThrowIfCancellationRequested();
			results.Add(await SendAsync(message, cancellationToken).ConfigureAwait(false));
		}

		var successCount = results.Count(static r => r.IsSuccess);
		return new BatchSendResult
		{
			TotalMessages = messages.Count,
			SuccessCount = successCount,
			FailureCount = messages.Count - successCount,
			Results = results,
		};
	}

	/// <inheritdoc />
	public Task FlushAsync(CancellationToken cancellationToken)
	{
		// Each send commits its own unit of work, so there is nothing buffered to flush.
		ObjectDisposedException.ThrowIf(_disposed, this);
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
		_disposed = true;
		return ValueTask.CompletedTask;
	}

	private SendResult PutSingle(TransportMessage message)
	{
		MQQueueManager? queueManager = null;
		MQQueue? queue = null;
		try
		{
			queueManager = _connectionProvider.CreateQueueManager();
			queue = queueManager.AccessQueue(Destination, MQC.MQOO_OUTPUT | MQC.MQOO_FAIL_IF_QUIESCING);

			var mqMessage = BuildMessage(message);
			queue.Put(mqMessage, new MQPutMessageOptions { Options = MQC.MQPMO_SYNCPOINT });
			queueManager.Commit();

			return SendResult.Success(message.Id);
		}
		catch (MQException ex)
		{
			TryBackout(queueManager);
			LogSendFailed(message.Id, Destination, ex.ReasonCode, ex);
			return SendResult.Failure(SendError.FromException(ex, isRetryable: IsRetryable(ex)));
		}
		finally
		{
			SafeClose(queue, queueManager);
		}
	}

	private static MQMessage BuildMessage(TransportMessage message)
	{
		var mqMessage = new MQMessage
		{
			Format = MQC.MQFMT_NONE,
			CharacterSet = 1208, // UTF-8
		};

		if (!string.IsNullOrEmpty(message.CorrelationId))
		{
			mqMessage.CorrelationId = System.Text.Encoding.UTF8.GetBytes(message.CorrelationId);
		}

		if (message.MessageType is not null)
		{
			mqMessage.SetStringProperty("dispatch.messageType", message.MessageType);
		}

		mqMessage.Write(message.Body.ToArray());
		return mqMessage;
	}

	private static bool IsRetryable(MQException ex) =>
		ex.ReasonCode is MQC.MQRC_CONNECTION_BROKEN or MQC.MQRC_Q_MGR_NOT_AVAILABLE or MQC.MQRC_Q_MGR_QUIESCING;

	private static void TryBackout(MQQueueManager? queueManager)
	{
		try
		{
			queueManager?.Backout();
		}
		catch (MQException)
		{
			// Backout is best-effort during failure handling; the connection is torn down next.
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

	[LoggerMessage(EventId = 6100, Level = LogLevel.Error,
		Message = "IBM MQ send failed for message {MessageId} to {Destination} (reason code {ReasonCode}).")]
	private partial void LogSendFailed(string messageId, string destination, int reasonCode, Exception exception);
}
