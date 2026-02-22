// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// In-memory transport adapter for testing and development scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This adapter provides a simple in-memory message transport that can be used for unit testing, local development, and scenarios where
/// external message brokers are not available.
/// </para>
/// <para> Messages are processed asynchronously through a bounded channel with configurable capacity. </para>
/// <para> Implements <see cref="ITransportHealthChecker" /> for integration with ASP.NET Core health checks and the <see cref="MultiTransportHealthCheck" />. </para>
/// </remarks>
public sealed partial class InMemoryTransportAdapter : ITransportAdapter, ITransportHealthChecker, IAsyncDisposable
{
	/// <summary>
	/// The default transport name for in-memory adapters.
	/// </summary>
	public const string DefaultName = "InMemory";

	/// <summary>
	/// The transport type identifier.
	/// </summary>
	public const string TransportTypeName = "inmemory";

	private readonly ILogger<InMemoryTransportAdapter> _logger;
	private readonly InMemoryTransportOptions _options;
	private readonly Channel<PendingMessage> _messageChannel;
	private readonly ChannelWriter<PendingMessage> _writer;
	private readonly ChannelReader<PendingMessage> _reader;
	private readonly ConcurrentDictionary<string, IDispatchMessage> _sentMessages = new(StringComparer.Ordinal);

	private CancellationTokenSource? _processingCts;
	private Task? _processingTask;
	private volatile bool _disposed;

	// Health check metrics tracking
	private long _totalMessages;

	private long _successfulMessages;
	private long _failedMessages;
	private DateTimeOffset _lastHealthCheck = DateTimeOffset.UtcNow;
	private TransportHealthStatus _lastStatus = TransportHealthStatus.Healthy;

	private readonly record struct PendingMessage(object TransportMessage, IDispatcher Dispatcher);

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryTransportAdapter" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="options"> The adapter options. </param>
	public InMemoryTransportAdapter(
		ILogger<InMemoryTransportAdapter> logger,
		InMemoryTransportOptions? options = null)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? new InMemoryTransportOptions();

		var channelOptions = new BoundedChannelOptions(_options.ChannelCapacity)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true,
			SingleWriter = false,
		};

		_messageChannel = Channel.CreateBounded<PendingMessage>(channelOptions);
		_writer = _messageChannel.Writer;
		_reader = _messageChannel.Reader;
	}

	/// <inheritdoc />
	public string Name => _options.Name ?? DefaultName;

	/// <inheritdoc />
	public string TransportType => TransportTypeName;

	/// <inheritdoc />
	public bool IsRunning { get; private set; }

	/// <summary>
	/// Gets the collection of messages that have been sent through this adapter.
	/// </summary>
	/// <value> A read-only dictionary of sent messages indexed by message ID. </value>
	/// <remarks> This property is useful for testing scenarios where you need to verify that messages were sent correctly. </remarks>
	public IReadOnlyDictionary<string, IDispatchMessage> SentMessages => _sentMessages;

	/// <inheritdoc />
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
			return Messaging.MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:not-running",
				Title = "Transport Not Running",
				ErrorCode = 10001,
				Status = 503,
				Detail = "The in-memory transport adapter is not running",
				Instance = $"inmemory-adapter-{Guid.NewGuid()}",
			});
		}

		if (transportMessage is not IDispatchMessage message)
		{
			TransportMeter.RecordError(Name, TransportType, "invalid_message_type");
			_ = Interlocked.Increment(ref _failedMessages);
			return Messaging.MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:invalid-message-type",
				Title = "Invalid Message Type",
				ErrorCode = 10002,
				Status = 400,
				Detail = $"Expected IDispatchMessage but received {transportMessage.GetType().Name}",
				Instance = $"inmemory-adapter-{Guid.NewGuid()}",
			});
		}

		var messageId = Guid.NewGuid().ToString();
		var messageType = message.GetType().Name;
		LogReceivingMessage(messageId, messageType);

		_ = Interlocked.Increment(ref _totalMessages);

		try
		{
			var context = new MessageContext(message, dispatcher is IServiceProvider sp ? sp : new EmptyServiceProvider())
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

			return Messaging.MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:transport:processing-failed",
				Title = "Message Processing Failed",
				ErrorCode = 10003,
				Status = 500,
				Detail = ex.Message,
				Instance = $"message-{messageId}",
			});
		}
	}

	/// <inheritdoc />
	public Task SendAsync(
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
			throw new InvalidOperationException(
				Resources.InMemoryTransportAdapter_NotRunning);
		}

		var messageId = Guid.NewGuid().ToString();
		var messageType = message.GetType().Name;
		LogSendingMessage(messageId, destination);

		// Store the message for testing verification
		_sentMessages[messageId] = message;

		TransportMeter.RecordMessageSent(Name, TransportType, messageType);
		TransportMeter.RecordSendDuration(Name, TransportType, stopwatch.Elapsed.TotalMilliseconds);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (IsRunning)
		{
			return;
		}

		LogStarting();
		IsRunning = true;

		_processingCts?.Dispose();
		_processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_processingTask = ProcessMessagesAsync(_processingCts.Token);

		// Record transport start metrics
		TransportMeter.RecordTransportStarted(Name, TransportType);
		TransportMeter.UpdateTransportState(Name, TransportType, isConnected: true);

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (!IsRunning)
		{
			return;
		}

		LogStopping();
		IsRunning = false;

		// Record transport stop metrics
		TransportMeter.RecordTransportStopped(Name, TransportType);
		TransportMeter.UpdateTransportState(Name, TransportType, isConnected: false);

		await (_processingCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
		_ = _writer.TryComplete();

		if (_processingTask is not null)
		{
			try
			{
				await _processingTask.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected during cancellation
			}
		}

		_processingTask = null;
		_processingCts?.Dispose();
		_processingCts = null;
	}

	/// <summary>
	/// Clears all sent messages from the internal collection.
	/// </summary>
	/// <remarks> This method is useful for resetting state between tests. </remarks>
	public void ClearSentMessages() => _sentMessages.Clear();

	#region ITransportHealthChecker Implementation

	/// <inheritdoc />
	string ITransportHealthChecker.Name => Name;

	/// <inheritdoc />
	string ITransportHealthChecker.TransportType => TransportType;

	/// <inheritdoc />
	TransportHealthCheckCategory ITransportHealthChecker.Categories =>
		TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Resources;

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckHealthAsync(
		TransportHealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();

		var data = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["ChannelCapacity"] = _options.ChannelCapacity,
			["TotalMessages"] = _totalMessages,
			["SuccessfulMessages"] = _successfulMessages,
			["FailedMessages"] = _failedMessages,
		};

		TransportHealthCheckResult result;

		if (!IsRunning)
		{
			result = TransportHealthCheckResult.Unhealthy(
				"In-memory transport adapter is not running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else if (_failedMessages > 0 && _failedMessages > _successfulMessages / 10)
		{
			// More than 10% failures - degraded
			result = TransportHealthCheckResult.Degraded(
				$"In-memory transport has elevated failure rate: {_failedMessages}/{_totalMessages}",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}
		else
		{
			result = TransportHealthCheckResult.Healthy(
				"In-memory transport adapter is healthy and running",
				context.RequestedCategories,
				stopwatch.Elapsed,
				data);
		}

		_lastHealthCheck = DateTimeOffset.UtcNow;
		_lastStatus = result.Status;

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckQuickHealthAsync(CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();

		var status = IsRunning
			? TransportHealthStatus.Healthy
			: TransportHealthStatus.Unhealthy;

		var description = IsRunning
			? "In-memory transport adapter is running"
			: "In-memory transport adapter is not running";

		var result = new TransportHealthCheckResult(
			status,
			description,
			TransportHealthCheckCategory.Connectivity,
			stopwatch.Elapsed);

		_lastHealthCheck = DateTimeOffset.UtcNow;
		_lastStatus = status;

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<TransportHealthMetrics> GetHealthMetricsAsync(CancellationToken cancellationToken)
	{
		var successRate = _totalMessages > 0
			? (double)_successfulMessages / _totalMessages
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
				["TotalMessages"] = _totalMessages,
				["SuccessfulMessages"] = _successfulMessages,
				["FailedMessages"] = _failedMessages,
				["ChannelCapacity"] = _options.ChannelCapacity,
			});

		return Task.FromResult(metrics);
	}

	#endregion ITransportHealthChecker Implementation

	/// <inheritdoc />
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

		// Clean up metrics tracking
		TransportMeter.RemoveTransport(Name);

		GC.SuppressFinalize(this);
	}

	private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var pending in _reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				_ = await ReceiveAsync(pending.TransportMessage, pending.Dispatcher, cancellationToken)
					.ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Expected during shutdown
		}
		catch (ChannelClosedException)
		{
			// Expected when channel is completed
		}
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.InMemoryTransportStarted, LogLevel.Information,
		"Starting in-memory transport adapter")]
	private partial void LogStarting();

	[LoggerMessage(DeliveryEventId.InMemoryTransportStopping, LogLevel.Information,
		"Stopping in-memory transport adapter")]
	private partial void LogStopping();

	[LoggerMessage(DeliveryEventId.InMemoryMessageReceived, LogLevel.Debug,
		"Receiving message {MessageId} of type {MessageType}")]
	private partial void LogReceivingMessage(string messageId, string messageType);

	[LoggerMessage(DeliveryEventId.InMemoryMessagePublished, LogLevel.Debug,
		"Sending message {MessageId} to destination {Destination}")]
	private partial void LogSendingMessage(string messageId, string destination);

	[LoggerMessage(DeliveryEventId.InMemoryProcessingFailed, LogLevel.Error,
		"Failed to process message {MessageId}")]
	private partial void LogMessageProcessingFailed(string messageId, Exception ex);

	/// <summary>
	/// Minimal service provider implementation for when no service provider is available.
	/// </summary>
	private sealed class EmptyServiceProvider : IServiceProvider
	{
		/// <inheritdoc />
		public object? GetService(Type serviceType) => null;
	}
}

/// <summary>
/// Configuration options for the in-memory transport adapter.
/// </summary>
public sealed class InMemoryTransportOptions
{
	/// <summary>
	/// Gets or sets the name of this transport adapter instance.
	/// </summary>
	/// <value> The transport name. Default is "InMemory". </value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the capacity of the message channel.
	/// </summary>
	/// <value> The channel capacity. Default is 1000. </value>
	/// <remarks>
	/// <para> When the channel reaches capacity, message producers will wait until space becomes available. </para>
	/// </remarks>
	public int ChannelCapacity { get; set; } = 1000;
}
