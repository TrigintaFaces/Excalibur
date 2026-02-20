// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

using Amazon.SQS;
using Amazon.SQS.Model;

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
	private readonly ConcurrentBag<Task> _pollingTasks;
	private readonly CancellationTokenSource _shutdownTokenSource;
	private readonly SemaphoreSlim _concurrencyLimit;

	// Performance tracking
	private readonly Stopwatch _uptimeStopwatch;

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
		_concurrencyLimit = new SemaphoreSlim(_options.MaxConcurrentPollers);

		// Initialize metrics
		Metrics = new SqsChannelMetrics();
		_uptimeStopwatch = Stopwatch.StartNew();

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
		_batchDeleteTimer = new Timer(ProcessBatchDeletes, state: null, Timeout.Infinite, Timeout.Infinite);
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
		LogProcessorStarting(_options.QueueUrl.ToString(), _options.ConcurrentPollers);

		// Start multiple polling tasks for increased throughput
		for (var i = 0; i < _options.ConcurrentPollers; i++)
		{
			var pollerIndex = i;
			var pollingTask = Task.Run(
				() => PollMessagesAsync(pollerIndex, _shutdownTokenSource.Token),
				cancellationToken);

			_pollingTasks.Add(pollingTask);
		}

		// Start batch delete timer
		_ = _batchDeleteTimer.Change(
			TimeSpan.FromMilliseconds(_options.BatchDeleteIntervalMs),
			TimeSpan.FromMilliseconds(_options.BatchDeleteIntervalMs));

		return Task.CompletedTask;
	}

	/// <summary>
	/// Disposes the processor asynchronously.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);
		_ = _channel.Writer.TryComplete();

		await Task.WhenAll(_pollingTasks).ConfigureAwait(false);

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
						Metrics.TotalMessagesProcessed += response.Messages.Count;

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
								ReceivedTimestampUtc = DateTimeOffset.UtcNow,
							};

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
								messageContext.DeliveryCount = count;
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

							await _channel.Writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
						}
					}
				}
				finally
				{
					_receiveRequestPool.Return(request);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
	private async void ProcessBatchDeletes(object? state)
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
			try
			{
				var request = _deleteRequestPool.Get();
				try
				{
					request.Entries = entries;
					_ = await _sqsClient.DeleteMessageBatchAsync(request).ConfigureAwait(false);
					Metrics.SuccessfulMessages += entries.Count;
				}
				finally
				{
					_deleteRequestPool.Return(request);
				}
			}
			catch (Exception ex)
			{
				LogBatchDeleteError(ex);
				Metrics.FailedMessages += entries.Count;
			}
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
}
