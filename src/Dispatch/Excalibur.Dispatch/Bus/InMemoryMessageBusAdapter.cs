// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Bus;

/// <summary>
/// In-memory message bus adapter for testing and development scenarios.
/// </summary>
public sealed partial class InMemoryMessageBusAdapter : IMessageBusAdapter, IAsyncDisposable
{
	private readonly ILogger<InMemoryMessageBusAdapter> _logger;
	private readonly Channel<PendingMessage> _messageChannel;
	private readonly ChannelWriter<PendingMessage> _writer;
	private readonly ChannelReader<PendingMessage> _reader;

	private readonly ConcurrentDictionary<string, Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>>>
		_subscriptions =
			new(StringComparer.Ordinal);

	private CancellationTokenSource? _processingCts;
	private Task? _processingTask;
	private volatile bool _disposed;

	private readonly record struct PendingMessage(IDispatchMessage Message, IMessageContext Context);

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryMessageBusAdapter" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	public InMemoryMessageBusAdapter(ILogger<InMemoryMessageBusAdapter> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		var options = new BoundedChannelOptions(1000)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false,
		};

		_messageChannel = Channel.CreateBounded<PendingMessage>(options);
		_writer = _messageChannel.Writer;
		_reader = _messageChannel.Reader;
	}

	/// <inheritdoc />
	public string Name => "InMemory";

	/// <inheritdoc />
	public bool SupportsPublishing => true;

	/// <inheritdoc />
	public bool SupportsSubscription => true;

	/// <inheritdoc />
	public bool SupportsTransactions => false;

	/// <inheritdoc />
	public bool IsConnected { get; private set; }

	/// <inheritdoc />
	public async Task InitializeAsync(IMessageBusOptions options, CancellationToken cancellationToken)
	{
		LogInitializing(_logger);
		IsConnected = true;
		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IMessageResult> PublishAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		if (!IsConnected)
		{
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:messaging:connection-failed",
				Title = "Connection Failed",
				ErrorCode = 10001,
				Status = 503,
				Detail = "Message bus is not connected",
				Instance = $"inmemory-adapter-{Guid.NewGuid()}",
			});
		}

		LogPublishingMessage(_logger, context.MessageId ?? string.Empty, message.GetType().Name);

		try
		{
			PrepareMessageContext(message, context);

			await _writer.WriteAsync(new PendingMessage(message, context), cancellationToken).ConfigureAwait(false);
			return MessageResult.Success();
		}
		catch (Exception ex)
		{
			LogFailedToPublishMessage(_logger, ex, context.MessageId ?? string.Empty);
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "urn:dispatch:messaging:publish-failed",
				Title = "Message Publishing Failed",
				ErrorCode = 10002,
				Status = 500,
				Detail = ex.Message,
				Instance = $"message-{context.MessageId ?? string.Empty}",
			});
		}
	}

	/// <inheritdoc />
	public async Task SubscribeAsync(
		string subscriptionName,
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> messageHandler,
		IMessageBusOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
		ArgumentNullException.ThrowIfNull(messageHandler);

		if (!IsConnected)
		{
			throw new InvalidOperationException(ErrorMessages.MessageBusIsNotConnected);
		}

		_subscriptions[subscriptionName] = messageHandler;
		LogSubscribed(_logger, subscriptionName);

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task UnsubscribeAsync(string subscriptionName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

		if (_subscriptions.TryRemove(subscriptionName, out _))
		{
			LogUnsubscribed(_logger, subscriptionName);
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
	{
		if (!IsConnected)
		{
			return HealthCheckResult.Unhealthy("In-memory message bus is not connected");
		}

		var data = new Dictionary<string, object>(StringComparer.Ordinal) { ["IsConnected"] = IsConnected, ["SubscriptionCount"] = _subscriptions.Count };

		await Task.CompletedTask.ConfigureAwait(false);
		return HealthCheckResult.Healthy("In-memory message bus is healthy", data);
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		LogStarting(_logger);
		IsConnected = true;

		if (_processingTask?.IsCompleted != false)
		{
			_processingCts?.Dispose();
			_processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			_processingTask = Task.Run(() => ProcessMessagesAsync(_processingCts.Token), CancellationToken.None);
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		LogStopping(_logger);
		IsConnected = false;
		await (_processingCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
		_ = _writer.TryComplete();

		if (_processingTask is not null)
		{
			try
			{
				await _processingTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (_processingCts?.IsCancellationRequested == true)
			{
			}
		}

		_processingTask = null;

		_processingCts?.Dispose();
		_processingCts = null;

		_subscriptions.Clear();

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await StopAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during cancellation - dispose should not throw
		}
		catch (ObjectDisposedException)
		{
			// Expected if resources already disposed - dispose should not throw
		}

		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			// Cancel processing gracefully if possible
			try
			{
				_processingCts?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				// CTS already disposed - continue with cleanup
			}

			// Complete the channel writer to signal no more messages
			_ = _writer.TryComplete();

			// Don't block on processing task - fire and forget cleanup
			// The task will complete on its own when cancelled
		}
		finally
		{
			try
			{
				_processingCts?.Dispose();
			}
			catch (ObjectDisposedException)
			{
				// Already disposed - ignore
			}

			_processingCts = null;
		}

		GC.SuppressFinalize(this);
	}

	private static void PrepareMessageContext(IDispatchMessage message, IMessageContext context)
	{
		if (context.Message is null)
		{
			context.Message = message;
		}

		context.MessageId ??= Guid.NewGuid().ToString();
		context.MessageType ??= message.GetType().FullName;
		context.ReceivedTimestampUtc = DateTimeOffset.UtcNow;
	}

	private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var pending in _reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				LogDispatchingMessage(_logger, pending.Context.MessageId ?? string.Empty);
				await DispatchToSubscribersAsync(pending, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			await DrainRemainingMessagesAsync().ConfigureAwait(false);
		}
		catch (ChannelClosedException)
		{
			await DrainRemainingMessagesAsync().ConfigureAwait(false);
		}
	}

	private async Task DispatchToSubscribersAsync(PendingMessage pendingMessage, CancellationToken cancellationToken)
	{
		foreach (var handler in _subscriptions.Values)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				_ = await handler(pendingMessage.Message, pendingMessage.Context, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				LogDispatchHandlerFailed(_logger, ex, pendingMessage.Context.MessageId ?? string.Empty);
			}
		}
	}

	private async Task DrainRemainingMessagesAsync()
	{
		while (_reader.TryRead(out var pending))
		{
			LogDispatchingMessage(_logger, pending.Context.MessageId ?? string.Empty);
			await DispatchToSubscribersAsync(pending, CancellationToken.None).ConfigureAwait(false);
		}
	}

	#region LoggerMessage Definitions

	[LoggerMessage(CoreEventId.MessageBusInitializing, LogLevel.Information,
		"Initializing in-memory message bus adapter")]
	private static partial void LogInitializing(ILogger logger);

	[LoggerMessage(CoreEventId.PublishingMessage, LogLevel.Debug,
		"Publishing message {MessageId} of type {MessageType}")]
	private static partial void LogPublishingMessage(ILogger logger, string messageId, string messageType);

	[LoggerMessage(CoreEventId.FailedToPublishMessage, LogLevel.Error,
		"Failed to publish message {MessageId}")]
	private static partial void LogFailedToPublishMessage(ILogger logger, Exception ex, string messageId);

	[LoggerMessage(CoreEventId.Subscribed, LogLevel.Information,
		"Subscribed to {SubscriptionName}")]
	private static partial void LogSubscribed(ILogger logger, string subscriptionName);

	[LoggerMessage(CoreEventId.Unsubscribed, LogLevel.Information,
		"Unsubscribed from {SubscriptionName}")]
	private static partial void LogUnsubscribed(ILogger logger, string subscriptionName);

	[LoggerMessage(CoreEventId.MessageBusConnected, LogLevel.Information,
		"Starting in-memory message bus adapter")]
	private static partial void LogStarting(ILogger logger);

	[LoggerMessage(CoreEventId.MessageBusDisconnected, LogLevel.Information,
		"Stopping in-memory message bus adapter")]
	private static partial void LogStopping(ILogger logger);

	[LoggerMessage(CoreEventId.DispatchingMessage, LogLevel.Trace,
		"Dispatching in-memory message {MessageId} to subscriptions")]
	private static partial void LogDispatchingMessage(ILogger logger, string messageId);

	[LoggerMessage(CoreEventId.DispatchHandlerFailed, LogLevel.Error,
		"Unhandled exception while invoking subscription handler for message {MessageId}")]
	private static partial void LogDispatchHandlerFailed(ILogger logger, Exception ex, string messageId);

	#endregion
}
