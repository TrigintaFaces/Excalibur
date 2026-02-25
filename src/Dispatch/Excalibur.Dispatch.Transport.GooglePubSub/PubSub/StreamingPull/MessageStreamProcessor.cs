// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Threading.Channels;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Processes messages from streaming pull connections efficiently.
/// </summary>
public sealed class MessageStreamProcessor : IAsyncDisposable
{
	private readonly ILogger<MessageStreamProcessor> _logger;
	private readonly StreamingPullOptions _options;
	private readonly Channel<ReceivedMessage> _messageChannel;
	private readonly SemaphoreSlim _processingSemaphore;
	private readonly ArrayPool<byte> _bufferPool;
	private readonly CancellationTokenSource _shutdownTokenSource;
	private readonly Task _processingTask;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageStreamProcessor" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="options"> The streaming pull options. </param>
	/// <param name="messageProcessor"> The message processor delegate. </param>
	public MessageStreamProcessor(
		ILogger<MessageStreamProcessor> logger,
		StreamingPullOptions options,
		MessageProcessor messageProcessor)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		ProcessMessageAsync = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));

		_bufferPool = ArrayPool<byte>.Shared;
		_shutdownTokenSource = new CancellationTokenSource();

		// Create channel for message processing
		var channelOptions = new BoundedChannelOptions(_options.MaxOutstandingMessagesPerStream * _options.ConcurrentStreams)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleWriter = false,
			SingleReader = false,
		};
		_messageChannel = Channel.CreateBounded<ReceivedMessage>(channelOptions);

		// Semaphore to control concurrent processing
		_processingSemaphore = new SemaphoreSlim(
			_options.MaxOutstandingMessagesPerStream,
			_options.MaxOutstandingMessagesPerStream);

		// Start processing task
		_processingTask = ProcessMessagesAsync(_shutdownTokenSource.Token);
	}

	/// <summary>
	/// Delegate for processing received messages.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public delegate Task<bool> MessageProcessor(ReceivedMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Event raised when a message is enqueued.
	/// </summary>
	public event EventHandler<MessageEnqueuedEventArgs>? MessageEnqueued;

	/// <summary>
	/// Event raised when a message is processed.
	/// </summary>
	public event EventHandler<MessageProcessedEventArgs>? MessageProcessed;

	/// <summary>
	/// Event raised when an acknowledgment deadline extension is requested.
	/// </summary>
	public event EventHandler<AckDeadlineExtensionEventArgs>? AckDeadlineExtensionRequested;

	/// <summary>
	/// Gets or sets the message processor delegate.
	/// </summary>
	/// <value>
	/// The message processor delegate.
	/// </value>
	public MessageProcessor ProcessMessageAsync { get; set; }

	/// <summary>
	/// Enqueues a message for processing.
	/// </summary>
	/// <param name="message"> The message to process. </param>
	/// <param name="streamId"> The ID of the stream that received the message. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the message was enqueued; false if the processor is shutting down. </returns>
	public async Task<bool> EnqueueMessageAsync(
		ReceivedMessage message,
		string streamId,
		CancellationToken cancellationToken)
	{
		if (_disposed || _shutdownTokenSource.IsCancellationRequested)
		{
			return false;
		}

		try
		{
			await _messageChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
			MessageEnqueued?.Invoke(this, new MessageEnqueuedEventArgs(streamId, message.Message.MessageId));
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to enqueue message {MessageId} from stream {StreamId}",
				message.Message.MessageId, streamId);
			return false;
		}
	}

	/// <summary>
	/// Gets statistics about the processor.
	/// </summary>
	public ProcessorStatistics GetStatistics() =>
		new()
		{
			QueuedMessages = _messageChannel.Reader.Count,
			MaxQueueCapacity = _options.MaxOutstandingMessagesPerStream * _options.ConcurrentStreams,
			ActiveProcessingThreads = _options.MaxOutstandingMessagesPerStream - _processingSemaphore.CurrentCount,
			IsShuttingDown = _shutdownTokenSource.IsCancellationRequested,
		};

	/// <summary>
	/// Disposes the processor asynchronously.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Signal shutdown
		await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);
		_ = _messageChannel.Writer.TryComplete();

		// Wait for processing to complete
		try
		{
			await _processingTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Cleanup
		_shutdownTokenSource.Dispose();
		_processingSemaphore.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Processes messages from the channel.
	/// </summary>
	private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
	{
		var tasks = new List<Task>();
		await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			await _processingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

			var task = ProcessSingleMessageAsync(message, cancellationToken);
			tasks.Add(task);

			// Clean up completed tasks periodically
			if (tasks.Count > 100)
			{
				_ = tasks.RemoveAll(static t => t.IsCompleted);
			}
		}

		// Wait for all remaining tasks
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Processes a single message.
	/// </summary>
	private async Task ProcessSingleMessageAsync(ReceivedMessage message, CancellationToken cancellationToken)
	{
		var startTime = DateTimeOffset.UtcNow;
		var messageId = message.Message.MessageId;
		var ackId = message.AckId;
		byte[]? buffer = null;

		try
		{
			// Handle acknowledgment deadline extension if needed
			Task? ackDeadlineTask = null;
			if (_options.AutoExtendAckDeadline)
			{
				ackDeadlineTask = ExtendAckDeadlineAsync(ackId, cancellationToken);
			}

			// Rent buffer for message processing if needed
			var dataSize = message.Message.Data?.Length ?? 0;
			if (dataSize > 0 && message.Message.Data != null)
			{
				buffer = _bufferPool.Rent(dataSize);
				message.Message.Data.CopyTo(buffer, 0);
			}

			// Process the message
			var shouldAck = await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);

			// Cancel deadline extension if still running
			if (ackDeadlineTask is { IsCompleted: false })
			{
				await ackDeadlineTask.ConfigureAwait(false);
			}

			// Handle acknowledgment
			if (shouldAck)
			{
				MessageProcessed?.Invoke(this, new MessageProcessedEventArgs(
					messageId,
					success: true,
					DateTimeOffset.UtcNow - startTime));
			}
			else
			{
				MessageProcessed?.Invoke(this, new MessageProcessedEventArgs(
					messageId,
					success: false,
					DateTimeOffset.UtcNow - startTime));
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing message {MessageId}", messageId);

			MessageProcessed?.Invoke(this, new MessageProcessedEventArgs(
				messageId,
				success: false,
				DateTimeOffset.UtcNow - startTime,
				ex));
		}
		finally
		{
			// Return buffer to pool
			if (buffer != null)
			{
				_bufferPool.Return(buffer, clearArray: true);
			}

			_ = _processingSemaphore.Release();
		}
	}

	/// <summary>
	/// Extends the acknowledgment deadline for a message.
	/// </summary>
	private async Task ExtendAckDeadlineAsync(string ackId, CancellationToken cancellationToken)
	{
		var extensionInterval = TimeSpan.FromSeconds(
			_options.StreamAckDeadlineSeconds * _options.AckExtensionThresholdPercent / 100.0);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(extensionInterval, cancellationToken).ConfigureAwait(false);

				// The actual extension would be done by the parent stream manager
				AckDeadlineExtensionRequested?.Invoke(this, new AckDeadlineExtensionEventArgs(
					ackId,
					_options.StreamAckDeadlineSeconds));
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}
}
