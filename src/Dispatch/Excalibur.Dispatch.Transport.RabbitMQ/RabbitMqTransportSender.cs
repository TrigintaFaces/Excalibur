// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of <see cref="ITransportSender"/>.
/// Uses RabbitMQ.Client v7's <see cref="IChannel"/> for native message production.
/// </summary>
/// <remarks>
/// <para>
/// Reads well-known property keys from <see cref="TransportMessage.Properties"/>:
/// </para>
/// <list type="bullet">
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.OrderingKey"/> maps to the routing key.</item>
/// <item><see cref="TransportTelemetryConstants.PropertyKeys.Priority"/> maps to <c>BasicProperties.Priority</c>.</item>
/// </list>
/// </remarks>
internal sealed partial class RabbitMqTransportSender : ITransportSender
{
	// CA2213: DI-injected channel is owned by the container.
	[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
		Justification = "RabbitMQ channel is injected via DI and owned by the container.")]
	private readonly IChannel _channel;

	private readonly string _exchange;
	private readonly string _defaultRoutingKey;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqTransportSender"/> class.
	/// </summary>
	/// <param name="channel">The RabbitMQ channel.</param>
	/// <param name="destination">The destination (queue or exchange name).</param>
	/// <param name="exchange">The exchange name to publish to.</param>
	/// <param name="defaultRoutingKey">The default routing key.</param>
	/// <param name="logger">The logger instance.</param>
	public RabbitMqTransportSender(
		IChannel channel,
		string destination,
		string exchange,
		string defaultRoutingKey,
		ILogger<RabbitMqTransportSender> logger)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		Destination = destination ?? throw new ArgumentNullException(nameof(destination));
		_exchange = exchange ?? string.Empty;
		_defaultRoutingKey = defaultRoutingKey ?? destination;
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
			var (basicProperties, body) = CreateRabbitMqMessage(message);
			var routingKey = ResolveRoutingKey(message);

			await _channel.BasicPublishAsync(
					_exchange,
					routingKey,
					mandatory: false,
					basicProperties: basicProperties,
					body: body,
					cancellationToken)
				.ConfigureAwait(false);

			LogMessageSent(message.Id, Destination);

			return SendResult.Success(message.Id);
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

		var stopwatch = Stopwatch.StartNew();
		var results = new List<SendResult>(messages.Count);

		foreach (var message in messages)
		{
			var result = await SendAsync(message, cancellationToken).ConfigureAwait(false);
			results.Add(result);
		}

		stopwatch.Stop();
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

	/// <inheritdoc />
	public Task FlushAsync(CancellationToken cancellationToken)
	{
		// RabbitMQ publish-confirms flush is handled via GetService → IChannel.WaitForConfirmsAsync().
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(IChannel))
		{
			return _channel;
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

	private static (RabbitMqBasicProperties Properties, ReadOnlyMemory<byte> Body) CreateRabbitMqMessage(TransportMessage message)
	{
		var properties = new RabbitMqBasicProperties
		{
			MessageId = message.Id,
			ContentType = message.ContentType ?? "application/octet-stream",
			CorrelationId = message.CorrelationId,
			Persistent = true,
			Headers = new Dictionary<string, object?>(),
		};

		if (message.TimeToLive.HasValue)
		{
			properties.Expiration = ((int)message.TimeToLive.Value.TotalMilliseconds).ToString();
		}

		if (message.MessageType is not null)
		{
			properties.Type = message.MessageType;
		}

		if (message.Subject is not null)
		{
			properties.Headers["subject"] = message.Subject;
		}

		// Map priority from properties
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.Priority, out var priorityObj))
		{
			properties.Priority = priorityObj switch
			{
				byte b => b,
				int i => (byte)Math.Clamp(i, 0, 9),
				string s when byte.TryParse(s, out var parsed) => parsed,
				_ => 0,
			};
		}

		// Copy custom properties as headers
		foreach (var (key, value) in message.Properties)
		{
			if (!key.StartsWith("dispatch.", StringComparison.Ordinal))
			{
				properties.Headers[key] = value?.ToString();
			}
		}

		return (properties, message.Body);
	}

	private string ResolveRoutingKey(TransportMessage message)
	{
		if (message.Properties.TryGetValue(TransportTelemetryConstants.PropertyKeys.OrderingKey, out var orderingKey) &&
			orderingKey is string orderingKeyStr)
		{
			return orderingKeyStr;
		}

		return _defaultRoutingKey;
	}

	[LoggerMessage(RabbitMqEventId.TransportSenderMessageSent, LogLevel.Debug,
		"RabbitMQ transport sender: message {MessageId} sent to {Destination}")]
	private partial void LogMessageSent(string messageId, string destination);

	[LoggerMessage(RabbitMqEventId.TransportSenderSendFailed, LogLevel.Error,
		"RabbitMQ transport sender: failed to send message {MessageId} to {Destination}")]
	private partial void LogSendFailed(string messageId, string destination, Exception exception);

	[LoggerMessage(RabbitMqEventId.TransportSenderBatchSent, LogLevel.Debug,
		"RabbitMQ transport sender: batch of {Count} messages sent to {Destination}, {SuccessCount} succeeded")]
	private partial void LogBatchSent(string destination, int count, int successCount);

	[LoggerMessage(RabbitMqEventId.TransportSenderDisposed, LogLevel.Debug,
		"RabbitMQ transport sender disposed for {Destination}")]
	private partial void LogDisposed(string destination);
}
