// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Google Cloud Pub/Sub implementation of <see cref="ITransportSender"/>.
/// Uses <see cref="PublisherServiceApiClient"/> for native message production.
/// </summary>
/// <remarks>
/// <para>
/// Reads well-known property keys from <see cref="TransportMessage.Properties"/>:
/// </para>
/// <list type="bullet">
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.OrderingKey"/> maps to <c>PubsubMessage.OrderingKey</c>.</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.DeduplicationId"/> maps to a message attribute.</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.ScheduledTime"/> maps to a message attribute.</item>
/// </list>
/// </remarks>
internal sealed partial class PubSubTransportSender : ITransportSender
{
	private readonly PublisherServiceApiClient _client;
	private readonly TimeSpan _requestTimeout;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTransportSender"/> class.
	/// </summary>
	/// <param name="client">The Pub/Sub publisher service API client.</param>
	/// <param name="topicName">The fully qualified topic name.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="requestTimeout">Optional request timeout.</param>
	public PubSubTransportSender(
		PublisherServiceApiClient client,
		string topicName,
		ILogger<PubSubTransportSender> logger,
		TimeSpan requestTimeout = default)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		Destination = topicName ?? throw new ArgumentNullException(nameof(topicName));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_requestTimeout = requestTimeout;
	}

	/// <inheritdoc />
	public string Destination { get; }

	/// <inheritdoc />
	public async Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		try
		{
			var pubsubMessage = CreatePubsubMessage(message);

			var response = await _client.PublishAsync(
					new PublishRequest { Topic = Destination, Messages = { pubsubMessage } },
					CreateCallSettings(cancellationToken))
				.ConfigureAwait(false);

			LogMessageSent(message.Id, Destination);

			return new SendResult
			{
				IsSuccess = true,
				MessageId = response.MessageIds.Count > 0 ? response.MessageIds[0] : message.Id,
				AcceptedAt = DateTimeOffset.UtcNow,
			};
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

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			var pubsubMessages = new List<PubsubMessage>(messages.Count);
			foreach (var message in messages)
			{
				pubsubMessages.Add(CreatePubsubMessage(message));
			}

			var response = await _client.PublishAsync(
					new PublishRequest { Topic = Destination, Messages = { pubsubMessages } },
					CreateCallSettings(cancellationToken))
				.ConfigureAwait(false);

			var results = new List<SendResult>(messages.Count);
			for (var i = 0; i < messages.Count; i++)
			{
				results.Add(new SendResult
				{
					IsSuccess = true,
					MessageId = i < response.MessageIds.Count ? response.MessageIds[i] : messages[i].Id,
					AcceptedAt = DateTimeOffset.UtcNow,
				});
			}

			LogBatchSent(Destination, messages.Count, messages.Count);

			return new BatchSendResult
			{
				TotalMessages = messages.Count,
				SuccessCount = messages.Count,
				FailureCount = 0,
				Results = results,
				Duration = stopwatch.Elapsed,
			};
		}
		catch (Exception ex)
		{
			LogBatchSendFailed(Destination, messages.Count, ex);

			var failedResults = new List<SendResult>(messages.Count);
			for (var i = 0; i < messages.Count; i++)
			{
				failedResults.Add(SendResult.Failure(SendError.FromException(ex)));
			}

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
		// Pub/Sub publishes are immediately committed; no buffering to flush.
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(PublisherServiceApiClient))
		{
			return _client;
		}

		return null;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		LogDisposed(Destination);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	private static PubsubMessage CreatePubsubMessage(TransportMessage message)
	{
		var pubsubMessage = new PubsubMessage
		{
			Data = message.Body.IsEmpty
				? ByteString.Empty
				: ByteString.CopyFrom(message.Body.Span),
		};

		// Map message metadata to attributes
		pubsubMessage.Attributes["message-id"] = message.Id;

		if (!string.IsNullOrWhiteSpace(message.ContentType))
		{
			pubsubMessage.Attributes["content-type"] = message.ContentType;
		}

		if (!string.IsNullOrWhiteSpace(message.CorrelationId))
		{
			pubsubMessage.Attributes["correlation-id"] = message.CorrelationId;
		}

		if (!string.IsNullOrWhiteSpace(message.MessageType))
		{
			pubsubMessage.Attributes["message-type"] = message.MessageType;
		}

		if (!string.IsNullOrWhiteSpace(message.Subject))
		{
			pubsubMessage.Attributes["subject"] = message.Subject;
		}

		// Map well-known properties to Pub/Sub native concepts
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.OrderingKey, out var orderingKey) &&
			orderingKey is string orderingKeyStr && !string.IsNullOrWhiteSpace(orderingKeyStr))
		{
			pubsubMessage.OrderingKey = orderingKeyStr;
			pubsubMessage.Attributes["ordering-key"] = orderingKeyStr;
		}

		// Copy custom properties as attributes
		foreach (var (key, value) in message.Properties)
		{
			if (!key.StartsWith("dispatch.", StringComparison.Ordinal) && value is not null)
			{
				pubsubMessage.Attributes[key] = value.ToString() ?? string.Empty;
			}
		}

		return pubsubMessage;
	}

	private CallSettings CreateCallSettings(CancellationToken cancellationToken)
	{
		var callSettings = CallSettings.FromCancellationToken(cancellationToken);
		if (_requestTimeout > TimeSpan.Zero)
		{
			callSettings = callSettings.WithExpiration(Expiration.FromTimeout(_requestTimeout));
		}

		return callSettings;
	}

	[LoggerMessage(GooglePubSubEventId.TransportSenderMessageSent, LogLevel.Debug,
		"Pub/Sub transport sender: message {MessageId} sent to {Destination}")]
	private partial void LogMessageSent(string messageId, string destination);

	[LoggerMessage(GooglePubSubEventId.TransportSenderSendFailed, LogLevel.Error,
		"Pub/Sub transport sender: failed to send message {MessageId} to {Destination}")]
	private partial void LogSendFailed(string messageId, string destination, Exception exception);

	[LoggerMessage(GooglePubSubEventId.TransportSenderBatchSent, LogLevel.Debug,
		"Pub/Sub transport sender: batch of {Count} messages sent to {Destination}, {SuccessCount} succeeded")]
	private partial void LogBatchSent(string destination, int count, int successCount);

	[LoggerMessage(GooglePubSubEventId.TransportSenderBatchSendFailed, LogLevel.Error,
		"Pub/Sub transport sender: batch send of {Count} messages to {Destination} failed")]
	private partial void LogBatchSendFailed(string destination, int count, Exception exception);

	[LoggerMessage(GooglePubSubEventId.TransportSenderDisposed, LogLevel.Debug,
		"Pub/Sub transport sender disposed for {Destination}")]
	private partial void LogDisposed(string destination);
}
