// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;

using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Delivery.BatchProcessing;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Responsible for consuming CDC change events from the processing channel
/// and applying them via the user-provided event handler (consumer role).
/// </summary>
/// <remarks>
/// Extracted from <see cref="CdcProcessor"/> to separate the change application
/// concern from change detection and checkpoint management.
/// </remarks>
internal sealed partial class CdcChangeApplier
{
	private readonly IDatabaseConfig _dbConfig;
	private readonly IDataAccessPolicyFactory _policyFactory;
	private readonly CdcCheckpointManager _checkpointManager;
	private readonly Domain.OrderedEventProcessor _orderedEventProcessor;
	private readonly ILogger _logger;
	private readonly CdcFatalErrorHandler? _onFatalError;

	private static readonly Counter<long> EventsProcessedCounter = CdcTelemetryConstants.Meter.CreateCounter<long>(
		CdcTelemetryConstants.MetricNames.EventsProcessed,
		"events",
		"Total CDC events processed successfully");

	private static readonly Counter<long> EventsFailedCounter = CdcTelemetryConstants.Meter.CreateCounter<long>(
		CdcTelemetryConstants.MetricNames.EventsFailed,
		"events",
		"Total CDC event processing failures");

	private static readonly Histogram<double> BatchDurationHistogram = CdcTelemetryConstants.Meter.CreateHistogram<double>(
		CdcTelemetryConstants.MetricNames.BatchDuration,
		"ms",
		"Duration of batch processing in milliseconds");

	private static readonly Histogram<int> BatchSizeHistogram = CdcTelemetryConstants.Meter.CreateHistogram<int>(
		CdcTelemetryConstants.MetricNames.BatchSize,
		"events",
		"Number of events in a processed batch");

	internal CdcChangeApplier(
		IDatabaseConfig dbConfig,
		IDataAccessPolicyFactory policyFactory,
		CdcCheckpointManager checkpointManager,
		Domain.OrderedEventProcessor orderedEventProcessor,
		ILogger logger,
		CdcFatalErrorHandler? onFatalError)
	{
		_dbConfig = dbConfig;
		_policyFactory = policyFactory;
		_checkpointManager = checkpointManager;
		_orderedEventProcessor = orderedEventProcessor;
		_logger = logger;
		_onFatalError = onFatalError;
	}

	/// <summary>
	/// Runs the consumer loop that processes CDC events in batches.
	/// </summary>
	internal async Task<int> ConsumerLoopAsync(
		ChannelReader<DataChangeEvent> reader,
		Func<DataChangeEvent, CancellationToken, Task> eventHandler,
		Func<bool> isDisposed,
		Func<bool> shouldWaitForProducer,
		Func<bool> isProducerStopped,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventHandler);

		LogConsumerLoopStarted();

		var totalProcessedCount = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			if (isDisposed())
			{
				LogDisposalRequested();
				break;
			}

			if (isProducerStopped() && reader.Count == 0)
			{
				LogNoMoreRecordsConsumer();
				break;
			}

			if (shouldWaitForProducer())
			{
				LogWaitingForProducer();

				if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
				{
					break;
				}

				continue;
			}

			try
			{
				cancellationToken.ThrowIfCancellationRequested();

				LogAttemptingDequeue();

				var stopwatch = ValueStopwatch.StartNew();

				var batch = await ChannelBatchUtilities.DequeueBatchAsync(reader, _dbConfig.ConsumerBatchSize, cancellationToken)
					.ConfigureAwait(false);
				LogDequeuedMessages(batch.Length, stopwatch.Elapsed.TotalMilliseconds);
				LogProcessingBatch(batch.Length);

				await ProcessBatchAsync(batch, eventHandler, cancellationToken).ConfigureAwait(false);

				LogProcessedBatch(batch.Length, stopwatch.Elapsed.TotalMilliseconds);

				totalProcessedCount += batch.Length;
			}
			catch (OperationCanceledException)
			{
				LogConsumerCanceled();
			}
			catch (Exception ex)
			{
				LogErrorInConsumer(ex);
				throw;
			}
		}

		LogCompletedProcessing(totalProcessedCount);

		return totalProcessedCount;
	}

	private async Task ProcessBatchAsync(
		IReadOnlyList<DataChangeEvent> batch,
		Func<DataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(batch);
		ArgumentNullException.ThrowIfNull(eventHandler);

		using var batchActivity = CdcTelemetryConstants.ActivitySource.StartActivity("cdc.consume_batch");
		batchActivity?.SetTag("cdc.batch.size", batch.Count);

		var batchStopwatch = ValueStopwatch.StartNew();

		BatchSizeHistogram.Record(batch.Count);

		foreach (var changeEvent in batch)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				await _orderedEventProcessor.ProcessAsync(async () =>
						await _policyFactory.GetComprehensivePolicy().ExecuteAsync(async () =>
								await eventHandler(changeEvent, cancellationToken)
									.ConfigureAwait(false))
							.ConfigureAwait(false))
					.ConfigureAwait(false);

				EventsProcessedCounter.Add(1, new TagList
				{
					{ CdcTelemetryConstants.Tags.CaptureInstance, changeEvent.TableName },
				});
			}
			catch (Exception ex)
			{
				EventsFailedCounter.Add(1, new TagList
				{
					{ CdcTelemetryConstants.Tags.CaptureInstance, changeEvent.TableName },
					{ CdcTelemetryConstants.Tags.ErrorType, ex.GetType().Name },
				});

				LogUnhandledException(changeEvent.TableName, CdcChangeDetector.ByteArrayToHex(changeEvent.Lsn), CdcChangeDetector.ByteArrayToHex(changeEvent.SeqVal),
					ex);

				if (_onFatalError != null)
				{
					await _onFatalError(ex, changeEvent).ConfigureAwait(false);
				}
				else
				{
					throw;
				}
			}

			var updateTablePolicy = _policyFactory.GetComprehensivePolicy();
			await updateTablePolicy.ExecuteAsync(() => _checkpointManager.UpdateTableLastProcessedAsync(
				changeEvent.TableName,
				changeEvent.Lsn,
				changeEvent.SeqVal,
				changeEvent.CommitTime,
				cancellationToken)).ConfigureAwait(false);
		}
		BatchDurationHistogram.Record(batchStopwatch.Elapsed.TotalMilliseconds);
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.CdcCleanupStarted, LogLevel.Information,
		"CDC Consumer loop started...")]
	private partial void LogConsumerLoopStarted();

	[LoggerMessage(DataSqlServerEventId.CdcCleanupCompleted, LogLevel.Warning,
		"ConsumerLoop: disposal requested, exit Excalibur.Data.")]
	private partial void LogDisposalRequested();

	[LoggerMessage(DataSqlServerEventId.CdcCleanupError, LogLevel.Information,
		"No more CDC records. Consumer is exiting gracefully.")]
	private partial void LogNoMoreRecordsConsumer();

	[LoggerMessage(DataSqlServerEventId.CdcHandlerInvoked, LogLevel.Information,
		"CDC Queue is empty. Waiting for examples.AdvancedSample.Producer...")]
	private partial void LogWaitingForProducer();

	[LoggerMessage(DataSqlServerEventId.CdcHandlerError, LogLevel.Debug,
		"Attempting to dequeue CDC messages...")]
	private partial void LogAttemptingDequeue();

	[LoggerMessage(DataSqlServerEventId.CdcPartitionProcessed, LogLevel.Debug,
		"Dequeued {BatchSize} messages in {ElapsedMs}ms")]
	private partial void LogDequeuedMessages(int batchSize, double elapsedMs);

	[LoggerMessage(DataSqlServerEventId.CdcPartitionError, LogLevel.Debug,
		"Processing batch of {BatchSize} CDC records")]
	private partial void LogProcessingBatch(int batchSize);

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointCreated, LogLevel.Debug,
		"Processed {BatchSize} CDC records in {ElapsedMs}ms")]
	private partial void LogProcessedBatch(int batchSize, double elapsedMs);

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointRestored, LogLevel.Debug,
		"Consumer canceled")]
	private partial void LogConsumerCanceled();

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointError, LogLevel.Error,
		"Error in ConsumerLoop")]
	private partial void LogErrorInConsumer(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcRetryAttempted, LogLevel.Information,
		"Completed CDC processing, total events processed: {TotalEvents}")]
	private partial void LogCompletedProcessing(int totalEvents);

	[LoggerMessage(DataSqlServerEventId.CdcMaxRetriesExceeded, LogLevel.Critical,
		"Unhandled exception occurred while processing change event for table '{TableName}', LSN {Lsn}, SeqVal {SeqVal}.")]
	private partial void LogUnhandledException(string tableName, string lsn, string seqVal, Exception ex);
}
