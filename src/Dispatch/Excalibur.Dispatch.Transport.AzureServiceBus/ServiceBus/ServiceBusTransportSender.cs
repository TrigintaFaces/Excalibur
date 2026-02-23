// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using System.Globalization;

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.AzureServiceBus;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Service Bus implementation of <see cref="ITransportSender"/>.
/// Uses <see cref="ServiceBusSender"/> for native message production.
/// </summary>
/// <remarks>
/// <para>
/// Reads well-known property keys from <see cref="TransportMessage.Properties"/>:
/// </para>
/// <list type="bullet">
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.OrderingKey"/> maps to <c>SessionId</c>.</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.PartitionKey"/> maps to <c>PartitionKey</c>.</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.ScheduledTime"/> maps to <c>ScheduledEnqueueTime</c>.</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.DeduplicationId"/> maps to <c>MessageId</c> for dedup.</item>
/// </list>
/// </remarks>
internal sealed partial class ServiceBusTransportSender : ITransportSender
{
	private readonly ServiceBusSender _sender;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusTransportSender"/> class.
	/// </summary>
	/// <param name="sender">The Azure Service Bus sender.</param>
	/// <param name="destination">The destination queue or topic name.</param>
	/// <param name="logger">The logger instance.</param>
	public ServiceBusTransportSender(
		ServiceBusSender sender,
		string destination,
		ILogger<ServiceBusTransportSender> logger)
	{
		_sender = sender ?? throw new ArgumentNullException(nameof(sender));
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
			var serviceBusMessage = CreateServiceBusMessage(message);

			// Check for scheduled delivery
			if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.ScheduledTime, out var scheduledObj) &&
				scheduledObj is string scheduledStr &&
				DateTimeOffset.TryParse(scheduledStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var scheduledTime))
			{
				var sequenceNumber = await _sender.ScheduleMessageAsync(serviceBusMessage, scheduledTime, cancellationToken)
					.ConfigureAwait(false);

				LogMessageSent(message.Id, Destination);
				return new SendResult
				{
					IsSuccess = true,
					MessageId = message.Id,
					SequenceNumber = sequenceNumber,
					AcceptedAt = DateTimeOffset.UtcNow,
				};
			}

			await _sender.SendMessageAsync(serviceBusMessage, cancellationToken).ConfigureAwait(false);
			LogMessageSent(message.Id, Destination);

			return SendResult.Success(message.Id);
		}
		catch (Exception ex)
		{
			LogSendFailed(message.Id, Destination, ex);
			return SendResult.Failure(SendError.FromException(ex, IsTransient(ex)));
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

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			using var batch = await _sender.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);
			var results = new List<SendResult>(messages.Count);
			var overflow = new List<TransportMessage>();

			foreach (var message in messages)
			{
				var sbMessage = CreateServiceBusMessage(message);
				if (!batch.TryAddMessage(sbMessage))
				{
					overflow.Add(message);
				}
				else
				{
					results.Add(SendResult.Success(message.Id));
				}
			}

			await _sender.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);

			// Send overflow individually
			foreach (var message in overflow)
			{
				var result = await SendAsync(message, cancellationToken).ConfigureAwait(false);
				results.Add(result);
			}
			var successCount = results.Count(static r => r.IsSuccess);

			LogBatchSent(Destination, messages.Count, successCount);

			return new BatchSendResult
			{
				TotalMessages = messages.Count,
				SuccessCount = successCount,
				FailureCount = messages.Count - successCount,
				Results = results,
				Duration = stopwatch.Elapsed,
			};
		}
		catch (Exception ex)
		{
			LogBatchSendFailed(Destination, messages.Count, ex);

			var failedResults = messages.Select(m =>
				SendResult.Failure(SendError.FromException(ex, IsTransient(ex)))).ToList();

			return new BatchSendResult
			{
				TotalMessages = messages.Count,
				SuccessCount = 0,
				FailureCount = messages.Count,
				Results = failedResults,
				Duration = stopwatch.Elapsed,
			};
		}
	}

	/// <inheritdoc />
	public Task FlushAsync(CancellationToken cancellationToken)
	{
		// Azure Service Bus sends are immediately committed; no buffering to flush.
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(ServiceBusSender))
		{
			return _sender;
		}

		return null;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await _sender.DisposeAsync().ConfigureAwait(false);
		LogDisposed(Destination);
		GC.SuppressFinalize(this);
	}

	private static ServiceBusMessage CreateServiceBusMessage(TransportMessage message)
	{
		var sbMessage = new ServiceBusMessage(message.Body)
		{
			MessageId = message.Id,
			ContentType = message.ContentType,
			Subject = message.Subject,
			CorrelationId = message.CorrelationId,
		};

		if (message.TimeToLive.HasValue)
		{
			sbMessage.TimeToLive = message.TimeToLive.Value;
		}

		// Map well-known properties to Azure Service Bus native concepts
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.OrderingKey, out var sessionId) &&
			sessionId is string sessionIdStr)
		{
			sbMessage.SessionId = sessionIdStr;
		}

		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.PartitionKey, out var partitionKey) &&
			partitionKey is string partitionKeyStr)
		{
			sbMessage.PartitionKey = partitionKeyStr;
		}

		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.DeduplicationId, out var dedupId) &&
			dedupId is string dedupIdStr)
		{
			sbMessage.MessageId = dedupIdStr;
		}

		if (message.MessageType is not null)
		{
			sbMessage.ApplicationProperties["message-type"] = message.MessageType;
		}

		// Copy custom properties
		foreach (var (key, value) in message.Properties)
		{
			if (!key.StartsWith("dispatch.", StringComparison.Ordinal))
			{
				sbMessage.ApplicationProperties[key] = value;
			}
		}

		return sbMessage;
	}

	private static bool IsTransient(Exception ex) =>
		ex is ServiceBusException sbEx && sbEx.IsTransient;

	[LoggerMessage(AzureServiceBusEventId.TransportSenderMessageSent, LogLevel.Debug,
		"Service Bus transport sender: message {MessageId} sent to {Destination}")]
	private partial void LogMessageSent(string messageId, string destination);

	[LoggerMessage(AzureServiceBusEventId.TransportSenderSendFailed, LogLevel.Error,
		"Service Bus transport sender: failed to send message {MessageId} to {Destination}")]
	private partial void LogSendFailed(string messageId, string destination, Exception exception);

	[LoggerMessage(AzureServiceBusEventId.TransportSenderBatchSent, LogLevel.Debug,
		"Service Bus transport sender: batch of {Count} messages sent to {Destination}, {SuccessCount} succeeded")]
	private partial void LogBatchSent(string destination, int count, int successCount);

	[LoggerMessage(AzureServiceBusEventId.TransportSenderBatchSendFailed, LogLevel.Error,
		"Service Bus transport sender: batch send of {Count} messages to {Destination} failed")]
	private partial void LogBatchSendFailed(string destination, int count, Exception exception);

	[LoggerMessage(AzureServiceBusEventId.TransportSenderDisposed, LogLevel.Debug,
		"Service Bus transport sender disposed for {Destination}")]
	private partial void LogDisposed(string destination);
}
