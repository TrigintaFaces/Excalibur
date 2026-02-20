// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Orchestrates long polling optimization for multiple SQS queues.
/// </summary>
public sealed partial class LongPollingOptimizer : IDisposable
{
	private readonly ILongPollingReceiver _receiver;
	private readonly ILongPollingStrategy _strategy;
	private readonly IPollingMetricsCollector _metricsCollector;
	private readonly LongPollingConfiguration _configuration;
	private readonly ILogger<LongPollingOptimizer> _logger;

	private readonly ConcurrentDictionary<string, QueuePollingContext> _queueContexts;
	private readonly SemaphoreSlim _coalescingLock;
	private readonly ConcurrentQueue<CoalescingRequest> _coalescingQueue;
	private readonly Timer? _coalescingTimer;
	private readonly CancellationTokenSource _shutdownTokenSource;

	private volatile bool _isDisposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="LongPollingOptimizer" /> class.
	/// </summary>
	public LongPollingOptimizer(
		ILongPollingReceiver receiver,
		ILongPollingStrategy strategy,
		IPollingMetricsCollector metricsCollector,
		LongPollingConfiguration configuration,
		ILogger<LongPollingOptimizer> logger)
	{
		_receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
		_strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
		_metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_queueContexts = new ConcurrentDictionary<string, QueuePollingContext>(StringComparer.Ordinal);
		_coalescingLock = new SemaphoreSlim(1, 1);
		_coalescingQueue = new ConcurrentQueue<CoalescingRequest>();
		_shutdownTokenSource = new CancellationTokenSource();

		if (_configuration.EnableRequestCoalescing)
		{
			_coalescingTimer = new Timer(
				ProcessCoalescedRequests,
				state: null,
				_configuration.CoalescingWindow,
				_configuration.CoalescingWindow);
		}
	}

	/// <summary>
	/// Starts optimized polling for a queue with a message handler.
	/// </summary>
	/// <param name="queueUrl"> The URL of the queue to poll. </param>
	/// <param name="messageHandler"> The handler for processing messages. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task StartPollingAsync(
		string queueUrl,
		Func<Message, CancellationToken, ValueTask> messageHandler,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		var context = _queueContexts.GetOrAdd(
			queueUrl,
			static (url, handler) => new QueuePollingContext { QueueUrl = url, MessageHandler = handler },
			messageHandler);

		context.MessageHandler = messageHandler;

		using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken, _shutdownTokenSource.Token);

		await _receiver.StartContinuousPollingAsync(queueUrl, messageHandler, linkedTokenSource.Token)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Starts optimized polling for multiple queues.
	/// </summary>
	/// <param name="queueHandlers"> Dictionary of queue URLs to their message handlers. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task StartPollingAsync(
		Dictionary<string, Func<Message, CancellationToken, ValueTask>> queueHandlers,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		var tasks = queueHandlers.Select(kvp =>
			StartPollingAsync(kvp.Key, kvp.Value, cancellationToken)).ToList();

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Receives messages from a queue with optional request coalescing.
	/// </summary>
	/// <param name="queueUrl"> The URL of the queue. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The received messages. </returns>
	public async Task<IReadOnlyList<Message>> ReceiveMessagesAsync(
		string queueUrl,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!_configuration.EnableRequestCoalescing)
		{
			return await _receiver.ReceiveMessagesAsync(queueUrl, cancellationToken)
				.ConfigureAwait(false);
		}

		// Queue for coalescing
		var tcs = new TaskCompletionSource<IReadOnlyList<Message>>();
		var request = new CoalescingRequest { QueueUrl = queueUrl, CompletionSource = tcs, CancellationToken = cancellationToken };

		_coalescingQueue.Enqueue(request);

		return await tcs.Task.ConfigureAwait(false);
	}

	/// <summary>
	/// Gets the health status of the optimizer.
	/// </summary>
	/// <returns> The health status. </returns>
	public async Task<HealthStatus> GetHealthStatusAsync()
	{
		try
		{
			var receiverStats = await _receiver.GetStatisticsAsync().ConfigureAwait(false);
			var strategyStats = await _strategy.GetStatisticsAsync().ConfigureAwait(false);

			var isHealthy = receiverStats.PollingStatus != PollingStatus.Error &&
							strategyStats.EmptyReceiveRate < 0.9; // Less than 90% empty receives

			var healthStatus = new HealthStatus
			{
				IsHealthy = isHealthy,
				Status = receiverStats.PollingStatus.ToString(),
				ActiveQueues = _queueContexts.Count,
				TotalMessagesProcessed = receiverStats.TotalMessagesReceived,
				EfficiencyScore = 1 - strategyStats.EmptyReceiveRate,
				LastActivityTime = receiverStats.LastReceiveTime,
				Details =
				{
					["ApiCallsSaved"] = strategyStats.ApiCallsSaved,
					["CurrentLoadFactor"] = strategyStats.CurrentLoadFactor,
					["CurrentWaitTime"] = strategyStats.CurrentWaitTime.TotalSeconds,
					["AverageMessagesPerReceive"] = strategyStats.AverageMessagesPerReceive,
				},
			};

			return healthStatus;
		}
		catch (Exception ex)
		{
			LogHealthStatusError(ex);
			var errorStatus = new HealthStatus { IsHealthy = false, Status = "Error", Details = { ["Error"] = ex.Message } };
			return errorStatus;
		}
	}

	/// <summary>
	/// Gets optimization statistics for all queues.
	/// </summary>
	/// <returns> Dictionary of queue URLs to their optimization statistics. </returns>
	public Task<Dictionary<string, OptimizationStatistics>> GetOptimizationStatisticsAsync()
	{
		var results = new Dictionary<string, OptimizationStatistics>(StringComparer.Ordinal);

		foreach (var context in _queueContexts.Values)
		{
			var stats = _metricsCollector.GetStatistics();

			results[context.QueueUrl] = new OptimizationStatistics
			{
				QueueUrl = new Uri(context.QueueUrl),
				TotalMessages = stats.TotalMessagesReceived,
				ApiCallsSaved = 0, // Not tracked in simplified interface
				EfficiencyScore = 0.0, // Not tracked in simplified interface
				AverageLatency = stats.AveragePollDuration,
				EmptyReceiveRate = stats.TotalAttempts > 0
					? (double)(stats.TotalAttempts - stats.SuccessfulAttempts) / stats.TotalAttempts
					: 0,
				LastUpdated = DateTimeOffset.UtcNow,
			};
		}

		return Task.FromResult(results);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}

		_isDisposed = true;
		_shutdownTokenSource.Cancel();

		_coalescingTimer?.Dispose();
		_receiver?.Dispose();
		_coalescingLock?.Dispose();
		_shutdownTokenSource?.Dispose();
	}

	private async void ProcessCoalescedRequests(object? state)
	{
		if (_isDisposed)
		{
			return;
		}

		await _coalescingLock.WaitAsync().ConfigureAwait(false);
		try
		{
			var requests = new List<CoalescingRequest>();
			while (_coalescingQueue.TryDequeue(out var request))
			{
				if (!request.CancellationToken.IsCancellationRequested)
				{
					requests.Add(request);
				}
			}

			if (requests.Count == 0)
			{
				return;
			}

			// Group by similar wait times
			var groups = requests.GroupBy(r => r.QueueUrl, StringComparer.Ordinal).ToList();

			if (groups.Count > 1)
			{
				var requestsSaved = requests.Count - groups.Count;
				_metricsCollector.RecordMetric("RequestCoalescing", requestsSaved, MetricUnit.Count);
			}

			// Process each group
			var tasks = groups.Select(async group =>
			{
				try
				{
					var messages = await _receiver.ReceiveMessagesAsync(
						group.Key, group.First().CancellationToken).ConfigureAwait(false);

					// Distribute messages to requesters
					var messageList = messages.ToList();
					var index = 0;

					foreach (var request in group)
					{
						var requestMessages = new List<Message>();
						var messagesPerRequest = Math.Max(1, messageList.Count / group.Count());

						for (var i = 0; i < messagesPerRequest && index < messageList.Count; i++)
						{
							requestMessages.Add(messageList[index++]);
						}

						request.CompletionSource.SetResult(requestMessages);
					}
				}
				catch (Exception ex)
				{
					foreach (var request in group)
					{
						request.CompletionSource.SetException(ex);
					}
				}
			});

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
		finally
		{
			_ = _coalescingLock.Release();
		}
	}

	private void ThrowIfDisposed()
	{
		if (_isDisposed)
		{
			throw new ObjectDisposedException(nameof(LongPollingOptimizer));
		}
	}

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.LongPollingHealthStatusError, LogLevel.Error,
		"Error getting health status")]
	private partial void LogHealthStatusError(Exception ex);

	private sealed class QueuePollingContext
	{
		public required string QueueUrl { get; init; }

		public required Func<Message, CancellationToken, ValueTask> MessageHandler { get; set; }
	}

	private sealed class CoalescingRequest
	{
		public required string QueueUrl { get; init; }

		public required TaskCompletionSource<IReadOnlyList<Message>> CompletionSource { get; init; }

		public CancellationToken CancellationToken { get; init; }
	}
}
