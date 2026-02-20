// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Base implementation of batch processor with common functionality.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="BatchProcessorBase" /> class. </remarks>
public abstract class BatchProcessorBase(ILogger logger, BatchMetricsCollector metricsCollector) : IBatchProcessor, IDisposable
{
	private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Extensions.Google.BatchProcessing");
	private readonly BatchMetricsCollector _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
	private volatile bool _disposed;

	/// <summary>
	/// Gets the logger instance.
	/// </summary>
	protected ILogger Logger => _logger;

	/// <summary>
	/// Gets the activity source for tracing.
	/// </summary>
	protected ActivitySource ActivitySource => _activitySource;

	/// <summary>
	/// Gets the metrics collector.
	/// </summary>
	protected BatchMetricsCollector MetricsCollector => _metricsCollector;

	/// <inheritdoc />
	public async Task<BatchProcessingResult> ProcessAsync(
		MessageBatch batch,
		CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("ProcessBatch");
		_ = activity?.SetTag("batch.size", batch.Count);
		_ = activity?.SetTag("batch.bytes", batch.TotalSizeBytes);

		var stopwatch = Stopwatch.StartNew();
		var successfulMessages = new List<ProcessedMessage>();
		var failedMessages = new List<FailedMessage>();

		try
		{
			MetricsCollector.IncrementActiveProcessors();

			await ProcessBatchCoreAsync(
				batch,
				successfulMessages,
				failedMessages,
				cancellationToken).ConfigureAwait(false);

			stopwatch.Stop();

			var result = new BatchProcessingResult(
				batch,
				successfulMessages,
				failedMessages,
				stopwatch.Elapsed);

			_ = activity?.SetTag("batch.success_count", successfulMessages.Count);
			_ = activity?.SetTag("batch.failure_count", failedMessages.Count);
			_ = activity?.SetTag("batch.duration_ms", stopwatch.Elapsed.TotalMilliseconds);

			Logger.LogInformation(
				"Processed batch of {Count} messages: {Success} successful, {Failed} failed in {Duration}ms",
				batch.Count,
				successfulMessages.Count,
				failedMessages.Count,
				stopwatch.Elapsed.TotalMilliseconds);

			return result;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Unexpected error processing batch");
			_ = activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
		finally
		{
			MetricsCollector.DecrementActiveProcessors();
		}
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<BatchProcessingResult> ProcessMultipleAsync(
		IAsyncEnumerable<MessageBatch> batches,
		int maxConcurrency,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var channel = Channel.CreateUnbounded<MessageBatch>();
		var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

		try
		{
			// Producer task
			var producerTask = Task.Run(
				async () =>
				{
					try
					{
						await foreach (var batch in batches.WithCancellation(cancellationToken).ConfigureAwait(false))
						{
							await channel.Writer.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
						}
					}
					finally
					{
						channel.Writer.Complete();
					}
				}, cancellationToken);

			// Process batches concurrently
			var tasks = new List<Task<BatchProcessingResult>>();

			await foreach (var batch in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false).ConfigureAwait(false))
			{
				await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

				var task = ProcessBatchWithReleaseAsync(batch, semaphore, cancellationToken);
				tasks.Add(task);

				// Yield completed results
				var completed = tasks.Where(t => t.IsCompleted).ToList();
				foreach (var completedTask in completed)
				{
					_ = tasks.Remove(completedTask);
					yield return await completedTask.ConfigureAwait(false);
				}
			}

			// Wait for remaining tasks
			await producerTask.ConfigureAwait(false);

			foreach (var result in await Task.WhenAll(tasks).ConfigureAwait(false))
			{
				yield return result;
			}
		}
		finally
		{
			semaphore.Dispose();
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="BatchProcessorBase"/> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_activitySource.Dispose();
			_metricsCollector.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	/// Core batch processing logic to be implemented by derived classes.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	protected internal abstract Task ProcessBatchCoreAsync(
		MessageBatch batch,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken);

	/// <summary>
	/// Processes a single message from the batch.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	protected async Task ProcessMessageAsync(
		ReceivedMessage message,
		List<ProcessedMessage> successfulMessages,
		List<FailedMessage> failedMessages,
		CancellationToken cancellationToken)
	{
		var messageStopwatch = Stopwatch.StartNew();

		try
		{
			var result = await ProcessMessageCoreAsync(message, cancellationToken).ConfigureAwait(false);

			successfulMessages.Add(new ProcessedMessage(
				message.Message.MessageId,
				message.AckId,
				result,
				messageStopwatch.Elapsed));
		}
		catch (Exception ex)
		{
			var shouldRetry = DetermineRetryPolicy(ex);

			failedMessages.Add(new FailedMessage(
				message.Message.MessageId,
				message.AckId,
				ex,
				shouldRetry,
				shouldRetry ? GetRetryDelay(ex) : null));

			Logger.LogError(
				ex,
				"Failed to process message {MessageId}. Retry: {ShouldRetry}",
				message.Message.MessageId,
				shouldRetry);
		}
	}

	/// <summary>
	/// Processes a single message. To be implemented by derived classes.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	protected abstract Task<object> ProcessMessageCoreAsync(ReceivedMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Determines if a failed message should be retried.
	/// </summary>
	protected virtual bool DetermineRetryPolicy(Exception exception) =>

		// Default policy: retry on transient errors
		exception is TimeoutException ||
		exception is OperationCanceledException ||
		(exception is InvalidOperationException &&
		 exception.Message.Contains("transient", StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Gets the retry delay for a failed message.
	/// </summary>
	protected virtual TimeSpan GetRetryDelay(Exception exception) =>

		// Default exponential backoff
		TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, 1)));

	private async Task<BatchProcessingResult> ProcessBatchWithReleaseAsync(
		MessageBatch batch,
		SemaphoreSlim semaphore,
		CancellationToken cancellationToken)
	{
		try
		{
			return await ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_ = semaphore.Release();
		}
	}
}
