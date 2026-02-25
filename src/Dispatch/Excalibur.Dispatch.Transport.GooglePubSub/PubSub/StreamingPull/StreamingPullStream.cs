// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;

using Grpc.Core;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents a single streaming pull connection to Google Pub/Sub.
/// </summary>
internal sealed partial class StreamingPullStream : IAsyncDisposable
{
	private readonly ILogger<StreamingPullStream> _logger;
	private readonly SubscriberServiceApiClient _subscriberClient;
	private readonly SubscriptionName _subscriptionName;
	private readonly StreamingPullOptions _options;
	private readonly Channel<ReceivedMessage> _messageChannel;
	private readonly ConcurrentDictionary<string, ReceivedMessage> _outstandingMessages;
	private readonly SemaphoreSlim _streamLock;
	private readonly CancellationTokenSource _streamCancellationSource;
	private SubscriberServiceApiClient.StreamingPullStream? _grpcStream;
	private Task? _readTask;
	private Task? _writeTask;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamingPullStream" /> class.
	/// </summary>
	/// <param name="streamId"> The unique identifier for this stream. </param>
	/// <param name="logger"> The logger instance for this stream. </param>
	/// <param name="subscriberClient"> The Pub/Sub subscriber client. </param>
	/// <param name="subscriptionName"> The name of the subscription to pull from. </param>
	/// <param name="options"> The streaming pull options. </param>
	public StreamingPullStream(
		string streamId,
		ILogger<StreamingPullStream> logger,
		SubscriberServiceApiClient subscriberClient,
		SubscriptionName subscriptionName,
		StreamingPullOptions options)
	{
		StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_subscriberClient = subscriberClient ?? throw new ArgumentNullException(nameof(subscriberClient));
		_subscriptionName = subscriptionName ?? throw new ArgumentNullException(nameof(subscriptionName));
		_options = options ?? throw new ArgumentNullException(nameof(options));

		_outstandingMessages = new ConcurrentDictionary<string, ReceivedMessage>();
		_streamLock = new SemaphoreSlim(1, 1);
		_streamCancellationSource = new CancellationTokenSource();

		var channelOptions = new BoundedChannelOptions(_options.MaxOutstandingMessagesPerStream)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleWriter = true,
			SingleReader = true,
		};
		_messageChannel = Channel.CreateBounded<ReceivedMessage>(channelOptions);
	}

	/// <summary>
	/// Gets the stream identifier.
	/// </summary>
	/// <value>
	/// The stream identifier.
	/// </value>
	public string StreamId { get; }

	/// <summary>
	/// Starts the streaming pull connection.
	/// </summary>
	/// <exception cref="ObjectDisposedException"></exception>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(StreamingPullStream));
		}

		// Create the gRPC streaming call
		var callSettings = CallSettings.FromCancellationToken(cancellationToken);
		_grpcStream = _subscriberClient.StreamingPull(callSettings);

		// Send initial request
		var initialRequest = new StreamingPullRequest
		{
			Subscription = _subscriptionName.ToString(),
			StreamAckDeadlineSeconds = _options.StreamAckDeadlineSeconds,
			MaxOutstandingMessages = _options.MaxOutstandingMessagesPerStream,
			MaxOutstandingBytes = _options.MaxOutstandingBytesPerStream,
		};

		await _grpcStream.WriteAsync(initialRequest).ConfigureAwait(false);

		// Start read and write tasks
		_readTask = ReadMessagesFromStreamAsync(_streamCancellationSource.Token);
		_writeTask = WriteAcknowledgmentsAsync(_streamCancellationSource.Token);
	}

	/// <summary>
	/// Reads messages from the stream asynchronously.
	/// </summary>
	public async IAsyncEnumerable<ReceivedMessage> ReadMessagesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return message;
		}
	}

	/// <summary>
	/// Acknowledges a message.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task AckAsync(string ackId, CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		await _streamLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_ = _outstandingMessages.TryRemove(ackId, out _);

			var request = new StreamingPullRequest { AckIds = { ackId } };

			if (_grpcStream != null)
			{
				await _grpcStream.WriteAsync(request).ConfigureAwait(false);
			}
		}
		finally
		{
			_ = _streamLock.Release();
		}
	}

	/// <summary>
	/// Negatively acknowledges a message.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task NackAsync(string ackId, CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		await ModifyAckDeadlineAsync(ackId, 0, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Modifies the acknowledgment deadline for a message.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task ModifyAckDeadlineAsync(string ackId, int seconds, CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		await _streamLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var request = new StreamingPullRequest { ModifyDeadlineAckIds = { ackId }, ModifyDeadlineSeconds = { seconds } };

			if (_grpcStream != null)
			{
				await _grpcStream.WriteAsync(request).ConfigureAwait(false);
			}
		}
		finally
		{
			_ = _streamLock.Release();
		}
	}

	/// <summary>
	/// Checks if the stream has a specific message.
	/// </summary>
	public bool HasMessage(string ackId) => _outstandingMessages.ContainsKey(ackId);

	/// <summary>
	/// Disposes the stream.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await _streamCancellationSource.CancelAsync().ConfigureAwait(false);

		// Complete and dispose the gRPC stream
		if (_grpcStream != null)
		{
			await _grpcStream.WriteCompleteAsync().ConfigureAwait(false);
			_grpcStream.Dispose();
		}

		// Wait for tasks to complete
		if (_readTask != null)
		{
			try
			{
				await _readTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException ex)
			{
				// Expected during shutdown
				LogTaskCleanupCancelled("ReadTask", StreamId, ex);
			}
			catch (ObjectDisposedException ex)
			{
				// Expected during disposal
				LogTaskCleanupDisposed("ReadTask", StreamId, ex);
			}
			catch (Exception ex)
			{
				// Log unexpected exceptions during cleanup
				LogTaskCleanupFailed("ReadTask", StreamId, ex);
			}
		}

		if (_writeTask != null)
		{
			try
			{
				await _writeTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException ex)
			{
				// Expected during shutdown
				LogTaskCleanupCancelled("WriteTask", StreamId, ex);
			}
			catch (ObjectDisposedException ex)
			{
				// Expected during disposal
				LogTaskCleanupDisposed("WriteTask", StreamId, ex);
			}
			catch (Exception ex)
			{
				// Log unexpected exceptions during cleanup
				LogTaskCleanupFailed("WriteTask", StreamId, ex);
			}
		}

		// Cleanup
		_streamCancellationSource.Dispose();
		_streamLock.Dispose();
		_outstandingMessages.Clear();
	}

	/// <summary>
	/// Reads messages from the gRPC stream.
	/// </summary>
	private async Task ReadMessagesFromStreamAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_grpcStream == null)
			{
				return;
			}

			await foreach (var response in _grpcStream.GetResponseStream().WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				foreach (var message in response.ReceivedMessages)
				{
					await _streamLock.WaitAsync(cancellationToken).ConfigureAwait(false);
					try
					{
						_outstandingMessages[message.AckId] = message;
					}
					finally
					{
						_ = _streamLock.Release();
					}

					await _messageChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
		{
			// Expected during shutdown
		}
		catch (Exception ex)
		{
			// Log error and complete the channel
			_ = _messageChannel.Writer.TryComplete(ex);
		}
		finally
		{
			_ = _messageChannel.Writer.TryComplete();
		}
	}

	/// <summary>
	/// Periodically writes acknowledgments and flow control.
	/// </summary>
	private async Task WriteAcknowledgmentsAsync(CancellationToken cancellationToken)
	{
		var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

		try
		{
			while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
			{
				// Send flow control update
				var request = new StreamingPullRequest
				{
					MaxOutstandingMessages = _options.MaxOutstandingMessagesPerStream,
					MaxOutstandingBytes = _options.MaxOutstandingBytesPerStream,
				};

				if (_grpcStream != null)
				{
					await _grpcStream.WriteAsync(request).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}
		finally
		{
			timer.Dispose();
		}
	}

	// Source-generated logging methods (Sprint 363 - EventId Migration)
	[LoggerMessage(GooglePubSubEventId.TaskCleanupFailed, LogLevel.Error,
		"Task {TaskName} cleanup failed for stream {StreamId}")]
	private partial void LogTaskCleanupFailed(string taskName, string streamId, Exception ex);

	[LoggerMessage(GooglePubSubEventId.TaskCleanupCancelled, LogLevel.Debug,
		"Task {TaskName} cleanup cancelled for stream {StreamId}")]
	private partial void LogTaskCleanupCancelled(string taskName, string streamId, Exception ex);

	[LoggerMessage(GooglePubSubEventId.TaskCleanupDisposed, LogLevel.Debug,
		"Task {TaskName} cleanup disposed for stream {StreamId}")]
	private partial void LogTaskCleanupDisposed(string taskName, string streamId, Exception ex);
}
