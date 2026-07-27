// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotPulsar;
using DotPulsar.Abstractions;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.Pulsar.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// Sends <see cref="TransportMessage"/> instances to an Apache Pulsar topic via a DotPulsar producer.
/// </summary>
internal sealed partial class PulsarTransportSender : ITransportSender
{
	private readonly IProducer<byte[]> _producer;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PulsarTransportSender"/> class.
	/// </summary>
	/// <param name="producer">The DotPulsar producer bound to the destination topic.</param>
	/// <param name="destination">The destination topic name.</param>
	/// <param name="logger">The logger.</param>
	public PulsarTransportSender(
		IProducer<byte[]> producer,
		string destination,
		ILogger<PulsarTransportSender> logger)
	{
		_producer = producer ?? throw new ArgumentNullException(nameof(producer));
		Destination = destination ?? throw new ArgumentNullException(nameof(destination));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Destination { get; }

	/// <inheritdoc />
	public async Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		try
		{
			var metadata = CreateMetadata(message);
			var messageId = await _producer.Send(metadata, message.Body.ToArray(), cancellationToken).ConfigureAwait(false);
			LogMessageSent(message.Id, Destination, messageId.ToString());
			return new SendResult
			{
				IsSuccess = true,
				MessageId = message.Id,
				Partition = messageId.Partition.ToString(System.Globalization.CultureInfo.InvariantCulture),
				SequenceNumber = (long)messageId.EntryId,
				AcceptedAt = DateTimeOffset.UtcNow,
			};
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogSendFailed(message.Id, Destination, ex);
			return SendResult.Failure(SendError.FromException(ex));
		}
	}

	/// <inheritdoc />
	public async Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);
		if (messages.Count == 0)
		{
			return new BatchSendResult { TotalMessages = 0, SuccessCount = 0, FailureCount = 0 };
		}

		var results = new List<SendResult>(messages.Count);
		foreach (var message in messages)
		{
			results.Add(await SendAsync(message, cancellationToken).ConfigureAwait(false));
		}

		var successCount = results.Count(static r => r.IsSuccess);
		LogBatchSent(Destination, messages.Count, successCount);
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
		// DotPulsar sends are awaited on the broker acknowledgement per message, so there is no
		// client-side buffer to flush. FlushAsync is a no-op that honors cancellation.
		cancellationToken.ThrowIfCancellationRequested();
		LogFlushed(Destination);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return serviceType == typeof(IProducer<byte[]>) ? _producer : null;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		await _producer.DisposeAsync().ConfigureAwait(false);
		LogDisposed(Destination);
		GC.SuppressFinalize(this);
	}

	private static MessageMetadata CreateMetadata(TransportMessage message)
	{
		var metadata = new MessageMetadata();

		if (message.ContentType is not null)
		{
			metadata["content-type"] = message.ContentType;
		}
		if (message.MessageType is not null)
		{
			metadata["message-type"] = message.MessageType;
		}
		if (message.CorrelationId is not null)
		{
			metadata[OutboxHeaderNames.CorrelationId] = message.CorrelationId;
		}
		if (message.CausationId is not null)
		{
			metadata[OutboxHeaderNames.CausationId] = message.CausationId;
		}
		if (message.Subject is not null)
		{
			metadata["subject"] = message.Subject;
		}
		metadata["message-id"] = message.Id;

		if (message.HasProperties)
		{
			foreach (var (key, value) in message.Properties)
			{
				if (!key.StartsWith("dispatch.", StringComparison.Ordinal))
				{
					metadata[key] = value?.ToString() ?? string.Empty;
				}
			}
		}

		var partitionKey = GetPartitionKey(message);
		if (partitionKey is not null)
		{
			metadata.Key = partitionKey;
		}

		return metadata;
	}

	private static string? GetPartitionKey(TransportMessage message)
	{
		if (!message.HasProperties)
		{
			return null;
		}
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.OrderingKey, out var orderingKey) &&
			orderingKey is string orderingKeyStr)
		{
			return orderingKeyStr;
		}
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.PartitionKey, out var partitionKey) &&
			partitionKey is string partitionKeyStr)
		{
			return partitionKeyStr;
		}
		return null;
	}

	[LoggerMessage(PulsarEventId.TransportSenderMessageSent, LogLevel.Debug,
		"Pulsar transport sender: message {MessageId} sent to {Destination} [pulsarMessageId={PulsarMessageId}]")]
	private partial void LogMessageSent(string messageId, string destination, string pulsarMessageId);

	[LoggerMessage(PulsarEventId.TransportSenderSendFailed, LogLevel.Error,
		"Pulsar transport sender: failed to send message {MessageId} to {Destination}")]
	private partial void LogSendFailed(string messageId, string destination, Exception exception);

	[LoggerMessage(PulsarEventId.TransportSenderBatchSent, LogLevel.Debug,
		"Pulsar transport sender: batch of {Count} messages sent to {Destination}, {SuccessCount} succeeded")]
	private partial void LogBatchSent(string destination, int count, int successCount);

	[LoggerMessage(PulsarEventId.TransportSenderFlushed, LogLevel.Debug,
		"Pulsar transport sender: flush requested for {Destination} (no-op; sends are broker-acknowledged)")]
	private partial void LogFlushed(string destination);

	[LoggerMessage(PulsarEventId.TransportSenderDisposed, LogLevel.Debug,
		"Pulsar transport sender disposed for {Destination}")]
	private partial void LogDisposed(string destination);
}
