// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Memory channel message pump implementation.
/// </summary>
public class MessagePump : IChannelMessagePump
{
	private readonly Channel<MessageEnvelope> _messageChannel;
	private readonly Channel<MessageEnvelope> _channel;
	private readonly Func<MessageEnvelope, Task> _messageHandler;
	private readonly ChannelMessagePumpOptions _options;
	private CancellationTokenSource? _cancellationTokenSource;
	private Task? _processingTask;
	private volatile bool _isRunning;
	private int _messagesConsumed;
	private int _messagesFailed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessagePump" /> class.
	/// </summary>
	public MessagePump(
		string name,
		Channel<MessageEnvelope> channel,
		Func<MessageEnvelope, Task> messageHandler,
		ChannelMessagePumpOptions? options = null)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
		_options = options ?? new ChannelMessagePumpOptions();

		// Create message envelope channel based on options
		var channelOptions = new BoundedChannelOptions(_options.Capacity)
		{
			FullMode = _options.FullMode,
			SingleReader = _options.SingleReader,
			SingleWriter = _options.SingleWriter,
		};
		_messageChannel = Channel.CreateBounded<MessageEnvelope>(channelOptions);
	}

	/// <summary>
	/// Gets a value indicating whether the pump is currently running.
	/// </summary>
	/// <value>The current <see cref="IsRunning"/> value.</value>
	public bool IsRunning => _isRunning;

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public ChannelMessagePumpStatus Status { get; private set; } = ChannelMessagePumpStatus.NotStarted;

	/// <inheritdoc />
	public ChannelReader<MessageEnvelope> Reader => _messageChannel.Reader;

	/// <inheritdoc />
	public ChannelWriter<MessageEnvelope>? Writer => _messageChannel.Writer;

	/// <inheritdoc />
	public ChannelMessagePumpMetrics Metrics { get; } = new();

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (Status == ChannelMessagePumpStatus.Running)
		{
			throw new InvalidOperationException("Message pump is already running");
		}

		Status = ChannelMessagePumpStatus.Starting;
		_isRunning = true;
		_cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_processingTask = ProcessMessagesAsync(_cancellationTokenSource.Token);
		Status = ChannelMessagePumpStatus.Running;
		Metrics.StartedAt = DateTimeOffset.UtcNow;

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		_isRunning = false;
		if (Status != ChannelMessagePumpStatus.Running)
		{
			return;
		}

		Status = ChannelMessagePumpStatus.Stopping;
		await (_cancellationTokenSource?.CancelAsync()).ConfigureAwait(false);

		if (_processingTask != null)
		{
			await _processingTask.ConfigureAwait(false);
		}

		_cancellationTokenSource?.Dispose();
		_cancellationTokenSource = null;
		_processingTask = null;
		Status = ChannelMessagePumpStatus.Stopped;
	}

	/// <inheritdoc />
	public IChannelMessagePump Configure(Action<ChannelMessagePumpOptions>? configure)
	{
		configure?.Invoke(_options);
		return this;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await StopAsync(CancellationToken.None).ConfigureAwait(false);
		_ = _messageChannel.Writer.TryComplete();
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
	/// Releases the unmanaged resources used by the message pump and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
	[SuppressMessage("AsyncUsage", "VSTHRD002:Avoid problematic synchronous waits",
		Justification = "IDisposable.Dispose() is synchronous by contract. Prefer DisposeAsync when available.")]
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (_isRunning)
			{
				// Use ConfigureAwait(false) to prevent deadlocks in synchronous disposal
				StopAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
			}

			_cancellationTokenSource?.Dispose();
			_ = (_channel?.Writer.TryComplete());
		}
	}

	protected virtual void OnMessageConsumed() => Interlocked.Increment(ref _messagesConsumed);

	protected virtual void OnMessageFailed() => Interlocked.Increment(ref _messagesFailed);

	/// <summary>
	/// </summary>
	/// <param name="writer"> </param>
	/// <param name="cancellationToken"> </param>
	/// <returns> A <see cref="Task" /> representing the result of the asynchronous operation. </returns>
	protected virtual async Task ProduceMessagesAsync(ChannelWriter<MessageEnvelope> writer, CancellationToken cancellationToken) =>

		// This method should be overridden by derived classes
		await Task.CompletedTask.ConfigureAwait(false);

	private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var envelope in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				try
				{
					await _messageHandler(envelope).ConfigureAwait(false);
				}
				catch (Exception)
				{
					// Log error
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Expected during normal shutdown.
		}
	}
}
