// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Threading.Channels;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Channels;

using Microsoft.Extensions.Logging;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Memory channel message pump for processing messages.
/// </summary>
public partial class MessagePump<T> : IDisposable
{
	// R0.8: Remove unread private members - reserved for future memory optimization
#pragma warning disable IDE0052
	private readonly MemoryPool<byte> _memoryPool;
#pragma warning restore IDE0052
	private readonly ChannelMessagePumpOptions _options;
	private readonly ILogger? _logger;
	private readonly Channel<MessageEnvelope> _channel;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private Task? _processingTask;
	private int _messagesConsumed;
	private int _messagesFailed;


	/// <summary>
	/// Initializes a new instance of the <see cref="MessagePump{T}" /> class.
	/// </summary>
	/// <param name="name"> The name of the message pump. </param>
	/// <param name="memoryPool"> The memory pool to use for message allocation. </param>
	/// <param name="options"> Optional configuration options for the message pump. </param>
	/// <param name="logger"> Optional logger for diagnostics. </param>
	public MessagePump(
		string name,
		MemoryPool<byte> memoryPool,
		ChannelMessagePumpOptions? options = null,
		ILogger? logger = null)
	{
		Name = name ?? "DefaultPump";
		_memoryPool = memoryPool ?? MemoryPool<byte>.Shared;
		_options = options ?? new ChannelMessagePumpOptions();
		_logger = logger;
		_cancellationTokenSource = new CancellationTokenSource();

		var dispatchChannelOptions =
			new UnboundedChannelOptions { SingleReader = _options.SingleReader, SingleWriter = _options.SingleWriter };
		_channel = Channel.CreateUnbounded<MessageEnvelope>(dispatchChannelOptions);
	}

	/// <summary>
	/// Occurs when a message is received and processed by the pump.
	/// </summary>
	public event EventHandler<MemoryMessageEventArgs>? MessageReceived;

	/// <summary>
	/// Gets the name of the message pump.
	/// </summary>
	/// <value>The current <see cref="Name"/> value.</value>
	public string Name { get; }

	/// <summary>
	/// Gets the channel writer for adding messages to the pump.
	/// </summary>
	/// <value>The current <see cref="Writer"/> value.</value>
	public ChannelWriter<MessageEnvelope> Writer => _channel.Writer;

	/// <summary>
	/// Gets the channel reader for consuming messages from the pump.
	/// </summary>
	/// <value>The current <see cref="Reader"/> value.</value>
	public ChannelReader<MessageEnvelope> Reader => _channel.Reader;

	/// <summary>
	/// Gets the total number of messages consumed by the pump.
	/// </summary>
	/// <value>The current <see cref="MessagesConsumed"/> value.</value>
	public int MessagesConsumed => _messagesConsumed;

	/// <summary>
	/// Gets the total number of messages that failed processing.
	/// </summary>
	/// <value>The current <see cref="MessagesFailed"/> value.</value>
	public int MessagesFailed => _messagesFailed;

	/// <summary>
	/// Starts the message pump processing.
	/// </summary>
	public void Start() => _processingTask = ProcessMessagesAsync(_cancellationTokenSource.Token);

	/// <summary>
	/// Stops the message pump processing asynchronously.
	/// </summary>
	/// <returns> A task that represents the asynchronous stop operation. </returns>
	public async Task StopAsync()
	{
		await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
		_ = _channel.Writer.TryComplete();
		if (_processingTask != null)
		{
			await _processingTask.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Disposes the message pump.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the message pump resources.
	/// </summary>
	/// <param name="disposing"> True if disposing managed resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			_cancellationTokenSource?.Cancel();
			_cancellationTokenSource?.Dispose();
			_processingTask?.Dispose();
		}
	}

	/// <summary>
	/// Called when a message is successfully consumed.
	/// </summary>
	protected virtual void OnMessageConsumed() => Interlocked.Increment(ref _messagesConsumed);

	/// <summary>
	/// Called when a message fails to be processed.
	/// </summary>
	protected virtual void OnMessageFailed() => Interlocked.Increment(ref _messagesFailed);

	private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				try
				{
					if (MessageReceived != null)
					{
						var eventArgs = new MemoryMessageEventArgs(message, cancellationToken);
						MessageReceived.Invoke(this, eventArgs);
					}

					OnMessageConsumed();
				}
				catch (Exception ex)
				{
					OnMessageFailed();
					if (_logger != null)
					{
						LogErrorProcessingMessage(_logger, ex);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation is requested
		}
		catch (Exception ex)
		{
			if (_logger != null)
			{
				LogErrorInMessagePump(_logger, ex);
			}
		}
	}

	#region LoggerMessage Definitions

	[LoggerMessage(CoreEventId.MessageProcessingError, LogLevel.Error,
		"Error processing message")]
	private static partial void LogErrorProcessingMessage(ILogger logger, Exception ex);

	[LoggerMessage(CoreEventId.MessagePumpError, LogLevel.Error,
		"Error in message pump")]
	private static partial void LogErrorInMessagePump(ILogger logger, Exception ex);

	#endregion
}
