// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// High-performance channel-based SQS message processor optimized for 50K+ msgs/sec throughput.
/// </summary>
public sealed partial class HighThroughputSqsChannelProcessor : IAsyncDisposable
{
	private readonly IAmazonSQS _sqsClient;
	private readonly HighThroughputSqsOptions _options;
	private readonly ILogger<HighThroughputSqsChannelProcessor> _logger;
	private readonly IServiceProvider _serviceProvider;
	private readonly Channel<SqsMessageEnvelope> _channel;
	private ConcurrentBag<Task> _pollingTasks;
	private readonly CancellationTokenSource _shutdownTokenSource;
	private readonly SemaphoreSlim _concurrencyLimit;

	// Performance tracking
	private readonly ValueStopwatch _uptimeStopwatch;

	/// <summary>
	/// Object pools for zero-allocation.
	/// </summary>
	private readonly SimpleObjectPool<ReceiveMessageRequest> _receiveRequestPool;

	private readonly SimpleObjectPool<DeleteMessageBatchRequest> _deleteRequestPool;
	private readonly ArrayPool<byte> _bufferPool;

	/// <summary>
	/// Batch delete infrastructure.
	/// </summary>
	private readonly ConcurrentQueue<DeleteMessageBatchRequestEntry> _pendingDeletes;

	private readonly Timer _batchDeleteTimer;

	/// <summary>
	/// Tracks in-flight receipt handles for visibility timeout extension.
	/// Key: entry ID (MessageId), Value: receipt handle.
	/// </summary>
	private readonly ConcurrentDictionary<string, string> _inFlightReceipts;

	private readonly Timer _visibilityExtensionTimer;

	/// <summary>
	/// Initializes a new instance of the <see cref="HighThroughputSqsChannelProcessor" /> class.
	/// </summary>
	/// <param name="sqsClient"> </param>
	/// <param name="options"> </param>
	/// <param name="logger"> </param>
	/// <param name="serviceProvider"> </param>
	public HighThroughputSqsChannelProcessor(
		IAmazonSQS sqsClient,
		HighThroughputSqsOptions options,
		ILogger<HighThroughputSqsChannelProcessor> logger,
		IServiceProvider serviceProvider)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

		// Create channel with configured options
		if (_options.ChannelCapacity > 0)
		{
			var boundedOptions = new BoundedChannelOptions(_options.ChannelCapacity)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = false,
				SingleWriter = false,
			};
			_channel = Channel.CreateBounded<SqsMessageEnvelope>(boundedOptions);
		}
		else
		{
			var unboundedOptions = new UnboundedChannelOptions { SingleReader = false, SingleWriter = false };
			_channel = Channel.CreateUnbounded<SqsMessageEnvelope>(unboundedOptions);
		}

		_pollingTasks = [];
		_shutdownTokenSource = new CancellationTokenSource();
		_concurrencyLimit = new SemaphoreSlim(_options.Polling.MaxConcurrentPollers);

		// Initialize metrics
		Metrics = new SqsChannelMetrics();
		_uptimeStopwatch = ValueStopwatch.StartNew();

		// Initialize object pools
		_receiveRequestPool = new SimpleObjectPool<ReceiveMessageRequest>(
			() => new ReceiveMessageRequest
			{
				QueueUrl = _options.QueueUrl.ToString(),
				MaxNumberOfMessages = 10, // SQS max
				WaitTimeSeconds = 20, // Max long polling
				VisibilityTimeout = _options.VisibilityTimeout,
				AttributeNames = ["All"],
				MessageAttributeNames = ["All"],
			},
			request =>
			{
				// Reset is not needed as values are immutable
			});

		_deleteRequestPool = new SimpleObjectPool<DeleteMessageBatchRequest>(
			() => new DeleteMessageBatchRequest { QueueUrl = _options.QueueUrl.ToString() },
			request => request.Entries.Clear());

		_bufferPool = ArrayPool<byte>.Shared;

		// Initialize batch delete
		_pendingDeletes = new ConcurrentQueue<DeleteMessageBatchRequestEntry>();
		_batchDeleteTimer = new Timer(_ => _ = ProcessBatchDeletesAsync(), state: null, Timeout.Infinite, Timeout.Infinite);

		// Initialize visibility timeout extension
		_inFlightReceipts = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
		_visibilityExtensionTimer = new Timer(_ => _ = ExtendVisibilityTimeoutsAsync(), state: null, Timeout.Infinite, Timeout.Infinite);
	}

	/// <summary>
	/// Gets the channel reader for consuming messages.
	/// </summary>
	/// <value>
	/// The channel reader for consuming messages.
	/// </value>
	public ChannelReader<SqsMessageEnvelope> Reader => _channel.Reader;

	/// <summary>
	/// Gets current performance metrics.
	/// </summary>
	/// <value>
	/// Current performance metrics.
	/// </value>
	public SqsChannelMetrics Metrics { get; }

	/// <summary>
	/// Starts the high-throughput message processing.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		LogProcessorStarting(_options.QueueUrl.ToString(), _options.Polling.ConcurrentPollers);

		// Start multiple polling tasks for increased throughput
		for (var i = 0; i < _options.Polling.ConcurrentPollers; i++)
		{
			var pollerIndex = i;
			var pollingTask = StartBackgroundTask(
				() => PollMessagesAsync(pollerIndex, _shutdownTokenSource.Token));

			_pollingTasks.Add(pollingTask);
		}

		// Atomic drain: swap the bag to avoid ToArray()+Clear() race
		var drained = Interlocked.Exchange(ref _pollingTasks, new ConcurrentBag<Task>());
		foreach (var t in drained)
		{
			if (!t.IsCompleted)
			{
				_pollingTasks.Add(t);
			}
		}

		// Start batch delete timer
		_ = _batchDeleteTimer.Change(
			TimeSpan.FromMilliseconds(_options.BatchDeleteIntervalMs),
			TimeSpan.FromMilliseconds(_options.BatchDeleteIntervalMs));

		// Start visibility timeout extension timer at 75% of the visibility timeout interval
		var extensionIntervalMs = (int)(_options.VisibilityTimeout * 1000 * 0.75);
		if (extensionIntervalMs > 0)
		{
			_ = _visibilityExtensionTimer.Change(
				TimeSpan.FromMilliseconds(extensionIntervalMs),
				TimeSpan.FromMilliseconds(extensionIntervalMs));
		}

		return Task.CompletedTask;
	}

	private static Task StartBackgroundTask(Func<Task> operation) =>
		Task.Factory.StartNew(
			operation,
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default).Unwrap();

	/// <summary>
	/// Disposes the processor asynchronously.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);
		_ = _channel.Writer.TryComplete();

		var finalTasks = Interlocked.Exchange(ref _pollingTasks, new ConcurrentBag<Task>());
		await Task.WhenAll(finalTasks).ConfigureAwait(false);

		await _visibilityExtensionTimer.DisposeAsync().ConfigureAwait(false);
		await _batchDeleteTimer.DisposeAsync().ConfigureAwait(false);
		_shutdownTokenSource.Dispose();
		_concurrencyLimit.Dispose();

		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Polls messages from SQS continuously.
	/// </summary>
	private async Task PollMessagesAsync(int pollerIndex, CancellationToken cancellationToken)
	{
		LogPollerStarted(pollerIndex);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				var request = _receiveRequestPool.Get();
				try
				{
					var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken).ConfigureAwait(false);

					if (response.Messages.Count > 0)
					{
						Metrics.AddTotalMessagesProcessed(response.Messages.Count);

						foreach (var message in response.Messages)
						{
							// Create a basic message object for the context
							var messageBody = Encoding.UTF8.GetBytes(message.Body ?? string.Empty);
							// R0.8: Dispose objects before losing scope - memoryMessage ownership is transferred to MessageContext
#pragma warning disable CA2000
							var memoryMessage = new MemoryMessage(messageBody.AsMemory(), "application/json");
#pragma warning restore CA2000

							// Create message context with proper metadata
							var messageContext = new MessageContext(memoryMessage, _serviceProvider)
							{
								MessageId = message.MessageId,
							};
							messageContext.SetReceivedTimestampUtc(DateTimeOffset.UtcNow);

							// Add SQS-specific attributes to the context
							if (message.Attributes != null)
							{
								foreach (var attr in message.Attributes)
								{
									messageContext.Items[$"SQS.{attr.Key}"] = attr.Value;
								}
							}

							// Set ApproximateReceiveCount if available
							if (message.Attributes?.TryGetValue("ApproximateReceiveCount", out var receiveCount) == true &&
								int.TryParse(receiveCount, out var count))
							{
								messageContext.GetOrCreateProcessingFeature().DeliveryCount = count;
							}

							// Create the envelope using the constructor
							// R0.8: Dispose objects before losing scope - envelope ownership is transferred to channel
#pragma warning disable CA2000
							var envelope = new SqsMessageEnvelope(
								message,
								dispatchMessage: null, // No dispatch message for raw SQS processing
								messageContext,
								pollerIndex);
#pragma warning restore CA2000

							// Track in-flight receipt handle for visibility extension
							if (message.ReceiptHandle is not null)
							{
								_inFlightReceipts[message.MessageId] = message.ReceiptHandle;
							}

							await _channel.Writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
						}
					}
				}
				finally
				{
					_receiveRequestPool.Return(request);
				}
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogPollerError(pollerIndex, ex);
				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
			}
		}

		LogPollerStopped(pollerIndex);
	}

	/// <summary>
	/// Processes batch deletes on a timer.
	/// </summary>
	private async Task ProcessBatchDeletesAsync()
	{
		try
		{
			if (_pendingDeletes.IsEmpty)
			{
				return;
			}

			var entries = new List<DeleteMessageBatchRequestEntry>();

			while (entries.Count < 10 && _pendingDeletes.TryDequeue(out var entry))
			{
				entries.Add(entry);
			}

			if (entries.Count > 0)
			{
				var request = _deleteRequestPool.Get();
				try
				{
					request.Entries = entries;
					var response = await _sqsClient.DeleteMessageBatchAsync(request).ConfigureAwait(false);

					// Count only actually successful deletes and remove from in-flight tracking
					Metrics.AddSuccessfulMessages(response.Successful.Count);
					foreach (var successful in response.Successful)
					{
						_inFlightReceipts.TryRemove(successful.Id, out _);
					}

					// Re-queue failed entries so they are retried on the next timer tick
					if (response.Failed.Count > 0)
					{
						Metrics.AddFailedMessages(response.Failed.Count);
						foreach (var failed in response.Failed)
						{
							LogBatchDeletePartialFailure(failed.Id, failed.Code, failed.Message);

							// Find the original entry and re-queue it for retry
							var original = entries.Find(e => e.Id == failed.Id);
							if (original is not null)
							{
								_pendingDeletes.Enqueue(original);
							}
						}
					}
				}
				finally
				{
					_deleteRequestPool.Return(request);
				}
			}
		}
		catch (Exception ex)
		{
			LogBatchDeleteError(ex);
		}
	}

	/// <summary>
	/// Extends visibility timeout for all in-flight messages to prevent duplicate delivery.
	/// </summary>
	private async Task ExtendVisibilityTimeoutsAsync()
	{
		try
		{
			if (_inFlightReceipts.IsEmpty)
			{
				return;
			}

			// Snapshot current in-flight receipts (SQS max batch = 10)
			var entries = new List<ChangeMessageVisibilityBatchRequestEntry>();
			foreach (var kvp in _inFlightReceipts)
			{
				entries.Add(new ChangeMessageVisibilityBatchRequestEntry
				{
					Id = kvp.Key,
					ReceiptHandle = kvp.Value,
					VisibilityTimeout = _options.VisibilityTimeout,
				});

				if (entries.Count >= 10)
				{
					await SendVisibilityExtensionBatchAsync(entries).ConfigureAwait(false);
					entries = [];
				}
			}

			if (entries.Count > 0)
			{
				await SendVisibilityExtensionBatchAsync(entries).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			LogVisibilityExtensionError(ex);
		}
	}

	private async Task SendVisibilityExtensionBatchAsync(List<ChangeMessageVisibilityBatchRequestEntry> entries)
	{
		try
		{
			var request = new ChangeMessageVisibilityBatchRequest
			{
				QueueUrl = _options.QueueUrl!.ToString(),
				Entries = entries,
			};

			var response = await _sqsClient.ChangeMessageVisibilityBatchAsync(request).ConfigureAwait(false);

			if (response.Failed.Count > 0)
			{
				foreach (var failed in response.Failed)
				{
					LogVisibilityExtensionFailed(failed.Id, failed.Code, failed.Message);
					// Remove stale receipt handles that can no longer be extended
					_inFlightReceipts.TryRemove(failed.Id, out _);
				}
			}
		}
		catch (Exception ex)
		{
			// Log but don't crash -- worst case is duplicate delivery, which consumers handle via idempotency
			LogVisibilityExtensionError(ex);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.HighThroughputProcessorStarting, LogLevel.Information,
		"Starting high-throughput SQS processor for queue {QueueUrl} with {PollerCount} concurrent pollers")]
	private partial void LogProcessorStarting(string queueUrl, int pollerCount);

	[LoggerMessage(AwsSqsEventId.HighThroughputPollerStarted, LogLevel.Debug,
		"Poller {PollerIndex} started")]
	private partial void LogPollerStarted(int pollerIndex);

	[LoggerMessage(AwsSqsEventId.HighThroughputPollerError, LogLevel.Error,
		"Error in poller {PollerIndex}")]
	private partial void LogPollerError(int pollerIndex, Exception ex);

	[LoggerMessage(AwsSqsEventId.HighThroughputPollerStopped, LogLevel.Debug,
		"Poller {PollerIndex} stopped")]
	private partial void LogPollerStopped(int pollerIndex);

	[LoggerMessage(AwsSqsEventId.HighThroughputBatchDeleteError, LogLevel.Error,
		"Error processing batch deletes")]
	private partial void LogBatchDeleteError(Exception ex);

	[LoggerMessage(AwsSqsEventId.HighThroughputBatchDeletePartialFailure, LogLevel.Warning,
		"Batch delete partial failure for entry {EntryId}: {ErrorCode} - {ErrorMessage}")]
	private partial void LogBatchDeletePartialFailure(string entryId, string errorCode, string errorMessage);

	[LoggerMessage(AwsSqsEventId.HighThroughputVisibilityExtensionFailed, LogLevel.Warning,
		"Visibility extension failed for entry {EntryId}: {ErrorCode} - {ErrorMessage}")]
	private partial void LogVisibilityExtensionFailed(string entryId, string errorCode, string errorMessage);

	[LoggerMessage(AwsSqsEventId.HighThroughputVisibilityExtensionError, LogLevel.Warning,
		"Error extending visibility timeouts for in-flight messages")]
	private partial void LogVisibilityExtensionError(Exception ex);
}
