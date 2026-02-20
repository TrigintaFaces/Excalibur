// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Azure;
using Azure.Messaging.EventGrid;

using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AzureCloudEvent = Azure.Messaging.CloudEvent;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Event Grid implementation of <see cref="ITransportSender"/>.
/// Publishes events using <see cref="EventGridPublisherClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// Supports both CloudEvents and Event Grid native schema via <see cref="EventGridTransportOptions.SchemaMode"/>.
/// CloudEvents schema is recommended and is the default.
/// </para>
/// <para>
/// Event Grid is a push-only service, so <see cref="ITransportReceiver"/> is not applicable.
/// For receiving events, use webhook subscriptions or Azure Functions Event Grid triggers.
/// </para>
/// </remarks>
internal sealed partial class EventGridTransportSender : ITransportSender
{
	private readonly EventGridPublisherClient _client;
	private readonly EventGridTransportOptions _options;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventGridTransportSender"/> class.
	/// </summary>
	/// <param name="client">The Event Grid publisher client.</param>
	/// <param name="options">The transport options.</param>
	/// <param name="logger">The logger instance.</param>
	public EventGridTransportSender(
		EventGridPublisherClient client,
		IOptions<EventGridTransportOptions> options,
		ILogger<EventGridTransportSender> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Destination => _options.Destination;

	/// <inheritdoc />
	public async Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		try
		{
			if (_options.SchemaMode == EventGridSchemaMode.CloudEvents)
			{
				var cloudEvent = MapToCloudEvent(message);
				await _client.SendEventAsync(cloudEvent, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				var eventGridEvent = MapToEventGridEvent(message);
				await _client.SendEventAsync(eventGridEvent, cancellationToken).ConfigureAwait(false);
			}

			LogMessageSent(message.Id, Destination);
			return SendResult.Success(message.Id);
		}
		catch (RequestFailedException ex)
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

		var stopwatch = Stopwatch.StartNew();

		try
		{
			if (_options.SchemaMode == EventGridSchemaMode.CloudEvents)
			{
				var cloudEvents = messages.Select(MapToCloudEvent).ToList();
				await _client.SendEventsAsync(cloudEvents, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				var eventGridEvents = messages.Select(MapToEventGridEvent).ToList();
				await _client.SendEventsAsync(eventGridEvents, cancellationToken).ConfigureAwait(false);
			}

			stopwatch.Stop();

			var results = messages.Select(m => SendResult.Success(m.Id)).ToList();
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
		catch (RequestFailedException ex)
		{
			stopwatch.Stop();
			LogBatchSendFailed(Destination, messages.Count, ex);

			var failedResults = messages.Select(_ =>
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
		// Event Grid sends are immediately committed; no buffering to flush.
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(EventGridPublisherClient))
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

	private static bool IsTransient(RequestFailedException ex) =>
		ex.Status is (>= 500 and < 600) or 429;

	private AzureCloudEvent MapToCloudEvent(TransportMessage message)
	{
		var eventType = message.MessageType ?? _options.DefaultEventType;
		var source = _options.DefaultEventSource;

		var cloudEvent = new AzureCloudEvent(
			source,
			eventType,
			new BinaryData(message.Body),
			message.ContentType ?? "application/octet-stream")
		{ Id = message.Id, Subject = message.Subject, };

		if (message.CorrelationId is not null)
		{
			cloudEvent.ExtensionAttributes["correlationid"] = message.CorrelationId;
		}

		return cloudEvent;
	}

	private EventGridEvent MapToEventGridEvent(TransportMessage message)
	{
		var eventType = message.MessageType ?? _options.DefaultEventType;
		var subject = message.Subject ?? "/";

		return new EventGridEvent(
			subject,
			eventType,
			"1.0",
			new BinaryData(message.Body))
		{ Id = message.Id, };
	}

	// Event IDs: Reuse 24970+ range for EventGrid sender within Azure transport project
	// Using 24975-24979 subrange for Event Grid (follows StorageQueues at 24900+)

	[LoggerMessage(AzureServiceBusEventId.EventGridSenderMessageSent, LogLevel.Debug,
		"Event Grid transport sender: message {MessageId} sent to {Destination}")]
	private partial void LogMessageSent(string messageId, string destination);

	[LoggerMessage(AzureServiceBusEventId.EventGridSenderSendFailed, LogLevel.Error,
		"Event Grid transport sender: failed to send message {MessageId} to {Destination}")]
	private partial void LogSendFailed(string messageId, string destination, Exception exception);

	[LoggerMessage(AzureServiceBusEventId.EventGridSenderBatchSent, LogLevel.Debug,
		"Event Grid transport sender: batch of {Count} messages sent to {Destination}, {SuccessCount} succeeded")]
	private partial void LogBatchSent(string destination, int count, int successCount);

	[LoggerMessage(AzureServiceBusEventId.EventGridSenderBatchSendFailed, LogLevel.Error,
		"Event Grid transport sender: batch send of {Count} messages to {Destination} failed")]
	private partial void LogBatchSendFailed(string destination, int count, Exception exception);

	[LoggerMessage(AzureServiceBusEventId.EventGridSenderDisposed, LogLevel.Debug,
		"Event Grid transport sender disposed for {Destination}")]
	private partial void LogDisposed(string destination);
}
