// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Batch processor that maintains message ordering based on ordering keys. Messages with the same ordering key are processed sequentially.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OrderedBatchProcessor" /> class.
/// </remarks>
/// <param name="options"> Batch configuration options. </param>
/// <param name="messageProcessor"> The message processing function. </param>
/// <param name="logger"> Logger instance. </param>
/// <param name="metricsCollector"> Metrics collector. </param>
public class OrderedBatchProcessor(
	IOptions<BatchConfiguration> options,
	Func<ReceivedMessage, CancellationToken, Task<object>> messageProcessor,
	ILogger<OrderedBatchProcessor> logger,
	BatchMetricsCollector metricsCollector) : BatchProcessorBase(logger, metricsCollector)
{
	private readonly Func<ReceivedMessage, CancellationToken, Task<object>> _messageProcessor =
		messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));

	private readonly ConcurrentDictionary<string, SemaphoreSlim> _orderingKeySemaphores = new(StringComparer.Ordinal);

	/// <inheritdoc />
	protected internal override async Task ProcessBatchCoreAsync(
		MessageBatch batch,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken)
	{
		// Group messages by ordering key
		var messageGroups = batch.Messages
			.GroupBy(static m => m.Message.OrderingKey ?? string.Empty, StringComparer.Ordinal)
			.ToList();

		Logger.LogDebug(
			"Processing batch of {MessageCount} messages with {GroupCount} ordering keys",
			batch.Count,
			messageGroups.Count);

		// Process each group
		var tasks = new List<Task>();

		foreach (var group in messageGroups)
		{
			if (string.IsNullOrEmpty(group.Key))
			{
				// Messages without ordering key can be processed in parallel
				foreach (var message in group)
				{
					tasks.Add(ProcessMessageAsync(
						message,
						successfulMessages,
						failedMessages,
						cancellationToken));
				}
			}
			else
			{
				// Messages with the same ordering key must be processed sequentially
				var semaphore = _orderingKeySemaphores.GetOrAdd(
					group.Key,
					static _ => new SemaphoreSlim(1, 1));

				tasks.Add(ProcessOrderedGroupAsync(
					[.. group],
					semaphore,
					successfulMessages,
					failedMessages,
					cancellationToken));
			}
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Clean up unused semaphores periodically
		CleanupSemaphores();
	}

	/// <inheritdoc />
	protected override Task<object> ProcessMessageCoreAsync(ReceivedMessage message, CancellationToken cancellationToken) =>
		_messageProcessor(message, cancellationToken);

	/// <inheritdoc />
	protected override bool DetermineRetryPolicy(Exception exception) =>

		// For ordered processing, be more conservative with retries to maintain message order integrity
		exception is TimeoutException ||
		(exception is InvalidOperationException &&
		 exception.Message.Contains("transient", StringComparison.OrdinalIgnoreCase));

	/// <inheritdoc />
	protected override TimeSpan GetRetryDelay(Exception exception) =>

		// Use shorter delays for ordered processing to minimize out-of-order risk
		TimeSpan.FromSeconds(Math.Min(5, Math.Pow(2, 1)));

	/// <summary>
	/// Processes a group of messages with the same ordering key sequentially.
	/// </summary>
	private async Task ProcessOrderedGroupAsync(
		List<ReceivedMessage> messages,
		SemaphoreSlim semaphore,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken)
	{
		await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Process messages sequentially within the ordering key
			foreach (var message in messages)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				await ProcessMessageAsync(
					message,
					successfulMessages,
					failedMessages,
					cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			_ = semaphore.Release();
		}
	}

	/// <summary>
	/// Cleans up unused semaphores to prevent memory leaks.
	/// </summary>
	private void CleanupSemaphores()
	{
		// Only cleanup if we have too many semaphores
		if (_orderingKeySemaphores.Count > 1000)
		{
			var keysToRemove = new List<string>();

			foreach (var kvp in _orderingKeySemaphores)
			{
				if (kvp.Value.CurrentCount == 1) // Not in use
				{
					keysToRemove.Add(kvp.Key);
				}
			}

			foreach (var key in keysToRemove)
			{
				if (_orderingKeySemaphores.TryRemove(key, out var semaphore))
				{
					semaphore.Dispose();
				}
			}

			if (keysToRemove.Count > 0)
			{
				Logger.LogDebug(
					"Cleaned up {Count} unused ordering key semaphores",
					keysToRemove.Count);
			}
		}
	}
}
