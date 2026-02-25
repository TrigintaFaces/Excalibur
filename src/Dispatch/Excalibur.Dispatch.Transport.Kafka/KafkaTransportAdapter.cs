// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Transport;

using Microsoft.Extensions.Logging;

using MessageContext = Excalibur.Dispatch.Messaging.MessageContext;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka transport adapter that wraps the existing KafkaMessageBus infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This adapter provides integration with the unified transport configuration system
/// while delegating actual Kafka operations to the existing <see cref="KafkaMessageBus"/>.
/// </para>
/// <para>
/// For publishing messages, the adapter uses KafkaMessageBus.
/// For receiving messages, use the existing KafkaChannelConsumer infrastructure.
/// </para>
/// <para>
/// Implements <see cref="ITransportHealthChecker"/> for integration with
/// ASP.NET Core health checks and the <c>MultiTransportHealthCheck</c>.
/// </para>
/// </remarks>
public sealed partial class KafkaTransportAdapter : ITransportAdapter, ITransportHealthChecker, IAsyncDisposable
{
	/// <summary>
	/// The default transport name for Kafka adapters.
	/// </summary>
	public const string DefaultName = "Kafka";

	/// <summary>
	/// The transport type identifier.
	/// </summary>
	public const string TransportTypeName = "kafka";

	private readonly ILogger<KafkaTransportAdapter> _logger;
	private readonly KafkaMessageBus _messageBus;
	private readonly KafkaTransportAdapterOptions _options;
	private readonly IServiceProvider _serviceProvider;
	private volatile bool _disposed;

	// Health check and metrics tracking
	private long _totalMessages;
	private long _successfulMessages;
	private long _failedMessages;
	private DateTimeOffset _lastHealthCheck = DateTimeOffset.UtcNow;
	private TransportHealthStatus _lastStatus = TransportHealthStatus.Healthy;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaTransportAdapter"/> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="messageBus"> The Kafka message bus to wrap. </param>
	/// <param name="serviceProvider"> The service provider for resolving dependencies. </param>
	/// <param name="options"> The adapter options. </param>
	public KafkaTransportAdapter(
		ILogger<KafkaTransportAdapter> logger,
		KafkaMessageBus messageBus,
		IServiceProvider serviceProvider,
		KafkaTransportAdapterOptions? options = null)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_options = options ?? new KafkaTransportAdapterOptions();
	}

	/// <inheritdoc/>
	public string Name => _options.Name ?? DefaultName;

	/// <inheritdoc/>
	public string TransportType => TransportTypeName;

	/// <inheritdoc/>
	public bool IsRunning { get; private set; }

	/// <inheritdoc/>
	public async Task<IMessageResult> ReceiveAsync(
		object transportMessage,
		IDispatcher dispatcher,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);
		ArgumentNullException.ThrowIfNull(dispatcher);

		var stopwatch = ValueStopwatch.StartNew();

		if (!IsRunning)
		{
			TransportMeter.RecordError(Name, TransportType, "not_running");
			_ = Interlocked.Increment(ref _failedMessages);
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:not-running",
				Title = "Transport Not Running",
				ErrorCode = 10001,
				Detail = "The Kafka transport adapter is not running",
				Instance = $"kafka-adapter-{Guid.NewGuid()}",
			});
		}

		if (transportMessage is not IDispatchMessage message)
		{
			TransportMeter.RecordError(Name, TransportType, "invalid_message_type");
			_ = Interlocked.Increment(ref _failedMessages);
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:invalid-message-type",
				Title = "Invalid Message Type",
				ErrorCode = 10002,
				Detail = $"Expected IDispatchMessage but received {transportMessage.GetType().Name}",
				Instance = $"kafka-adapter-{Guid.NewGuid()}",
			});
		}

		var messageId = Guid.NewGuid().ToString();
		var messageType = message.GetType().Name;
		LogReceivingMessage(messageId, messageType);

		_ = Interlocked.Increment(ref _totalMessages);

		try
		{
			var context = new MessageContext(message, _serviceProvider)
			{
				MessageId = messageId,
				MessageType = message.GetType().FullName,
				ReceivedTimestampUtc = DateTimeOffset.UtcNow,
			};

			var result = await dispatcher.DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);

			TransportMeter.RecordMessageReceived(Name, TransportType, messageType);
			TransportMeter.RecordReceiveDuration(Name, TransportType, stopwatch.Elapsed.TotalMilliseconds);
			_ = Interlocked.Increment(ref _successfulMessages);

			return result;
		}
		catch (Exception ex)
		{
			LogMessageProcessingFailed(messageId, ex);
			TransportMeter.RecordError(Name, TransportType, "processing_failed");
			TransportMeter.RecordReceiveDuration(Name, TransportType, stopwatch.Elapsed.TotalMilliseconds);
			_ = Interlocked.Increment(ref _failedMessages);

			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:processing-failed",
				Title = "Message Processing Failed",
				ErrorCode = 10003,
				Detail = ex.Message,
				Instance = $"message-{messageId}",
			});
		}
	}

	/// <inheritdoc/>
	public async Task SendAsync(
		IDispatchMessage message,
		string destination,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(destination);

		var stopwatch = ValueStopwatch.StartNew();

		if (!IsRunning)
		{
			TransportMeter.RecordError(Name, TransportType, "not_running");
			throw new InvalidOperationException("The Kafka transport adapter is not running");
		}

		var messageId = Guid.NewGuid().ToString();
		var messageType = message.GetType().Name;
		LogSendingMessage(messageId, destination);

		try
		{
			// Create a basic message context for the underlying message bus
			var context = new MessageContext(message, _serviceProvider)
			{
				MessageId = messageId,
				CorrelationId = messageId,
			};

			// Route to appropriate KafkaMessageBus.PublishAsync overload based on message type
			switch (message)
			{
				case IDispatchAction action:
					await _messageBus.PublishAsync(action, context, cancellationToken).ConfigureAwait(false);
					break;

				case IDispatchEvent evt:
					await _messageBus.PublishAsync(evt, context, cancellationToken).ConfigureAwait(false);
					break;

				case IDispatchDocument doc:
					await _messageBus.PublishAsync(doc, context, cancellationToken).ConfigureAwait(false);
					break;

				default:
					throw new ArgumentException(
						$"Unsupported message type: {message.GetType().Name}. " +
						"Message must implement IDispatchAction, IDispatchEvent, or IDispatchDocument.",
						nameof(message));
			}

			TransportMeter.RecordMessageSent(Name, TransportType, messageType);
			TransportMeter.RecordSendDuration(Name, TransportType, stopwatch.Elapsed.TotalMilliseconds);
		}
		catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException)
		{
			LogSendFailed(messageId, ex);
			TransportMeter.RecordError(Name, TransportType, "send_failed");
			TransportMeter.RecordSendDuration(Name, TransportType, stopwatch.Elapsed.TotalMilliseconds);
			throw new InvalidOperationException($"Failed to send message to Kafka: {ex.Message}", ex);
		}
	}

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (IsRunning)
		{
			return Task.CompletedTask;
		}

		LogStarting();
		IsRunning = true;

		TransportMeter.RecordTransportStarted(Name, TransportType);
		TransportMeter.UpdateTransportState(Name, TransportType, isConnected: true, pendingMessages: 0);
		_lastStatus = TransportHealthStatus.Healthy;

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (!IsRunning)
		{
			return;
		}

		LogStopping();
		IsRunning = false;

		TransportMeter.RecordTransportStopped(Name, TransportType);
		TransportMeter.UpdateTransportState(Name, TransportType, isConnected: false, pendingMessages: 0);
		_lastStatus = TransportHealthStatus.Unhealthy;

		await Task.CompletedTask.ConfigureAwait(false);
	}

	#region ITransportHealthChecker Implementation

	/// <inheritdoc/>
	TransportHealthCheckCategory ITransportHealthChecker.Categories =>
		TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Resources;

	/// <inheritdoc/>
	public Task<TransportHealthCheckResult> CheckHealthAsync(
		TransportHealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();

		var total = Interlocked.Read(ref _totalMessages);
		var successful = Interlocked.Read(ref _successfulMessages);
		var failed = Interlocked.Read(ref _failedMessages);

		var data = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["TotalMessages"] = total,
			["SuccessfulMessages"] = successful,
			["FailedMessages"] = failed,
		};

		TransportHealthCheckResult result;

		if (!IsRunning)
		{
			result = TransportHealthCheckResult.Unhealthy(
				"Kafka transport adapter is not running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else if (failed > 0 && failed > successful / 10)
		{
			// More than 10% failures - degraded
			result = TransportHealthCheckResult.Degraded(
				$"Kafka transport has elevated failure rate: {failed}/{total}",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else
		{
			result = TransportHealthCheckResult.Healthy(
				"Kafka transport adapter is healthy and running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}

		_lastHealthCheck = DateTimeOffset.UtcNow;
		_lastStatus = result.Status;

		return Task.FromResult(result);
	}

	/// <inheritdoc/>
	public Task<TransportHealthCheckResult> CheckQuickHealthAsync(CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();

		var status = IsRunning
			? TransportHealthStatus.Healthy
			: TransportHealthStatus.Unhealthy;

		var description = IsRunning
			? "Kafka transport adapter is running"
			: "Kafka transport adapter is not running";

		var result = new TransportHealthCheckResult(
			status,
			description,
			TransportHealthCheckCategory.Connectivity,
			stopwatch.Elapsed);

		_lastHealthCheck = DateTimeOffset.UtcNow;
		_lastStatus = status;

		return Task.FromResult(result);
	}

	/// <inheritdoc/>
	public Task<TransportHealthMetrics> GetHealthMetricsAsync(CancellationToken cancellationToken)
	{
		var total = Interlocked.Read(ref _totalMessages);
		var successful = Interlocked.Read(ref _successfulMessages);
		var failed = Interlocked.Read(ref _failedMessages);

		var successRate = total > 0
			? (double)successful / total
			: 1.0;

		var metrics = new TransportHealthMetrics(
			lastCheckTimestamp: _lastHealthCheck,
			lastStatus: _lastStatus,
			consecutiveFailures: IsRunning ? 0 : 1,
			totalChecks: 1,
			successRate: successRate,
			averageCheckDuration: TimeSpan.FromMilliseconds(1),
			customMetrics: new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["TotalMessages"] = total,
				["SuccessfulMessages"] = successful,
				["FailedMessages"] = failed,
			});

		return Task.FromResult(metrics);
	}

	#endregion

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await StopAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during cancellation
		}
		catch (ObjectDisposedException)
		{
			// Expected if resources already disposed
		}

		TransportMeter.RemoveTransport(Name);

		// Note: We don't dispose _messageBus here as it's injected and managed by DI
		GC.SuppressFinalize(this);
	}

	// Source-generated logging methods
	[LoggerMessage(KafkaEventId.TransportAdapterStarting, LogLevel.Information,
		"Starting Kafka transport adapter")]
	private partial void LogStarting();

	[LoggerMessage(KafkaEventId.TransportAdapterStopping, LogLevel.Information,
		"Stopping Kafka transport adapter")]
	private partial void LogStopping();

	[LoggerMessage(KafkaEventId.ReceivingMessage, LogLevel.Debug,
		"Receiving message {MessageId} of type {MessageType}")]
	private partial void LogReceivingMessage(string messageId, string messageType);

	[LoggerMessage(KafkaEventId.SendingMessage, LogLevel.Debug,
		"Sending message {MessageId} to destination {Destination}")]
	private partial void LogSendingMessage(string messageId, string destination);

	[LoggerMessage(KafkaEventId.MessageProcessingFailed, LogLevel.Error,
		"Failed to process message {MessageId}")]
	private partial void LogMessageProcessingFailed(string messageId, Exception ex);

	[LoggerMessage(KafkaEventId.SendFailed, LogLevel.Error,
		"Failed to send message {MessageId}")]
	private partial void LogSendFailed(string messageId, Exception ex);
}

/// <summary>
/// Configuration options for the Kafka transport adapter.
/// </summary>
public sealed class KafkaTransportAdapterOptions
{
	/// <summary>
	/// Gets or sets the name of this transport adapter instance.
	/// </summary>
	/// <value> The transport name. Default is "Kafka". </value>
	public string? Name { get; set; }
}
