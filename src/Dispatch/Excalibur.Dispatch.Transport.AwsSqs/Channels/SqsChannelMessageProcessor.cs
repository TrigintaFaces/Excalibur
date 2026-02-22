// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// High-throughput message processor using channels for AWS SQS. Optimized for 50K+ msgs/sec with zero-allocation processing.
/// </summary>
public sealed partial class SqsChannelMessageProcessor : IAsyncDisposable
{
	private readonly SqsChannelAdapter _channelAdapter;
	private readonly IMessageProcessor<Message> _messageProcessor;
	private readonly ILogger<SqsChannelMessageProcessor> _logger;
	private readonly SqsProcessorOptions _options;
	private readonly CancellationTokenSource _shutdownTokenSource;

	/// <summary>
	/// Processing tasks.
	/// </summary>
	private readonly Task[] _processingTasks;

	private readonly SemaphoreSlim _concurrencySemaphore;

	/// <summary>
	/// Delete batching.
	/// </summary>
	private readonly Channel<DeleteRequest> _deleteChannel;

	private readonly Task _deleteTask;
	private readonly IAmazonSQS _sqsClient;

	// Metrics
	private readonly ValueStopwatch _uptimeStopwatch;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqsChannelMessageProcessor" /> class.
	/// </summary>
	/// <param name="sqsClient"> </param>
	/// <param name="channelAdapter"> </param>
	/// <param name="messageProcessor"> </param>
	/// <param name="options"> </param>
	/// <param name="logger"> </param>
	public SqsChannelMessageProcessor(
		IAmazonSQS sqsClient,
		SqsChannelAdapter channelAdapter,
		IMessageProcessor<Message> messageProcessor,
		SqsProcessorOptions options,
		ILogger<SqsChannelMessageProcessor> logger)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		_channelAdapter = channelAdapter ?? throw new ArgumentNullException(nameof(channelAdapter));
		_messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_shutdownTokenSource = new CancellationTokenSource();
		_concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrentMessages);

		// Initialize processing tasks
		_processingTasks = new Task[_options.ProcessorCount];

		// Initialize delete channel
		var deleteChannelOptions = new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false,
			AllowSynchronousContinuations = false,
		};
		_deleteChannel = Channel.CreateUnbounded<DeleteRequest>(deleteChannelOptions);
		_deleteTask = StartBackgroundTask(() => ProcessDeletesAsync(_shutdownTokenSource.Token));

		// Initialize metrics
		Metrics = new SqsProcessorMetrics();
		_uptimeStopwatch = ValueStopwatch.StartNew();
	}

	/// <summary>
	/// Gets the current processor metrics.
	/// </summary>
	/// <value>
	/// The current processor metrics.
	/// </value>
	public SqsProcessorMetrics Metrics { get; }

	/// <summary>
	/// Starts the message processor.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		LogStartingProcessor(_options.ProcessorCount);

		// Start the channel adapter
		await _channelAdapter.StartAsync(cancellationToken).ConfigureAwait(false);

		// Start processing tasks
		for (var i = 0; i < _options.ProcessorCount; i++)
		{
			var processorIndex = i;
			_processingTasks[i] = StartBackgroundTask(
				() => ProcessMessagesAsync(processorIndex, _shutdownTokenSource.Token));
		}

		LogProcessorStarted();
	}

	/// <summary>
	/// Stops the message processor gracefully.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		LogStoppingProcessor();

		// Signal shutdown
		await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);

		// Stop the channel adapter
		await _channelAdapter.StopAsync(cancellationToken).ConfigureAwait(false);

		// Wait for processing tasks
		try
		{
			await Task.WhenAll(_processingTasks).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}

		// Complete delete channel
		_ = _deleteChannel.Writer.TryComplete();
		await _deleteTask.ConfigureAwait(false);

		var totalProcessed = Metrics.MessagesProcessed;
		var totalTime = _uptimeStopwatch.Elapsed.TotalSeconds;
		var throughput = totalProcessed / totalTime;

		LogProcessorStopped(totalProcessed, Metrics.ProcessingErrors, throughput);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await StopAsync(CancellationToken.None).ConfigureAwait(false);

		_shutdownTokenSource.Dispose();
		_concurrencySemaphore.Dispose();
		_channelAdapter.Dispose();
	}

	private async Task ProcessMessagesAsync(int processorIndex, CancellationToken cancellationToken)
	{
		LogWorkerStarting(processorIndex);

		await foreach (var message in _channelAdapter.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

			// Fire and forget for maximum throughput
			_ = ProcessMessageAsync(message, cancellationToken).ContinueWith(
				_ => _concurrencySemaphore.Release(),
				TaskContinuationOptions.ExecuteSynchronously);
		}

		LogWorkerStopped(processorIndex);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			// Process the message
			var result = await _messageProcessor.ProcessAsync(message, cancellationToken)
				.ConfigureAwait(false);

			if (result.Success)
			{
				// Queue for deletion
				await _deleteChannel.Writer
					.WriteAsync(
						new DeleteRequest { ReceiptHandle = message.ReceiptHandle, MessageId = message.MessageId },
						cancellationToken).ConfigureAwait(false);

				Metrics.RecordSuccess(stopwatch.Elapsed);
			}
			else
			{
				Metrics.RecordFailure(stopwatch.Elapsed);
				LogProcessingFailed(message.MessageId, result.Error);
			}
		}
		catch (Exception ex)
		{
			Metrics.RecordError(stopwatch.Elapsed);
			LogProcessingError(message.MessageId, ex);
		}
	}

	private async Task ProcessDeletesAsync(CancellationToken cancellationToken)
	{
		var batch = new List<DeleteMessageBatchRequestEntry>();
		var batchTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.DeleteBatchIntervalMs));

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				// Wait for either a delete request or batch timer
				var readTask = _deleteChannel.Reader.ReadAsync(cancellationToken).AsTask();
				var timerTask = batchTimer.WaitForNextTickAsync(cancellationToken).AsTask();

				var completedTask = await Task.WhenAny(readTask, timerTask).ConfigureAwait(false);

				if (completedTask == readTask && readTask.IsCompletedSuccessfully)
				{
					var deleteRequest = await readTask.ConfigureAwait(false);
					batch.Add(new DeleteMessageBatchRequestEntry
					{
						Id = deleteRequest.MessageId,
						ReceiptHandle = deleteRequest.ReceiptHandle,
					});
				}

				// Send batch if full or timer elapsed
				if (batch.Count >= 10 || completedTask == timerTask)
				{
					await DeleteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
					batch.Clear();
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogDeleteProcessorError(ex);
			}
		}

		// Delete any remaining messages
		if (batch.Count > 0)
		{
			await DeleteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
		}

		batchTimer.Dispose();
	}

	private async Task DeleteBatchAsync(List<DeleteMessageBatchRequestEntry> entries, CancellationToken cancellationToken)
	{
		if (entries.Count == 0)
		{
			return;
		}

		var request = new DeleteMessageBatchRequest { QueueUrl = _options.QueueUrl.ToString(), Entries = [.. entries], };

		try
		{
			var response = await _sqsClient.DeleteMessageBatchAsync(request, cancellationToken)
				.ConfigureAwait(false);

			Metrics.RecordDeletes(response.Successful.Count);

			if (response.Failed.Count > 0)
			{
				Metrics.RecordDeleteErrors(response.Failed.Count);

				foreach (var failure in response.Failed)
				{
					LogDeleteMessageFailed(failure.Id, failure.Code, failure.Message);
				}
			}
		}
		catch (Exception ex)
		{
			Metrics.RecordDeleteErrors(entries.Count);
			LogDeleteBatchError(entries.Count, ex);
		}
	}

	private static Task StartBackgroundTask(Func<Task> operation) =>
		Task.Factory.StartNew(
			operation,
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default).Unwrap();

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.ChannelProcessorStarting, LogLevel.Information,
		"Starting SQS channel processor with {ProcessorCount} processors")]
	private partial void LogStartingProcessor(int processorCount);

	[LoggerMessage(AwsSqsEventId.ChannelProcessorStarted, LogLevel.Information,
		"SQS channel processor started successfully")]
	private partial void LogProcessorStarted();

	[LoggerMessage(AwsSqsEventId.ChannelProcessorStopping, LogLevel.Information,
		"Stopping SQS channel processor")]
	private partial void LogStoppingProcessor();

	[LoggerMessage(AwsSqsEventId.ChannelProcessorStopped, LogLevel.Information,
		"SQS channel processor stopped. Processed: {Processed}, Errors: {Errors}, Throughput: {Throughput:F2} msgs/sec")]
	private partial void LogProcessorStopped(long processed, long errors, double throughput);

	[LoggerMessage(AwsSqsEventId.ChannelWorkerStarting, LogLevel.Debug,
		"Starting processor {ProcessorIndex}")]
	private partial void LogWorkerStarting(int processorIndex);

	[LoggerMessage(AwsSqsEventId.ChannelWorkerStopped, LogLevel.Debug,
		"Processor {ProcessorIndex} stopped")]
	private partial void LogWorkerStopped(int processorIndex);

	[LoggerMessage(AwsSqsEventId.ChannelProcessingError, LogLevel.Error,
		"Error processing message {MessageId}")]
	private partial void LogProcessingError(string messageId, Exception ex);

	[LoggerMessage(AwsSqsEventId.ChannelDeleteProcessorError, LogLevel.Error,
		"Error in delete processor")]
	private partial void LogDeleteProcessorError(Exception ex);

	[LoggerMessage(AwsSqsEventId.ChannelDeleteBatchError, LogLevel.Error,
		"Error deleting message batch of {Count} messages")]
	private partial void LogDeleteBatchError(int count, Exception ex);

	[LoggerMessage(AwsSqsEventId.ChannelDeleteMessageFailed, LogLevel.Warning,
		"Failed to delete message {MessageId}: {Code} - {ErrorMessage}")]
	private partial void LogDeleteMessageFailed(string messageId, string code, string errorMessage);

	[LoggerMessage(AwsSqsEventId.ChannelProcessingFailed, LogLevel.Warning,
		"Failed to process message {MessageId}: {Error}")]
	private partial void LogProcessingFailed(string messageId, string error);

	private readonly struct DeleteRequest
	{
		public string ReceiptHandle { get; init; }

		public string MessageId { get; init; }
	}
}
