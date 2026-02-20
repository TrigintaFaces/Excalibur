// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Options.Channels;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// High-throughput AWS SQS message receiver using System.Threading.Channels.
/// </summary>
public sealed partial class AwsSqsChannelReceiver : IAsyncDisposable
{
	private readonly IAmazonSQS _sqsClient;
	private readonly AwsSqsOptions _sqsOptions;
	private readonly IServiceProvider _serviceProvider;
	private readonly string _queueUrl;
	private readonly int _maxNumberOfMessages;

	/// <summary>
	/// Batch delete infrastructure.
	/// </summary>
	private readonly ConcurrentQueue<DeleteMessageBatchRequestEntry> _pendingDeletes = new();

	private readonly Timer _batchDeleteTimer;
	private readonly SemaphoreSlim _batchDeleteSemaphore = new(1, 1);
	private readonly int _batchDeleteIntervalMs;

	/// <summary>
	/// SQS limit.
	/// </summary>
	private readonly int _maxBatchSize = 10;

	/// <summary>
	/// Long polling wait time in seconds (0-20, where 20 is max long polling).
	/// </summary>
	private readonly int _waitTimeSeconds;

	/// <summary>
	/// Visibility timeout in seconds for received messages.
	/// </summary>
	private readonly int _visibilityTimeoutSeconds;

	private readonly ILogger<AwsSqsChannelReceiver>? _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsChannelReceiver" /> class.
	/// </summary>
	/// <param name="sqsClient"> The AWS SQS client. </param>
	/// <param name="sqsOptions"> SQS configuration options. </param>
	/// <param name="serviceProvider"> The service provider for dependency injection. </param>
	/// <param name="memoryPool"> Memory pool for zero-copy operations. </param>
	/// <param name="channelOptions"> Channel configuration options. </param>
	/// <param name="logger"> Optional logger. </param>
	public AwsSqsChannelReceiver(
		IAmazonSQS sqsClient,
		AwsSqsOptions sqsOptions,
		IServiceProvider serviceProvider,
		MemoryPool<byte>? memoryPool = null,
		ChannelMessagePumpOptions? channelOptions = null,
		ILogger<AwsSqsChannelReceiver>? logger = null)
	{
		_ = memoryPool;
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		_sqsOptions = sqsOptions ?? throw new ArgumentNullException(nameof(sqsOptions));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger;

		if (_sqsOptions.QueueUrl == null)
		{
			throw new ArgumentException("QueueUrl must be configured", nameof(sqsOptions));
		}

		_queueUrl = _sqsOptions.QueueUrl.ToString();

		// Configure SQS-specific settings
		_maxNumberOfMessages = channelOptions?.BatchSize > 0
			? Math.Min(channelOptions.BatchSize, 10) // SQS max is 10
			: 10;

		// Wire long polling wait time from options (0-20 seconds, default 20 for max long polling)
		_waitTimeSeconds = (int)Math.Clamp(sqsOptions.WaitTimeSeconds.TotalSeconds, 0, 20);

		// Wire visibility timeout from options (used for ChangeMessageVisibilityAsync)
		_visibilityTimeoutSeconds = (int)Math.Clamp(sqsOptions.VisibilityTimeout.TotalSeconds, 0, 43200);

		// Batch delete interval (default 100ms, configurable via BatchTimeoutMs)
		_batchDeleteIntervalMs = channelOptions?.BatchTimeoutMs > 0 ? channelOptions.BatchTimeoutMs : 100;

		// Initialize batch delete timer
		_batchDeleteTimer = new Timer(ProcessBatchDeletes, state: null, Timeout.Infinite, Timeout.Infinite);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await OnStoppingAsync(CancellationToken.None).ConfigureAwait(false);
		await _batchDeleteTimer.DisposeAsync().ConfigureAwait(false);
		_batchDeleteSemaphore?.Dispose();
	}

	private static string GetQueueName(Uri? queueUrl)
	{
		if (queueUrl == null)
		{
			return "Unknown";
		}

		// Extract queue name from URL (last segment)
		var segments = queueUrl.Segments;
		return segments.Length > 0 ? segments[^1].TrimEnd('/') : queueUrl.ToString();
	}

	/// <summary>
	/// Creates a high-performance DateTimeOffset timestamp using ValueStopwatch.
	/// </summary>
	private static DateTimeOffset CreateDateTimeOffsetTimestamp()
	{
		var perfTimestamp = ValueStopwatch.GetTimestamp();
		var elapsedTicks = (long)(perfTimestamp * 10_000_000.0 / ValueStopwatch.GetFrequency());
		var baseDateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		return new DateTimeOffset(baseDateTime.Ticks + elapsedTicks, TimeSpan.Zero);
	}

	/// <summary>
	/// Cleans up the SQS receiver when stopping.
	/// </summary>
	private async Task OnStoppingAsync(CancellationToken cancellationToken)
	{
		// Stop the timer
		_ = _batchDeleteTimer.Change(Timeout.Infinite, Timeout.Infinite);

		// Process any remaining deletes
		await FlushPendingDeletesAsync(cancellationToken).ConfigureAwait(false);
	}

	private bool TryDecodeMessageBody(Message sqsMessage, out byte[] bodyBytes)
	{
		try
		{
			return AwsSqsMessageBodyCodec.TryDecodeBody(sqsMessage, out bodyBytes, out _);
		}
		catch (Exception ex)
		{
			_logger?.LogMessageDecompressionFailed(ex);
			bodyBytes = [];
			return false;
		}
	}

	private async void ProcessBatchDeletes(object? state)
	{
		if (!await _batchDeleteSemaphore.WaitAsync(0).ConfigureAwait(false))
		{
			// Already processing
			return;
		}

		try
		{
			await ProcessBatchDeletesInternalAsync(CancellationToken.None).ConfigureAwait(false);
		}
		finally
		{
			_ = _batchDeleteSemaphore.Release();
		}
	}

	private async Task ProcessBatchDeletesInternalAsync(CancellationToken cancellationToken)
	{
		var entries = new List<DeleteMessageBatchRequestEntry>();

		// Dequeue up to max batch size
		while (entries.Count < _maxBatchSize && _pendingDeletes.TryDequeue(out var entry))
		{
			entries.Add(entry);
		}

		if (entries.Count == 0)
		{
			return;
		}

		try
		{
			var batchRequest = new DeleteMessageBatchRequest { QueueUrl = _queueUrl, Entries = entries };

			var response = await _sqsClient.DeleteMessageBatchAsync(batchRequest, cancellationToken).ConfigureAwait(false);

			_logger?.LogBatchDeleteCompleted(response.Successful.Count, response.Failed.Count);

			// Handle failures
			foreach (var failure in response.Failed)
			{
				_logger?.LogBatchDeleteFailed(failure.Id, failure.Code, failure.Message);
				// Could re-enqueue for retry or implement dead letter logic here
			}
		}
		catch (Exception ex)
		{
			_logger?.LogFailedToExecuteBatchDelete(entries.Count, ex);

			// Re-enqueue failed entries for retry
			foreach (var entry in entries)
			{
				_pendingDeletes.Enqueue(entry);
			}
		}
	}

	private async Task FlushPendingDeletesAsync(CancellationToken cancellationToken)
	{
		await _batchDeleteSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			while (!_pendingDeletes.IsEmpty)
			{
				await ProcessBatchDeletesInternalAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			_ = _batchDeleteSemaphore.Release();
		}
	}
}
