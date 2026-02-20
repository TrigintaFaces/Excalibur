// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Batch processor that adaptively switches between parallel and ordered processing based on message characteristics and system performance.
/// </summary>
public class AdaptiveBatchProcessor : BatchProcessorBase
{
	private readonly Func<ReceivedMessage, CancellationToken, Task<object>> _messageProcessor;
	private readonly IOptions<BatchConfiguration> _options;
	private readonly OrderedBatchProcessor _orderedProcessor;
	private readonly ParallelBatchProcessor _parallelProcessor;
	private readonly AdaptiveMetrics _metrics;

	/// <summary>
	/// Initializes a new instance of the <see cref="AdaptiveBatchProcessor" /> class.
	/// </summary>
	/// <param name="options"> Batch configuration options. </param>
	/// <param name="messageProcessor"> The message processing function. </param>
	/// <param name="logger"> Logger instance. </param>
	/// <param name="loggerFactory"> Logger factory to create child loggers. </param>
	/// <param name="metricsCollector"> Metrics collector. </param>
	public AdaptiveBatchProcessor(
		IOptions<BatchConfiguration> options,
		Func<ReceivedMessage, CancellationToken, Task<object>> messageProcessor,
		ILogger<AdaptiveBatchProcessor> logger,
		ILoggerFactory loggerFactory,
		BatchMetricsCollector metricsCollector)
		: base(logger, metricsCollector)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
		ArgumentNullException.ThrowIfNull(loggerFactory);

		// Create internal processors
		_orderedProcessor = new OrderedBatchProcessor(
			options,
			messageProcessor,
			loggerFactory.CreateLogger<OrderedBatchProcessor>(),
			metricsCollector);

		_parallelProcessor = new ParallelBatchProcessor(
			options,
			messageProcessor,
			loggerFactory.CreateLogger<ParallelBatchProcessor>(),
			metricsCollector);

		_metrics = new AdaptiveMetrics();
	}

	/// <summary>
	/// Processing strategy options.
	/// </summary>
	private enum ProcessingStrategy
	{
		Parallel = 0,
		Ordered = 1,
		Hybrid = 2,
	}

	/// <inheritdoc />
	protected internal override async Task ProcessBatchCoreAsync(
		MessageBatch batch,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken)
	{
		// Analyze batch characteristics
		var batchAnalysis = AnalyzeBatch(batch);

		Logger.LogDebug(
			"Adaptive processing batch: {MessageCount} messages, {OrderedPercent}% ordered, " +
			"Strategy: {Strategy}",
			batch.Count,
			batchAnalysis.OrderedMessagePercent,
			batchAnalysis.RecommendedStrategy);

		// Route to appropriate processor based on analysis
		switch (batchAnalysis.RecommendedStrategy)
		{
			case ProcessingStrategy.Parallel:
				await ProcessWithParallelAsync(
					batch,
					successfulMessages,
					failedMessages,
					cancellationToken).ConfigureAwait(false);
				break;

			case ProcessingStrategy.Ordered:
				await ProcessWithOrderedAsync(
					batch,
					successfulMessages,
					failedMessages,
					cancellationToken).ConfigureAwait(false);
				break;

			case ProcessingStrategy.Hybrid:
				await ProcessHybridAsync(
					batch,
					batchAnalysis,
					successfulMessages,
					failedMessages,
					cancellationToken).ConfigureAwait(false);
				break;
			default:
				break;
		}

		// Update adaptive metrics
		_metrics.RecordBatchResult(
			batch.Count,
			successfulMessages.Count,
			failedMessages.Count,
			batchAnalysis);
	}

	/// <inheritdoc />
	protected override Task<object> ProcessMessageCoreAsync(ReceivedMessage message, CancellationToken cancellationToken) =>
		_messageProcessor(message, cancellationToken);

	/// <inheritdoc />
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_orderedProcessor.Dispose();
			_parallelProcessor.Dispose();
		}

		base.Dispose(disposing);
	}

	/// <summary>
	/// Analyzes batch characteristics to determine optimal processing strategy.
	/// </summary>
	private BatchAnalysis AnalyzeBatch(MessageBatch batch)
	{
		var orderedMessages = batch.Messages.Count(static m => !string.IsNullOrEmpty(m.Message.OrderingKey));
		var orderedPercent = batch.Count > 0 ? (double)orderedMessages / batch.Count * 100 : 0;

		// Determine message size distribution
		var avgMessageSize = batch.TotalSizeBytes / Math.Max(1, batch.Count);
		var hasLargeMessages = avgMessageSize > 1024 * 100; // 100KB

		// Get historical performance metrics
		var recentPerformance = _metrics.GetRecentPerformance();

		// Determine strategy based on characteristics and performance
		ProcessingStrategy strategy;

		if (orderedPercent > 80)
		{
			// Mostly ordered messages
			strategy = ProcessingStrategy.Ordered;
		}
		else if (orderedPercent < 20)
		{
			// Mostly unordered messages
			strategy = ProcessingStrategy.Parallel;
		}
		else
		{
			// Mixed batch - use hybrid approach
			strategy = ProcessingStrategy.Hybrid;
		}

		// Adjust based on performance history
		if (recentPerformance.ParallelSuccessRate < 0.9 && strategy == ProcessingStrategy.Parallel)
		{
			// Switch to ordered if parallel is having issues
			strategy = ProcessingStrategy.Ordered;
		}

		return new BatchAnalysis
		{
			OrderedMessageCount = orderedMessages,
			OrderedMessagePercent = orderedPercent,
			AverageMessageSize = avgMessageSize,
			HasLargeMessages = hasLargeMessages,
			RecommendedStrategy = strategy,
		};
	}

	/// <summary>
	/// Processes batch using parallel strategy.
	/// </summary>
	private Task ProcessWithParallelAsync(
		MessageBatch batch,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken) =>
		_parallelProcessor.ProcessBatchCoreAsync(
			batch,
			successfulMessages,
			failedMessages,
			cancellationToken);

	/// <summary>
	/// Processes batch using ordered strategy.
	/// </summary>
	private Task ProcessWithOrderedAsync(
		MessageBatch batch,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken) =>
		_orderedProcessor.ProcessBatchCoreAsync(
			batch,
			successfulMessages,
			failedMessages,
			cancellationToken);

	/// <summary>
	/// Processes batch using hybrid strategy (split between ordered and parallel).
	/// </summary>
	private async Task ProcessHybridAsync(
		MessageBatch batch,
		BatchAnalysis _,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken)
	{
		// Split messages
		var orderedMessages = batch.Messages.Where(static m => !string.IsNullOrEmpty(m.Message.OrderingKey)).ToList();
		var unorderedMessages = batch.Messages.Where(static m => string.IsNullOrEmpty(m.Message.OrderingKey)).ToList();

		// Process both groups concurrently
		var tasks = new List<Task>();

		if (orderedMessages.Count != 0)
		{
			var orderedBatch = new MessageBatch(
				orderedMessages,
				batch.SubscriptionName,
				orderedMessages.Sum(static m => m.Message.Data.Length),
				batch.Metadata);

			tasks.Add(_orderedProcessor.ProcessBatchCoreAsync(
				orderedBatch,
				successfulMessages,
				failedMessages,
				cancellationToken));
		}

		if (unorderedMessages.Count != 0)
		{
			var unorderedBatch = new MessageBatch(
				unorderedMessages,
				batch.SubscriptionName,
				unorderedMessages.Sum(static m => m.Message.Data.Length),
				batch.Metadata);

			tasks.Add(_parallelProcessor.ProcessBatchCoreAsync(
				unorderedBatch,
				successfulMessages,
				failedMessages,
				cancellationToken));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Batch analysis results.
	/// </summary>
	private sealed class BatchAnalysis
	{
		public int OrderedMessageCount { get; set; }

		public double OrderedMessagePercent { get; set; }

		public double AverageMessageSize { get; set; }

		public bool HasLargeMessages { get; set; }

		public ProcessingStrategy RecommendedStrategy { get; set; }
	}

	/// <summary>
	/// Tracks adaptive processing metrics.
	/// </summary>
	private sealed class AdaptiveMetrics
	{
		private const int MaxHistorySize = 100;
#if NET9_0_OR_GREATER

		private readonly Lock _lock = new();

#else
		private readonly object _lock = new();

#endif
		private readonly Queue<BatchMetric> _recentBatches = new();

		public void RecordBatchResult(
			int totalMessages,
			int successfulMessages,
			int failedMessages,
			BatchAnalysis analysis)
		{
			lock (_lock)
			{
				_recentBatches.Enqueue(new BatchMetric
				{
					Timestamp = DateTimeOffset.UtcNow,
					TotalMessages = totalMessages,
					SuccessfulMessages = successfulMessages,
					FailedMessages = failedMessages,
					Strategy = analysis.RecommendedStrategy,
				});

				// Maintain history size
				while (_recentBatches.Count > MaxHistorySize)
				{
					_ = _recentBatches.Dequeue();
				}
			}
		}

		public PerformanceMetrics GetRecentPerformance()
		{
			lock (_lock)
			{
				if (_recentBatches.Count == 0)
				{
					return new PerformanceMetrics { ParallelSuccessRate = 1.0, OrderedSuccessRate = 1.0 };
				}

				var recentBatchesList = _recentBatches.ToList();
				var parallelBatches = recentBatchesList.Where(static b => b.Strategy == ProcessingStrategy.Parallel).ToList();
				var orderedBatches = recentBatchesList.Where(static b => b.Strategy == ProcessingStrategy.Ordered).ToList();

				return new PerformanceMetrics
				{
					ParallelSuccessRate = CalculateSuccessRate(parallelBatches),
					OrderedSuccessRate = CalculateSuccessRate(orderedBatches),
				};
			}
		}

		private static double CalculateSuccessRate(List<BatchMetric> batches)
		{
			if (batches.Count == 0)
			{
				return 1.0;
			}

			var totalMessages = batches.Sum(static b => b.TotalMessages);
			var successfulMessages = batches.Sum(static b => b.SuccessfulMessages);

			return totalMessages > 0 ? (double)successfulMessages / totalMessages : 1.0;
		}

		private sealed class BatchMetric
		{
			public DateTimeOffset Timestamp { get; set; }

			public int TotalMessages { get; set; }

			public int SuccessfulMessages { get; set; }

			public int FailedMessages { get; set; }

			public ProcessingStrategy Strategy { get; set; }
		}
	}

	/// <summary>
	/// Performance metrics for adaptive decision making.
	/// </summary>
	private sealed class PerformanceMetrics
	{
		public double ParallelSuccessRate { get; set; }

		public double OrderedSuccessRate { get; set; }
	}
}
