// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	/// <remarks>
	/// Cancellation stops the batch; it does not discard what the batch already knows. Every input still
	/// gets a result, because a caller who is told only "cancelled" cannot tell which messages were sent
	/// and has no safe move left but to resend all of them — which on an at-least-once transport
	/// duplicates every message that already arrived. Messages not reached, and the one in flight when
	/// cancellation landed, are reported as retryable failures: their delivery is unknown, and a duplicate
	/// is recoverable where a silent loss is not.
	/// </remarks>
	public async Task<BatchSendResult> SendBatchAsync(
		IReadOnlyList<TransportMessage> messages,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var results = new List<SendResult>(messages.Count);
		foreach (var message in messages)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			results.Add(await SendAsync(message, cancellationToken).ConfigureAwait(false));
		}

		// Cancellation is reported per input rather than thrown, so the caller can retry precisely the
		// messages whose delivery is unknown. See BatchSendResult.Results for the contract.
		while (results.Count < messages.Count)
		{
			results.Add(SendResult.Failure(new SendError
			{
				Code = "Canceled",
				Message = "The batch was cancelled; this message is not confirmed sent.",
				IsRetryable = true,
			}));
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
		IIbmMqQueueManager? queueManager = null;
		IIbmMqQueue? queue = null;
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

	/// <summary>
	/// The message property carrying the MIME content type, which the MQMD cannot express.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>UNDERSCORE, NOT HYPHEN, AND THAT IS THE PLATFORM'S RULE RATHER THAN A PREFERENCE.</b> IBM MQ
	/// validates property names as Java identifiers, permitting <c>.</c> but no hyphen; setting
	/// <c>content-type</c> is rejected with <c>MQRC_PROPERTY_NAME_ERROR</c> (2442). The conventional
	/// hyphenated spelling every other transport uses is therefore not merely unconventional here, it is
	/// unsettable — so this transport cannot interoperate on that name however much we would prefer to, and
	/// the underscore form matches the naming this codebase already uses for CloudEvents attributes on MQ.
	/// </para>
	/// <para>
	/// Shared with <c>IbmMqTransportReceiver</c>, which reads this property in place of the MQMD wire-format
	/// tag. Both sides must name it identically or the round trip silently loses the content type.
	/// </para>
	/// </remarks>
	internal const string ContentTypePropertyName = "content_type";

	private MQMessage BuildMessage(TransportMessage message)
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

		// THE CONTENT TYPE TRAVELS AS A PROPERTY BECAUSE THE MQMD HAS NOWHERE TO PUT IT. MQMD's Format is a
		// fixed-width wire-format tag drawn from a closed set of MQ's own values (MQSTR, MQHRF2, MQFMT_NONE);
		// it is not a MIME type field and a media type does not fit in it. Until now this sender simply
		// discarded the caller's content type, so a receiver had nothing to read and the receive side
		// substituted the wire-format tag -- which is why this transport could not carry a structured-mode
		// CloudEvent, whose ONLY identifier is its media type.
		//
		// The name is unprefixed rather than "dispatch.*" so a producer outside this framework can set it,
		// and it uses an underscore because IBM MQ REJECTS the conventional hyphenated spelling outright.
		// See ContentTypePropertyName for the platform rule.
		if (!string.IsNullOrEmpty(message.ContentType))
		{
			mqMessage.SetStringProperty(ContentTypePropertyName, message.ContentType);
		}

		ApplyProperties(mqMessage, message);

		mqMessage.Write(message.Body.ToArray());
		return mqMessage;
	}

	/// <summary>
	/// Copies the caller's message properties onto the MQ message.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Previously absent, which silently discarded every property a caller set.</b> The receiver has
	/// always read the full property set back (it enumerates with IBM MQ's <c>"%"</c> wildcard), so the
	/// send path was the only asymmetric half: a caller could set properties, observe no error, and have
	/// them vanish before the message reached the queue.
	/// </para>
	/// <para>
	/// <b>A rejected property name can never fail the send.</b> IBM MQ constrains property names to the
	/// Java identifier grammar — a hyphen, a leading digit, a reserved prefix or a misplaced <c>.</c> is
	/// refused with <see cref="MQException"/> — and those names are consumer-supplied, so validating them
	/// here would mean re-implementing a vendor grammar this code cannot keep in step with. Instead the
	/// broker is the authority: a refused property is logged with the reason code it was refused for and
	/// skipped, and the message is still delivered. Turning a property-name mistake into an undeliverable
	/// message would be a worse defect than the one this fixes.
	/// </para>
	/// <para>
	/// Values are written as strings, matching what the receiver reconstructs and the convention the other
	/// property-carrying transports in this framework already follow.
	/// </para>
	/// </remarks>
	private void ApplyProperties(MQMessage mqMessage, TransportMessage message)
	{
		if (!message.HasProperties)
		{
			return;
		}

		foreach (var (name, value) in message.Properties)
		{
			if (value is null)
			{
				continue;
			}

			try
			{
				mqMessage.SetStringProperty(name, value as string ?? value.ToString());
			}
			catch (MQException ex)
			{
				LogPropertyRejected(name, message.Id, Destination, ex.ReasonCode);
			}
		}
	}

	private static bool IsRetryable(MQException ex) =>
		ex.ReasonCode is MQC.MQRC_CONNECTION_BROKEN or MQC.MQRC_Q_MGR_NOT_AVAILABLE or MQC.MQRC_Q_MGR_QUIESCING;

	private static void TryBackout(IIbmMqQueueManager? queueManager)
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

	private static void SafeClose(IIbmMqQueue? queue, IIbmMqQueueManager? queueManager)
	{
		try
		{
			queue?.Dispose();
		}
		catch (MQException)
		{
			// ignored — connection teardown follows
		}

		try
		{
			queueManager?.Dispose();
		}
		catch (MQException)
		{
			// ignored
		}
	}

	[LoggerMessage(EventId = 6101, Level = LogLevel.Warning,
		Message = "IBM MQ refused message property '{PropertyName}' on message {MessageId} to {Destination} (reason code {ReasonCode}); the property was dropped and the message was still sent.")]
	private partial void LogPropertyRejected(string propertyName, string messageId, string destination, int reasonCode);

	[LoggerMessage(EventId = 6100, Level = LogLevel.Error,
		Message = "IBM MQ send failed for message {MessageId} to {Destination} (reason code {ReasonCode}).")]
	private partial void LogSendFailed(string messageId, string destination, int reasonCode, Exception exception);
}
