// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Implements optimized SQS message receiving with adaptive long polling.
/// </summary>
public sealed partial class SqsLongPollingReceiver : ILongPollingReceiver
{
	private readonly IAmazonSQS _sqsClient;
	private readonly ILongPollingStrategy _pollingStrategy;
	private readonly IPollingMetricsCollector _metricsCollector;
	private readonly LongPollingConfiguration _configuration;
	private readonly ILogger<SqsLongPollingReceiver> _logger;
	private readonly SemaphoreSlim _receiveLock;
	private readonly Dictionary<string, CancellationTokenSource> _activePolling;
#if NET9_0_OR_GREATER

	private readonly Lock _pollingLock = new();

#else

	private readonly object _pollingLock = new();

#endif

	private long _totalReceiveOperations;
	private long _totalMessagesReceived;
	private long _totalMessagesDeleted;
	private long _visibilityTimeoutOptimizations;
	private DateTimeOffset _lastReceiveTime;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqsLongPollingReceiver" /> class.
	/// </summary>
	public SqsLongPollingReceiver(
		IAmazonSQS sqsClient,
		ILongPollingStrategy pollingStrategy,
		IPollingMetricsCollector metricsCollector,
		LongPollingConfiguration configuration,
		ILogger<SqsLongPollingReceiver> logger)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		_pollingStrategy = pollingStrategy ?? throw new ArgumentNullException(nameof(pollingStrategy));
		_metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_configuration.Validate();
		_receiveLock = new SemaphoreSlim(1, 1);
		_activePolling = [];
		_lastReceiveTime = DateTimeOffset.UtcNow;
	}

	/// <inheritdoc />
	public PollingStatus Status { get; private set; } = PollingStatus.Inactive;

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<Message>> ReceiveMessagesAsync(
		string queueUrl,
		CancellationToken cancellationToken)
	{
		var options = new ReceiveOptions { MaxNumberOfMessages = _configuration.MaxMessagesPerReceive };

		return await ReceiveMessagesAsync(queueUrl, options, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<Message>> ReceiveMessagesAsync(
		string queueUrl,
		ReceiveOptions options,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(queueUrl))
		{
			throw new ArgumentException("Queue URL cannot be null or empty.", nameof(queueUrl));
		}

		ArgumentNullException.ThrowIfNull(options);

		var stopwatch = ValueStopwatch.StartNew();
		var waitTime = options.WaitTime ?? await _pollingStrategy.CalculateOptimalWaitTimeAsync().ConfigureAwait(false);

		try
		{
			var request = new ReceiveMessageRequest
			{
				QueueUrl = queueUrl,
				MaxNumberOfMessages = options.MaxNumberOfMessages ?? _configuration.MaxMessagesPerReceive,
				WaitTimeSeconds = (int)waitTime.TotalSeconds,
			};

			if (options.VisibilityTimeout.HasValue)
			{
				request.VisibilityTimeout = (int)options.VisibilityTimeout.Value.TotalSeconds;
			}

			if (options.MessageAttributeNames?.Count > 0)
			{
				request.MessageAttributeNames = [.. options.MessageAttributeNames];
			}

			if (options.AttributeNames?.Count > 0)
			{
				request.AttributeNames = [.. options.AttributeNames];
			}

			LogReceivingMessages(queueUrl, waitTime.TotalSeconds);

			var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken).ConfigureAwait(false);

			var messages = response.Messages ?? [];
			_ = Interlocked.Increment(ref _totalReceiveOperations);
			_ = Interlocked.Add(ref _totalMessagesReceived, messages.Count);
			_lastReceiveTime = DateTimeOffset.UtcNow;

			// Record metrics
			await _pollingStrategy.RecordReceiveResultAsync(messages.Count, waitTime).ConfigureAwait(false);

			if (messages.Count > 0)
			{
				_metricsCollector.RecordPollingAttempt(messages.Count, stopwatch.Elapsed);

				// Calculate and record message latency if possible
				foreach (var message in messages)
				{
					if (message.Attributes?.TryGetValue("SentTimestamp", out var sentTimestamp) == true &&
						long.TryParse(sentTimestamp, out var sentTimestampMs))
					{
						var sentTime = DateTimeOffset.FromUnixTimeMilliseconds(sentTimestampMs);
						var latency = DateTimeOffset.UtcNow - sentTime;
						_metricsCollector.RecordMetric("MessageLatency", latency.TotalMilliseconds, MetricUnit.Milliseconds);
					}
				}
			}
			else
			{
				_metricsCollector.RecordPollingAttempt(0, stopwatch.Elapsed);
			}

			// Calculate API calls saved
			if (waitTime > TimeSpan.FromSeconds(1) && messages.Count == 0)
			{
				var callsSaved = (int)(waitTime.TotalSeconds / 1) - 1;
				_metricsCollector.RecordMetric("ApiCallsSaved", callsSaved, MetricUnit.Count);
			}

			LogReceivedMessages(messages.Count, queueUrl, (long)stopwatch.ElapsedMilliseconds);

			return messages;
		}
		catch (Exception ex)
		{
			_metricsCollector.RecordPollingError(ex);
			LogReceiveError(queueUrl, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async ValueTask StartContinuousPollingAsync(
		string queueUrl,
		Func<Message, CancellationToken, ValueTask> messageHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messageHandler);

		// Create a batch handler that processes messages individually
		async ValueTask BatchHandler(IReadOnlyList<Message> messages, CancellationToken ct)
		{
			var tasks = messages.Select(async message =>
			{
				try
				{
					await messageHandler(message, ct).ConfigureAwait(false);
					await DeleteMessageAsync(queueUrl, message.ReceiptHandle, ct).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogMessageProcessingError(message.MessageId, ex);
				}
			});

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}

		await StartContinuousPollingAsync(queueUrl, BatchHandler, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask StartContinuousPollingAsync(
		string queueUrl,
		Func<IReadOnlyList<Message>, CancellationToken, ValueTask> batchHandler,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(queueUrl))
		{
			throw new ArgumentException("Queue URL cannot be null or empty.", nameof(queueUrl));
		}

		ArgumentNullException.ThrowIfNull(batchHandler);

		lock (_pollingLock)
		{
			if (_activePolling.ContainsKey(queueUrl))
			{
				throw new InvalidOperationException($"Continuous polling is already active for queue: {queueUrl}");
			}

			var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			_activePolling[queueUrl] = cts;
			Status = PollingStatus.Active;
		}

		LogPollingStarted(queueUrl);

		try
		{
			var linkedToken = _activePolling[queueUrl].Token;

			while (!linkedToken.IsCancellationRequested)
			{
				try
				{
					var messages = await ReceiveMessagesAsync(queueUrl, linkedToken).ConfigureAwait(false);

					if (messages.Count > 0)
					{
						await batchHandler(messages, linkedToken).ConfigureAwait(false);
					}
				}
				catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
				{
					break;
				}
				catch (Exception ex)
				{
					LogPollingError(queueUrl, ex);

					// Wait before retrying to avoid tight error loops
					await Task.Delay(TimeSpan.FromSeconds(5), linkedToken).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			lock (_pollingLock)
			{
				_ = _activePolling.Remove(queueUrl);
				if (_activePolling.Count == 0)
				{
					Status = PollingStatus.Inactive;
				}
			}

			LogPollingStopped(queueUrl);
		}
	}

	/// <inheritdoc />
	public async ValueTask OptimizeVisibilityTimeoutAsync(
		string queueUrl,
		string receiptHandle,
		TimeSpan estimatedProcessingTime,
		CancellationToken cancellationToken)
	{
		if (!_configuration.EnableVisibilityTimeoutOptimization)
		{
			return;
		}

		try
		{
			var optimizedTimeout = TimeSpan.FromMilliseconds(
				estimatedProcessingTime.TotalMilliseconds * _configuration.VisibilityTimeoutBufferFactor);

			// AWS SQS maximum visibility timeout is 12 hours
			if (optimizedTimeout > TimeSpan.FromHours(12))
			{
				optimizedTimeout = TimeSpan.FromHours(12);
			}

			var request = new ChangeMessageVisibilityRequest
			{
				QueueUrl = queueUrl,
				ReceiptHandle = receiptHandle,
				VisibilityTimeout = (int)optimizedTimeout.TotalSeconds,
			};

			_ = await _sqsClient.ChangeMessageVisibilityAsync(request, cancellationToken).ConfigureAwait(false);

			_ = Interlocked.Increment(ref _visibilityTimeoutOptimizations);

			_metricsCollector.RecordMetric("VisibilityTimeoutAdjustment", optimizedTimeout.TotalSeconds, MetricUnit.Seconds);

			LogVisibilityTimeoutOptimized(optimizedTimeout.TotalSeconds);
		}
		catch (Exception ex)
		{
			LogVisibilityTimeoutOptimizationFailed(ex);
		}
	}

	/// <inheritdoc />
	public async ValueTask DeleteMessageAsync(
		string queueUrl,
		string receiptHandle,
		CancellationToken cancellationToken)
	{
		try
		{
			var request = new DeleteMessageRequest { QueueUrl = queueUrl, ReceiptHandle = receiptHandle };

			_ = await _sqsClient.DeleteMessageAsync(request, cancellationToken).ConfigureAwait(false);
			_ = Interlocked.Increment(ref _totalMessagesDeleted);
		}
		catch (Exception ex)
		{
			LogDeleteMessageFailed(queueUrl, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async ValueTask DeleteMessagesAsync(
		string queueUrl,
		IEnumerable<string> receiptHandles,
		CancellationToken cancellationToken)
	{
		var handlesList = receiptHandles.ToList();
		if (handlesList.Count == 0)
		{
			return;
		}

		try
		{
			// AWS SQS DeleteMessageBatch supports up to 10 messages
			foreach (var batch in handlesList.Chunk(10))
			{
				var entries = batch.Select(static (handle, index) => new DeleteMessageBatchRequestEntry
				{
					Id = index.ToString(CultureInfo.InvariantCulture),
					ReceiptHandle = handle,
				}).ToList();

				var request = new DeleteMessageBatchRequest { QueueUrl = queueUrl, Entries = entries };

				var response = await _sqsClient.DeleteMessageBatchAsync(request, cancellationToken).ConfigureAwait(false);

				_ = Interlocked.Add(ref _totalMessagesDeleted, response.Successful.Count);

				if (response.Failed.Count != 0)
				{
					LogBatchDeleteFailed(response.Failed.Count, queueUrl);
				}
			}
		}
		catch (Exception ex)
		{
			LogBatchDeleteError(queueUrl, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public ValueTask<ReceiverStatistics> GetStatisticsAsync()
	{
		var stats = new ReceiverStatistics
		{
			TotalReceiveOperations = _totalReceiveOperations,
			TotalMessagesReceived = _totalMessagesReceived,
			TotalMessagesDeleted = _totalMessagesDeleted,
			VisibilityTimeoutOptimizations = _visibilityTimeoutOptimizations,
			LastReceiveTime = _lastReceiveTime,
			PollingStatus = Status,
		};

		return new ValueTask<ReceiverStatistics>(stats);
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		lock (_pollingLock)
		{
			if (Status == PollingStatus.Active)
			{
				return Task.CompletedTask;
			}

			Status = PollingStatus.Active;
			LogReceiverStarted();
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		List<CancellationTokenSource> sourcesToCancel;
		lock (_pollingLock)
		{
			if (Status != PollingStatus.Active)
			{
				return;
			}

			Status = PollingStatus.Stopping;
			sourcesToCancel = [.. _activePolling.Values];
		}

		foreach (var cts in sourcesToCancel)
		{
			await cts.CancelAsync().ConfigureAwait(false);
		}

		// Wait for all active polling operations to complete
		var timeout = TimeSpan.FromSeconds(30);
		var stopwatch = ValueStopwatch.StartNew();

		while (_activePolling.Count > 0 && stopwatch.Elapsed < timeout)
		{
			await Task.Delay(100, cancellationToken).ConfigureAwait(false);
		}

		lock (_pollingLock)
		{
			_activePolling.Clear();
			Status = PollingStatus.Inactive;
			LogReceiverStopped();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		lock (_pollingLock)
		{
			Status = PollingStatus.Stopping;

			foreach (var cts in _activePolling.Values)
			{
				cts.Cancel();
			}

			_activePolling.Clear();
		}

		_receiveLock?.Dispose();
	}

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.LongPollingReceivingMessages, LogLevel.Debug,
		"Receiving messages from {QueueUrl} with {WaitTime}s wait time")]
	private partial void LogReceivingMessages(string queueUrl, double waitTime);

	[LoggerMessage(AwsSqsEventId.LongPollingReceivedMessages, LogLevel.Debug,
		"Received {MessageCount} messages from {QueueUrl} in {ElapsedMs}ms")]
	private partial void LogReceivedMessages(int messageCount, string queueUrl, long elapsedMs);

	[LoggerMessage(AwsSqsEventId.LongPollingReceiveError, LogLevel.Error,
		"Error receiving messages from {QueueUrl}")]
	private partial void LogReceiveError(string queueUrl, Exception ex);

	[LoggerMessage(AwsSqsEventId.LongPollingMessageProcessingError, LogLevel.Error,
		"Error processing message {MessageId}")]
	private partial void LogMessageProcessingError(string messageId, Exception ex);

	[LoggerMessage(AwsSqsEventId.LongPollingPollingStarted, LogLevel.Information,
		"Starting continuous polling for queue {QueueUrl}")]
	private partial void LogPollingStarted(string queueUrl);

	[LoggerMessage(AwsSqsEventId.LongPollingPollingError, LogLevel.Error,
		"Error in continuous polling for queue {QueueUrl}")]
	private partial void LogPollingError(string queueUrl, Exception ex);

	[LoggerMessage(AwsSqsEventId.LongPollingPollingStopped, LogLevel.Information,
		"Stopped continuous polling for queue {QueueUrl}")]
	private partial void LogPollingStopped(string queueUrl);

	[LoggerMessage(AwsSqsEventId.LongPollingVisibilityTimeoutOptimized, LogLevel.Debug,
		"Optimized visibility timeout for message to {Timeout}s")]
	private partial void LogVisibilityTimeoutOptimized(double timeout);

	[LoggerMessage(AwsSqsEventId.LongPollingVisibilityTimeoutOptimizationFailed, LogLevel.Warning,
		"Failed to optimize visibility timeout for message")]
	private partial void LogVisibilityTimeoutOptimizationFailed(Exception ex);

	[LoggerMessage(AwsSqsEventId.LongPollingDeleteMessageFailed, LogLevel.Error,
		"Failed to delete message from queue {QueueUrl}")]
	private partial void LogDeleteMessageFailed(string queueUrl, Exception ex);

	[LoggerMessage(AwsSqsEventId.LongPollingBatchDeleteFailed, LogLevel.Warning,
		"Failed to delete {Count} messages from queue {QueueUrl}")]
	private partial void LogBatchDeleteFailed(int count, string queueUrl);

	[LoggerMessage(AwsSqsEventId.LongPollingBatchDeleteError, LogLevel.Error,
		"Failed to delete messages from queue {QueueUrl}")]
	private partial void LogBatchDeleteError(string queueUrl, Exception ex);

	[LoggerMessage(AwsSqsEventId.LongPollingReceiverStarted, LogLevel.Information,
		"Long polling receiver started")]
	private partial void LogReceiverStarted();

	[LoggerMessage(AwsSqsEventId.LongPollingReceiverStopped, LogLevel.Information,
		"Long polling receiver stopped")]
	private partial void LogReceiverStopped();
}
