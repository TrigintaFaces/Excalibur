// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Threading.Channels;


using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Enhanced parallel message processor with strict ordering guarantees.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="OrderedParallelProcessor" /> class. </remarks>
/// <param name="parallelProcessor"> The underlying parallel processor. </param>
/// <param name="orderingKeyManager"> The ordering key manager. </param>
/// <param name="orderingMetrics"> Ordering metrics collector. </param>
/// <param name="logger"> Logger instance. </param>
public sealed class OrderedParallelProcessor(
	ParallelMessageProcessor parallelProcessor,
	OrderingKeyManager orderingKeyManager,
	OrderingKeyMetrics orderingMetrics,
	ILogger<OrderedParallelProcessor> logger) : IAsyncDisposable
{
	private readonly ParallelMessageProcessor _parallelProcessor =
		parallelProcessor ?? throw new ArgumentNullException(nameof(parallelProcessor));

	private readonly OrderingKeyManager _orderingKeyManager =
		orderingKeyManager ?? throw new ArgumentNullException(nameof(orderingKeyManager));

	private readonly OrderingKeyMetrics _orderingMetrics = orderingMetrics ?? throw new ArgumentNullException(nameof(orderingMetrics));
	private readonly ILogger<OrderedParallelProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	private readonly ConcurrentDictionary<string, Channel<OrderedWorkItem>> _orderingQueues = new(StringComparer.Ordinal);

	private readonly ConcurrentDictionary<string, Task>
		_orderingProcessors = new(StringComparer.Ordinal);

	private readonly CancellationTokenSource _shutdownTokenSource = new();
	private volatile bool _disposed;

	/// <summary>
	/// Processes a message with ordering guarantees.
	/// </summary>
	/// <param name="message"> The message to process. </param>
	/// <param name="handler"> The message handler. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Processing result. </returns>
	public async Task<OrderedProcessingResult> ProcessAsync(
		PubsubMessage message,
		Func<PubsubMessage, CancellationToken, Task> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(handler);

		// Messages without ordering key go directly to parallel processor
		if (string.IsNullOrEmpty(message.OrderingKey))
		{
			var result = await _parallelProcessor.EnqueueAsync(message, handler, cancellationToken)
				.ConfigureAwait(false);

			return new OrderedProcessingResult
			{
				Success = result.Success,
				WorkerId = result.WorkerId,
				ProcessingTime = result.ProcessingTime,
				WasOrdered = false,
			};
		}

		// Messages with ordering key need sequential processing
		return await ProcessOrderedMessageAsync(message, handler, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Gets the status of an ordering key.
	/// </summary>
	/// <param name="orderingKey"> The ordering key. </param>
	/// <returns> Ordering key information. </returns>
	public OrderingKeyInfo? GetOrderingKeyStatus(string orderingKey) => _orderingKeyManager.GetOrderingKeyInfo(orderingKey);

	/// <summary>
	/// Resets a failed ordering key.
	/// </summary>
	/// <param name="orderingKey"> The ordering key to reset. </param>
	/// <returns> True if reset, false otherwise. </returns>
	public bool ResetFailedOrderingKey(string orderingKey)
	{
		if (_orderingKeyManager.ResetFailedKey(orderingKey))
		{
			_orderingMetrics.RecordOrderingKeyReset();

			// Resume processing if there's a queue
			if (_orderingQueues.TryGetValue(orderingKey, out var channel))
			{
				_ = ProcessOrderingQueueAsync(orderingKey, channel);
			}

			return true;
		}

		return false;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);

		// Complete all ordering queues
		foreach (var queue in _orderingQueues.Values)
		{
			_ = queue.Writer.TryComplete();
		}

		// Wait for all ordering processors
		await Task.WhenAll(_orderingProcessors.Values).ConfigureAwait(false);

		// Dispose components
		await _parallelProcessor.DisposeAsync().ConfigureAwait(false);
		_orderingKeyManager.Dispose();
		_orderingMetrics.Dispose();
		_shutdownTokenSource.Dispose();
	}

	private async Task<OrderedProcessingResult> ProcessOrderedMessageAsync(
		PubsubMessage message,
		Func<PubsubMessage, CancellationToken, Task> handler,
		CancellationToken cancellationToken)
	{
		var orderingKey = message.OrderingKey;
		var completionSource = new TaskCompletionSource<OrderedProcessingResult>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		// Extract sequence number if available
		long? sequenceNumber = null;
		if (message.Attributes.TryGetValue("sequence_number", out var seqStr) &&
			long.TryParse(seqStr, out var seq))
		{
			sequenceNumber = seq;
		}

		// Record the message
		var inSequence = _orderingKeyManager.RecordMessage(
			orderingKey,
			message.MessageId,
			sequenceNumber);

		if (inSequence)
		{
			_orderingMetrics.RecordInSequenceMessage();
		}
		else
		{
			var gap = sequenceNumber.HasValue
				? Math.Abs(sequenceNumber.Value - (_orderingKeyManager.GetOrderingKeyInfo(orderingKey)?.ExpectedSequence ?? 0))
				: 1;
			_orderingMetrics.RecordOutOfSequenceMessage(gap);
		}

		// Get or create ordering queue
		var queue = _orderingQueues.GetOrAdd(orderingKey, _ =>
		{
			_orderingMetrics.RecordOrderingKeyCreated();
			var channel = Channel.CreateUnbounded<OrderedWorkItem>(
				new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

			// Start processor for this ordering key
			_orderingProcessors[orderingKey] = ProcessOrderingQueueAsync(orderingKey, channel);

			return channel;
		});

		// Enqueue work item
		var workItem = new OrderedWorkItem
		{
			Message = message,
			Handler = handler,
			CompletionSource = completionSource,
			SequenceNumber = sequenceNumber,
			EnqueuedAt = DateTimeOffset.UtcNow,
		};

		await queue.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);

		return await completionSource.Task.ConfigureAwait(false);
	}

	private async Task ProcessOrderingQueueAsync(string orderingKey, Channel<OrderedWorkItem> queue)
	{
		_logger.LogDebug("Started ordering processor for key {OrderingKey}", orderingKey);

		try
		{
			await foreach (var workItem in queue.Reader.ReadAllAsync(_shutdownTokenSource.Token).ConfigureAwait(false))
			{
				// Check if ordering key is failed
				var keyInfo = _orderingKeyManager.GetOrderingKeyInfo(orderingKey);
				if (keyInfo?.IsFailed == true)
				{
					workItem.CompletionSource.SetException(
						new InvalidOperationException($"Ordering key {orderingKey} is in failed state: {keyInfo.FailureReason}"));
					continue;
				}

				try
				{
					// Process through parallel processor (which maintains worker affinity)
					var result = await _parallelProcessor.EnqueueAsync(
						workItem.Message,
						workItem.Handler,
						_shutdownTokenSource.Token).ConfigureAwait(false);

					workItem.CompletionSource.SetResult(new OrderedProcessingResult
					{
						Success = result.Success,
						WorkerId = result.WorkerId,
						ProcessingTime = result.ProcessingTime,
						WasOrdered = true,
						QueueTime = DateTimeOffset.UtcNow - workItem.EnqueuedAt,
					});
				}
				catch (Exception ex)
				{
					// Mark ordering key as failed
					_orderingKeyManager.MarkFailed(orderingKey, ex.Message);
					_orderingMetrics.RecordOrderingKeyFailed();

					workItem.CompletionSource.SetException(ex);

					// Drain remaining items in queue
					await DrainOrderingQueueAsync(orderingKey, queue, ex).ConfigureAwait(false);
					break;
				}
			}
		}
		catch (OperationCanceledException) when (_shutdownTokenSource.Token.IsCancellationRequested)
		{
			// Expected during shutdown
		}
		finally
		{
			_ = _orderingQueues.TryRemove(orderingKey, out _);
			_ = _orderingProcessors.TryRemove(orderingKey, out _);
			_logger.LogDebug("Stopped ordering processor for key {OrderingKey}", orderingKey);
		}
	}

	private async Task DrainOrderingQueueAsync(
		string orderingKey,
		Channel<OrderedWorkItem> queue,
		Exception failureException)
	{
		_logger.LogWarning(
			"Draining ordering queue for failed key {OrderingKey}",
			orderingKey);

		await Task.Yield(); // Ensure async context

		while (queue.Reader.TryRead(out var workItem))
		{
			workItem.CompletionSource.SetException(
				new InvalidOperationException(
					$"Ordering key {orderingKey} failed: {failureException.Message}",
					failureException));
		}

		_ = queue.Writer.TryComplete(failureException);
	}

	/// <summary>
	/// Represents an ordered work item.
	/// </summary>
	private sealed class OrderedWorkItem
	{
		public required PubsubMessage Message { get; init; }

		public required Func<PubsubMessage, CancellationToken, Task> Handler { get; init; }

		public required TaskCompletionSource<OrderedProcessingResult> CompletionSource { get; init; }

		public long? SequenceNumber { get; init; }

		public required DateTimeOffset EnqueuedAt { get; init; }
	}
}
