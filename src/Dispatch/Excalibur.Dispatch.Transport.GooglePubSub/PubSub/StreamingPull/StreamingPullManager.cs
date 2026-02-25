// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Manages multiple concurrent streaming pull connections for high-throughput message consumption.
/// </summary>
public sealed class StreamingPullManager : IAsyncDisposable
{
	private readonly ILogger<StreamingPullManager> _logger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly SubscriberServiceApiClient _subscriberClient;
	private readonly SubscriptionName _subscriptionName;
	private readonly StreamingPullOptions _options;
	private readonly StreamHealthMonitor _healthMonitor;
	private readonly MessageStreamProcessor _messageProcessor;
	private readonly ConcurrentDictionary<string, StreamingPullStream> _activeStreams;
	private readonly SemaphoreSlim _streamManagementSemaphore;
	private readonly ConcurrentBag<Task> _backgroundTasks;
	private readonly CancellationTokenSource _shutdownTokenSource;
	private readonly Task _managementTask;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamingPullManager" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="loggerFactory"> The logger factory for creating child loggers. </param>
	/// <param name="subscriberClient"> The Pub/Sub subscriber client. </param>
	/// <param name="subscriptionName"> The subscription name. </param>
	/// <param name="options"> The streaming pull options. </param>
	/// <param name="processor"> The message processor delegate. </param>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public StreamingPullManager(
		ILogger<StreamingPullManager> logger,
		ILoggerFactory loggerFactory,
		SubscriberServiceApiClient subscriberClient,
		SubscriptionName subscriptionName,
		StreamingPullOptions options,
		MessageStreamProcessor.MessageProcessor processor)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_subscriberClient = subscriberClient ?? throw new ArgumentNullException(nameof(subscriberClient));
		_subscriptionName = subscriptionName ?? throw new ArgumentNullException(nameof(subscriptionName));
		_options = options ?? throw new ArgumentNullException(nameof(options));

		_options.Validate();

		_activeStreams = new ConcurrentDictionary<string, StreamingPullStream>(StringComparer.Ordinal);
		_backgroundTasks = [];
		_streamManagementSemaphore = new SemaphoreSlim(1, 1);
		_shutdownTokenSource = new CancellationTokenSource();

		// Initialize health monitor
		_healthMonitor = new StreamHealthMonitor(
			_loggerFactory.CreateLogger<StreamHealthMonitor>(),
			_options);
		_healthMonitor.UnhealthyStreamDetected += OnUnhealthyStreamDetectedHandler;

		// Initialize message processor
		_messageProcessor = new MessageStreamProcessor(
			_loggerFactory.CreateLogger<MessageStreamProcessor>(),
			_options,
			processor);
		_messageProcessor.AckDeadlineExtensionRequested += OnAckDeadlineExtensionRequested;

		// Start management task
		_managementTask = ManageStreamsAsync(_shutdownTokenSource.Token);
	}

	/// <summary>
	/// Starts the streaming pull operations.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <exception cref="ObjectDisposedException"></exception>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(StreamingPullManager));
		}

		_logger.LogInformation(
			"Starting streaming pull manager with {StreamCount} concurrent streams",
			_options.ConcurrentStreams);

		// Create initial streams
		var tasks = new List<Task>();
		for (var i = 0; i < _options.ConcurrentStreams; i++)
		{
			tasks.Add(CreateStreamAsync(cancellationToken));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		_logger.LogInformation(
			"Streaming pull manager started with {ActiveStreams} active streams",
			_activeStreams.Count);
	}

	/// <summary>
	/// Gets statistics about the streaming pull manager.
	/// </summary>
	public StreamingPullStatistics GetStatistics()
	{
		var processorStats = _messageProcessor.GetStatistics();
		var healthInfos = _healthMonitor.GetAllHealthInfo();

		return new StreamingPullStatistics
		{
			ActiveStreamCount = _activeStreams.Count,
			TargetStreamCount = _options.ConcurrentStreams,
			TotalMessagesReceived = healthInfos.Sum(static h => h.MessagesReceived),
			TotalBytesReceived = healthInfos.Sum(static h => h.BytesReceived),
			TotalErrors = healthInfos.Sum(static h => h.ErrorCount),
			QueuedMessages = processorStats.QueuedMessages,
			ActiveProcessingThreads = processorStats.ActiveProcessingThreads,
			StreamHealthInfos = healthInfos,
		};
	}

	/// <summary>
	/// Disposes the streaming pull manager.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_logger.LogInformation("Shutting down streaming pull manager");

		// Unsubscribe event handlers BEFORE cancellation to prevent race (AD-541.6)
		_healthMonitor.UnhealthyStreamDetected -= OnUnhealthyStreamDetectedHandler;
		_messageProcessor.AckDeadlineExtensionRequested -= OnAckDeadlineExtensionRequested;

		// Signal shutdown
		await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);

		// Await tracked background tasks (AD-541.3)
		try
		{
			await Task.WhenAll(_backgroundTasks).ConfigureAwait(false);
		}
		catch (AggregateException ex)
		{
			_logger.LogWarning(ex, "Background tasks completed with errors during shutdown");
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}

		// Stop accepting new messages
		await _messageProcessor.DisposeAsync().ConfigureAwait(false);

		// Wait for management task
		try
		{
			await _managementTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Dispose all streams
		var disposeTasks = _activeStreams.Values.Select(static s => s.DisposeAsync().AsTask());
		await Task.WhenAll(disposeTasks).ConfigureAwait(false);

		// Cleanup
		_healthMonitor.Dispose();
		_streamManagementSemaphore.Dispose();
		_shutdownTokenSource.Dispose();

		_logger.LogInformation("Streaming pull manager shutdown complete");
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Creates a new streaming pull stream.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	private async Task<StreamingPullStream> CreateStreamAsync(CancellationToken cancellationToken)
	{
		var streamId = Guid.NewGuid().ToString("N");
		var stream = new StreamingPullStream(
			streamId,
			_loggerFactory.CreateLogger<StreamingPullStream>(),
			_subscriberClient,
			_subscriptionName,
			_options);

		try
		{
			await stream.StartAsync(cancellationToken).ConfigureAwait(false);

			if (_activeStreams.TryAdd(streamId, stream))
			{
				_healthMonitor.MarkConnected(streamId);

				// Start processing messages from this stream
				_ = ProcessStreamMessagesAsync(stream, cancellationToken);

				_logger.LogInformation("Created new stream {StreamId}", streamId);
				return stream;
			}

			await stream.DisposeAsync().ConfigureAwait(false);
			throw new InvalidOperationException($"Failed to add stream {streamId} to active streams");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create stream {StreamId}", streamId);
			await stream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	/// <summary>
	/// Processes messages from a specific stream.
	/// </summary>
	private async Task ProcessStreamMessagesAsync(StreamingPullStream stream, CancellationToken cancellationToken)
	{
		var streamId = stream.StreamId;

		try
		{
			await foreach (var message in stream.ReadMessagesAsync(cancellationToken).ConfigureAwait(false))
			{
				_healthMonitor.RecordMessageReceived(streamId, message.Message.Data?.Length ?? 0);

				var enqueued = await _messageProcessor.EnqueueMessageAsync(message, streamId, cancellationToken).ConfigureAwait(false);
				if (!enqueued)
				{
					// Processor is shutting down, NACK the message
					await stream.NackAsync(message.AckId, cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Expected during shutdown
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing messages from stream {StreamId}", streamId);
			_healthMonitor.RecordError(streamId, ex);
		}
		finally
		{
			_healthMonitor.MarkDisconnected(streamId);
			_ = _activeStreams.TryRemove(streamId, out _);
			await stream.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Manages stream lifecycle, creating new streams as needed.
	/// </summary>
	private async Task ManageStreamsAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);

				if (!await _streamManagementSemaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false))
				{
					continue;
				}

				try
				{
					var currentStreamCount = _activeStreams.Count;
					var targetStreamCount = _options.ConcurrentStreams;

					if (currentStreamCount < targetStreamCount)
					{
						_logger.LogInformation(
							"Stream count ({Current}) below target ({Target}), creating new streams",
							currentStreamCount, targetStreamCount);

						var streamsToCreate = targetStreamCount - currentStreamCount;
						var tasks = new List<Task>();

						for (var i = 0; i < streamsToCreate; i++)
						{
							tasks.Add(CreateStreamAsync(cancellationToken));
						}

						await Task.WhenAll(tasks).ConfigureAwait(false);
					}
				}
				finally
				{
					_ = _streamManagementSemaphore.Release();
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in stream management");
			}
		}
	}

	/// <summary>
	/// Handles unhealthy stream detection.
	/// </summary>
	private async void OnUnhealthyStreamDetectedHandler(object? sender, UnhealthyStreamEventArgs e)
	{
		if (_disposed || _shutdownTokenSource.IsCancellationRequested)
		{
			return;
		}

		var streamId = e.StreamId;
		try
		{
			_logger.LogWarning("Unhealthy stream detected: {StreamId}", streamId);

			if (_activeStreams.TryRemove(streamId, out var stream))
			{
				await stream.DisposeAsync().ConfigureAwait(false);
				_healthMonitor.RemoveStream(streamId);

				// Create replacement stream (tracked for disposal — AD-541.3)
				if (!_shutdownTokenSource.IsCancellationRequested)
				{
					_backgroundTasks.Add(CreateStreamAsync(_shutdownTokenSource.Token));
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error handling unhealthy stream {StreamId}", streamId);
		}
	}

	/// <summary>
	/// Handles acknowledgment deadline extension requests.
	/// </summary>
	private async void OnAckDeadlineExtensionRequested(object? sender, AckDeadlineExtensionEventArgs e)
	{
		if (_disposed || _shutdownTokenSource.IsCancellationRequested)
		{
			return;
		}

		try
		{
			// Find the stream that owns this message
			var stream = _activeStreams.Values.FirstOrDefault(s => s.HasMessage(e.AckId));
			if (stream != null)
			{
				await stream.ModifyAckDeadlineAsync(e.AckId, e.ExtensionSeconds, _shutdownTokenSource.Token).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error extending ack deadline for {AckId}", e.AckId);
		}
	}
}
