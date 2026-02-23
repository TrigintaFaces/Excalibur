// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Diagnostics;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// High-performance batch receiver for Google Pub/Sub with adaptive batching and flow control integration.
/// </summary>
public sealed partial class PubSubBatchReceiver : IBatchReceiver, IDisposable
{
	private readonly SubscriberServiceApiClient _subscriberClient;
	private readonly PubSubFlowController _flowController;
	private readonly IOptions<BatchConfiguration> _options;
	private readonly ILogger<PubSubBatchReceiver> _logger;
	private readonly SemaphoreSlim _concurrencyLimiter;
	private readonly BatchMetricsCollector _metricsCollector;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubBatchReceiver" /> class.
	/// </summary>
	public PubSubBatchReceiver(
		SubscriberServiceApiClient subscriberClient,
		PubSubFlowController flowController,
		IOptions<BatchConfiguration> options,
		ILogger<PubSubBatchReceiver> logger,
		BatchMetricsCollector metricsCollector)
	{
		_subscriberClient = subscriberClient ?? throw new ArgumentNullException(nameof(subscriberClient));
		_flowController = flowController ?? throw new ArgumentNullException(nameof(flowController));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

		_options.Value.Validate();

		_concurrencyLimiter = new SemaphoreSlim(
			_options.Value.ConcurrentBatchProcessors,
			_options.Value.ConcurrentBatchProcessors);
	}

	/// <inheritdoc />
	public async Task<MessageBatch> ReceiveBatchAsync(
		SubscriptionName subscriptionName,
		int maxMessages,
		CancellationToken cancellationToken)
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity("ReceiveBatch");
		_ = activity?.SetTag("subscription", subscriptionName.ToString());
		_ = activity?.SetTag("requested_messages", maxMessages);

		await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var config = _options.Value;
			var effectiveMaxMessages = Math.Min(maxMessages, config.MaxMessagesPerBatch);

			// Apply flow control
			var flowControlledSize = await _flowController.GetAllowedBatchSizeAsync(
				effectiveMaxMessages,
				cancellationToken).ConfigureAwait(false);

			if (flowControlledSize <= 0)
			{
				LogFlowControlPrevented();
				return new MessageBatch(
					[],
					subscriptionName.ToString(),
					0,
					new BatchMetadata { FlowControlApplied = true });
			}

			var stopwatch = ValueStopwatch.StartNew();

			var pullRequest = new PullRequest
			{
				SubscriptionAsSubscriptionName = subscriptionName,
				MaxMessages = flowControlledSize,

				// ReturnImmediately is obsolete and false is the default behavior
			};

			var response = await _subscriberClient.PullAsync(
				pullRequest,
				cancellationToken).ConfigureAwait(false);

			var messages = response.ReceivedMessages.ToList();
			var totalSize = messages.Sum(static m => m.Message.Data.Length);

			var metadata = new BatchMetadata
			{
				PullDurationMs = stopwatch.Elapsed.TotalMilliseconds,
				FlowControlApplied = flowControlledSize < effectiveMaxMessages,
				EffectiveBatchSize = flowControlledSize,
			};

			_ = activity?.SetTag("received_messages", messages.Count);
			_ = activity?.SetTag("total_bytes", totalSize);
			_ = activity?.SetTag("pull_duration_ms", metadata.PullDurationMs);

			_metricsCollector.RecordBatchReceived(messages.Count, totalSize, metadata.PullDurationMs);

			// Update flow control metrics
			await _flowController.RecordBatchReceivedAsync(messages.Count, cancellationToken).ConfigureAwait(false);

			return new MessageBatch(messages, subscriptionName.ToString(), totalSize, metadata);
		}
		finally
		{
			_ = _concurrencyLimiter.Release();
		}
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<MessageBatch> ReceiveBatchesAsync(
		SubscriptionName subscriptionName,
		int batchCount,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var config = _options.Value;
		var adaptiveSizer = config.EnableAdaptiveBatching
			? new AdaptiveBatchSizer(config)
			: null;

		for (var i = 0; i < batchCount && !cancellationToken.IsCancellationRequested; i++)
		{
			var batchSize = adaptiveSizer?.GetNextBatchSize() ?? config.MaxMessagesPerBatch;

			var batch = await ReceiveBatchAsync(
				subscriptionName,
				batchSize,
				cancellationToken).ConfigureAwait(false);

			adaptiveSizer?.RecordBatchResult(
				batch.Count,
				batch.Metadata.PullDurationMs);

			yield return batch;

			// Add a small delay between batches to prevent overwhelming the system
			if (i < batchCount - 1)
			{
				await Task.Delay(10, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc />
	public async Task AcknowledgeBatchAsync(
		SubscriptionName subscriptionName,
		IEnumerable<string> ackIds,
		CancellationToken cancellationToken)
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity("AcknowledgeBatch");

		var ackIdsList = ackIds.ToList();
		_ = activity?.SetTag("subscription", subscriptionName.ToString());
		_ = activity?.SetTag("ack_count", ackIdsList.Count);

		if (ackIdsList.Count == 0)
		{
			return;
		}

		try
		{
			var stopwatch = ValueStopwatch.StartNew();

			await _subscriberClient.AcknowledgeAsync(
				subscriptionName,
				ackIdsList,
				cancellationToken).ConfigureAwait(false);

			_metricsCollector.RecordBatchAcknowledged(
				ackIdsList.Count,
				stopwatch.Elapsed.TotalMilliseconds);

			LogBatchAcknowledged(ackIdsList.Count, stopwatch.Elapsed.TotalMilliseconds);
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
		{
			LogAcknowledgmentsFailed(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task ModifyAckDeadlineBatchAsync(
		SubscriptionName subscriptionName,
		IEnumerable<string> ackIds,
		int ackDeadlineSeconds,
		CancellationToken cancellationToken)
	{
		using var activity = GooglePubSubTelemetryConstants.SharedActivitySource.StartActivity("ModifyAckDeadlineBatch");

		var ackIdsList = ackIds.ToList();
		_ = activity?.SetTag("subscription", subscriptionName.ToString());
		_ = activity?.SetTag("ack_count", ackIdsList.Count);
		_ = activity?.SetTag("deadline_seconds", ackDeadlineSeconds);

		if (ackIdsList.Count == 0)
		{
			return;
		}

		await _subscriberClient.ModifyAckDeadlineAsync(
			subscriptionName,
			ackIdsList,
			ackDeadlineSeconds,
			cancellationToken).ConfigureAwait(false);

		LogAckDeadlineModified(ackIdsList.Count, ackDeadlineSeconds);
	}

	/// <summary>
	/// Adaptive batch sizer that adjusts batch size based on processing performance.
	/// </summary>
	private sealed class AdaptiveBatchSizer(BatchConfiguration config)
	{
#if NET9_0_OR_GREATER

		private readonly Lock _lock = new();

#else

		private readonly object _lock = new();

#endif
		private int _currentBatchSize = config.MaxMessagesPerBatch / 2;
		private double _avgProcessingTime;
		private int _sampleCount;

		public int GetNextBatchSize()
		{
			lock (_lock)
			{
				return _currentBatchSize;
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
			Justification = "actualSize reserved for future batch efficiency metrics")]
		public void RecordBatchResult(int actualSize, double processingTimeMs)
		{
			lock (_lock)
			{
				// Update moving average
				_avgProcessingTime = ((_avgProcessingTime * _sampleCount) + processingTimeMs) / (_sampleCount + 1);
				_sampleCount++;

				// Adjust batch size every 10 samples
				if (_sampleCount % 10 == 0)
				{
					var targetMs = config.TargetBatchProcessingTime.TotalMilliseconds;

					if (_avgProcessingTime < targetMs * 0.8)
					{
						// Processing is fast, increase batch size
						_currentBatchSize = Math.Min(
							(int)(_currentBatchSize * 1.2),
							config.MaxMessagesPerBatch);
					}
					else if (_avgProcessingTime > targetMs * 1.2)
					{
						// Processing is slow, decrease batch size
						_currentBatchSize = Math.Max(
							(int)(_currentBatchSize * 0.8),
							config.MinMessagesPerBatch);
					}

					// Reset sample count to prevent overflow
					if (_sampleCount > 1000)
					{
						_sampleCount = 10;
					}
				}
			}
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// Shared static ActivitySource is process-lifetime and not disposed here.
		_concurrencyLimiter.Dispose();
		GC.SuppressFinalize(this);
	}

	// Source-generated logging methods (Sprint 363 - EventId Migration)
	[LoggerMessage(GooglePubSubEventId.FlowControlPreventedReceive, LogLevel.Warning,
		"Flow control prevented batch receive")]
	private partial void LogFlowControlPrevented();

	[LoggerMessage(GooglePubSubEventId.BatchAcknowledged, LogLevel.Debug,
		"Acknowledged {Count} messages in {Duration}ms")]
	private partial void LogBatchAcknowledged(int count, double duration);

	[LoggerMessage(GooglePubSubEventId.BatchAcknowledgmentsFailed, LogLevel.Warning,
		"Some acknowledgments failed, likely due to expired deadlines")]
	private partial void LogAcknowledgmentsFailed(Exception ex);

	[LoggerMessage(GooglePubSubEventId.BatchAckDeadlineModified, LogLevel.Debug,
		"Modified ack deadline for {Count} messages to {Deadline}s")]
	private partial void LogAckDeadlineModified(int count, int deadline);
}
