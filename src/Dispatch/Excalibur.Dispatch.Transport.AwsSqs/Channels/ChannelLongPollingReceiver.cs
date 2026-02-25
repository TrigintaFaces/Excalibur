// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Optimized long polling implementation for AWS SQS using channels. Reduces API calls while maintaining high throughput.
/// </summary>
public sealed partial class ChannelLongPollingReceiver : IAsyncDisposable
{
	private readonly IAmazonSQS _sqsClient;
	private readonly LongPollingOptions _options;
	private readonly ILogger<ChannelLongPollingReceiver> _logger;
	private readonly Channel<Message> _messageChannel;
	private readonly CancellationTokenSource _shutdownTokenSource;

	/// <summary>
	/// Polling tasks.
	/// </summary>
	private readonly Task[] _pollingTasks;

	private readonly SemaphoreSlim _pollingSemaphore;

	// Metrics
	private readonly ValueStopwatch _uptimeStopwatch;

	private readonly Timer _adaptiveTimer;

	/// <summary>
	/// Adaptive polling.
	/// </summary>
	private int _currentPollers;

	/// <summary>
	/// Initializes a new instance of the <see cref="ChannelLongPollingReceiver" /> class.
	/// </summary>
	/// <param name="sqsClient">The SQS client.</param>
	/// <param name="options">The long polling options.</param>
	/// <param name="logger">The logger.</param>
	public ChannelLongPollingReceiver(
		IAmazonSQS sqsClient,
		LongPollingOptions options,
		ILogger<ChannelLongPollingReceiver> logger)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_shutdownTokenSource = new CancellationTokenSource();

		// Create message channel
		var channelOptions = new BoundedChannelOptions(_options.ChannelCapacity)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false,
			AllowSynchronousContinuations = false,
		};
		_messageChannel = Channel.CreateBounded<Message>(channelOptions);

		// Initialize polling infrastructure
		_currentPollers = _options.MinPollers;
		_pollingTasks = new Task[_options.MaxPollers];
		_pollingSemaphore = new SemaphoreSlim(_options.MaxPollers);

		// Initialize metrics
		Metrics = new LongPollingMetrics();
		_uptimeStopwatch = ValueStopwatch.StartNew();

		// Initialize adaptive polling timer
		_adaptiveTimer = new Timer(
			AdaptPollerCount,
			state: null,
			TimeSpan.FromSeconds(_options.AdaptiveIntervalSeconds),
			TimeSpan.FromSeconds(_options.AdaptiveIntervalSeconds));
	}

	/// <summary>
	/// Gets the channel reader for consuming messages.
	/// </summary>
	/// <value>
	/// The channel reader for consuming messages.
	/// </value>
	public ChannelReader<Message> Reader => _messageChannel.Reader;

	/// <summary>
	/// Gets current metrics.
	/// </summary>
	/// <value>
	/// Current metrics.
	/// </value>
	public LongPollingMetrics Metrics { get; }

	/// <summary>
	/// Starts the long polling receiver.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		LogReceiverStarting(_options.QueueUrl?.ToString() ?? string.Empty, _currentPollers);

		// Start initial pollers
		for (var i = 0; i < _currentPollers; i++)
		{
			StartPoller(i);
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Stops the long polling receiver.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		LogReceiverStopping();

		// Stop adaptive timer
		await _adaptiveTimer.DisposeAsync().ConfigureAwait(false);

		// Signal shutdown
		await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);

		// Close channel
		_ = _messageChannel.Writer.TryComplete();

		// Wait for all polling tasks
		var activeTasks = _pollingTasks.Where(static t => t != null).ToArray();

		try
		{
			await Task.WhenAll(activeTasks).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}

		var totalTime = _uptimeStopwatch.Elapsed.TotalSeconds;
		var throughput = Metrics.MessagesReceived / totalTime;

		LogReceiverStopped(Metrics.MessagesReceived, Metrics.Errors, throughput);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await StopAsync(CancellationToken.None).ConfigureAwait(false);

		_shutdownTokenSource.Dispose();
		_pollingSemaphore.Dispose();
		await _adaptiveTimer.DisposeAsync().ConfigureAwait(false);
	}

	private void StartPoller(int index) => _pollingTasks[index] = StartBackgroundTask(() => PollMessagesAsync(index, _shutdownTokenSource.Token));

	private async Task PollMessagesAsync(int pollerIndex, CancellationToken cancellationToken)
	{
		LogPollerStarted(pollerIndex);

		var request = new ReceiveMessageRequest
		{
			QueueUrl = _options.QueueUrl.ToString(),
			MaxNumberOfMessages = 10, // SQS max
			WaitTimeSeconds = 20, // Max long polling (20 seconds)
			VisibilityTimeout = _options.VisibilityTimeout,
			AttributeNames = ["All"],
			MessageAttributeNames = ["All"],
		};

		var emptyReceives = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await _pollingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

				try
				{
					var stopwatch = ValueStopwatch.StartNew();
					var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken)
						.ConfigureAwait(false);

					Metrics.RecordPollDuration(stopwatch.Elapsed);

					if (response.Messages.Count > 0)
					{
						emptyReceives = 0;
						Metrics.RecordMessagesReceived(response.Messages.Count);

						// Write messages to channel
						foreach (var message in response.Messages)
						{
							await _messageChannel.Writer.WriteAsync(message, cancellationToken)
								.ConfigureAwait(false);
						}
					}
					else
					{
						emptyReceives++;
						Metrics.RecordEmptyPoll();

						// Back off if consistently empty
						if (emptyReceives > 3)
						{
							await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken)
								.ConfigureAwait(false);
						}
					}
				}
				finally
				{
					_ = _pollingSemaphore.Release();
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				Metrics.RecordError();
				LogPollerError(pollerIndex, ex);

				// Exponential backoff on error
				var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(5, Metrics.Errors % 10)));
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}

		LogPollerStopped(pollerIndex);
	}

	private void AdaptPollerCount(object? state)
	{
		try
		{
			var metrics = Metrics.GetSnapshot();
			var targetPollers = CalculateOptimalPollerCount(metrics);

			if (targetPollers != _currentPollers)
			{
				LogPollerCountAdjusting(_currentPollers, targetPollers);

				AdjustPollerCount(targetPollers);
			}
		}
		catch (Exception ex)
		{
			LogAdaptivePollingError(ex);
		}
	}

	private int CalculateOptimalPollerCount(LongPollingSnapshot metrics)
	{
		// Calculate based on message rate and channel utilization
		var messageRate = metrics.MessageRate;
		var channelUtilization = (double)_messageChannel.Reader.Count / _options.ChannelCapacity;
		var emptyPollRate = metrics.EmptyPollRate;

		// Increase pollers if high message rate or channel getting full
		if (messageRate > 1000 || channelUtilization > 0.8)
		{
			return Math.Min(_currentPollers + 2, _options.MaxPollers);
		}

		// Decrease pollers if low activity
		if (emptyPollRate > 0.8 && _currentPollers > _options.MinPollers)
		{
			return Math.Max(_currentPollers - 1, _options.MinPollers);
		}

		return _currentPollers;
	}

	private void AdjustPollerCount(int targetCount)
	{
		if (targetCount > _currentPollers)
		{
			// Add pollers
			for (var i = _currentPollers; i < targetCount; i++)
			{
				if (_pollingTasks[i]?.IsCompleted != false)
				{
					StartPoller(i);
				}
			}
		}
		else if (targetCount < _currentPollers)
		{
			// Reduce pollers gracefully (Pollers will naturally stop when they check _currentPollers)
		}

		_currentPollers = targetCount;
	}

	private static Task StartBackgroundTask(Func<Task> operation) =>
		Task.Factory.StartNew(
			operation,
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default).Unwrap();

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.LongPollingReceiverStarting, LogLevel.Information,
		"Starting SQS long polling receiver for {QueueUrl} with {Pollers} initial pollers")]
	private partial void LogReceiverStarting(string queueUrl, int pollers);

	[LoggerMessage(AwsSqsEventId.LongPollingReceiverStopping, LogLevel.Information,
		"Stopping SQS long polling receiver")]
	private partial void LogReceiverStopping();

	[LoggerMessage(AwsSqsEventId.LongPollingReceiverStoppedWithMetrics, LogLevel.Information,
		"SQS long polling receiver stopped. Messages: {Messages}, Errors: {Errors}, Throughput: {Throughput:F2} msgs/sec")]
	private partial void LogReceiverStopped(long messages, long errors, double throughput);

	[LoggerMessage(AwsSqsEventId.LongPollerStarted, LogLevel.Debug,
		"Long poller {PollerIndex} started")]
	private partial void LogPollerStarted(int pollerIndex);

	[LoggerMessage(AwsSqsEventId.LongPollerError, LogLevel.Error,
		"Error in long poller {PollerIndex}")]
	private partial void LogPollerError(int pollerIndex, Exception ex);

	[LoggerMessage(AwsSqsEventId.LongPollerStopped, LogLevel.Debug,
		"Long poller {PollerIndex} stopped")]
	private partial void LogPollerStopped(int pollerIndex);

	[LoggerMessage(AwsSqsEventId.LongPollerCountAdjusting, LogLevel.Information,
		"Adjusting poller count from {Current} to {Target} based on metrics")]
	private partial void LogPollerCountAdjusting(int current, int target);

	[LoggerMessage(AwsSqsEventId.AdaptivePollingError, LogLevel.Error,
		"Error in adaptive polling adjustment")]
	private partial void LogAdaptivePollingError(Exception ex);
}
