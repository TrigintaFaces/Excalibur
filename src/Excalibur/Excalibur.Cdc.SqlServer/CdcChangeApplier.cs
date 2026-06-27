// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;

using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch.Delivery.BatchProcessing;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Cdc.SqlServer;

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
	private readonly IDatabaseOptions _dbConfig;
	private readonly IDataAccessPolicyFactory _policyFactory;
	private readonly CdcCheckpointManager _checkpointManager;
	private readonly Domain.OrderedEventProcessor _orderedEventProcessor;
	private readonly ILogger _logger;
	private readonly CdcFatalErrorHandler<DataChangeEvent>? _onFatalError;
	private readonly ICdcIdempotencyFilter? _idempotencyFilter;

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
		IDatabaseOptions dbConfig,
		IDataAccessPolicyFactory policyFactory,
		CdcCheckpointManager checkpointManager,
		Domain.OrderedEventProcessor orderedEventProcessor,
		ILogger logger,
		CdcFatalErrorHandler<DataChangeEvent>? onFatalError,
		ICdcIdempotencyFilter? idempotencyFilter = null)
	{
		_dbConfig = dbConfig;
		_policyFactory = policyFactory;
		_checkpointManager = checkpointManager;
		_orderedEventProcessor = orderedEventProcessor;
		_logger = logger;
		_onFatalError = onFatalError;
		_idempotencyFilter = idempotencyFilter;
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
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
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

		// Track the last successfully processed event per table so we can write
		// a single checkpoint per table after the batch completes (instead of per-event).
		var lastSuccessfulPerTable = new Dictionary<string, DataChangeEvent>(StringComparer.Ordinal);

		// Tables that had a swallowed (onFatalError) failure in this batch. Once a table is here,
		// NO later event for it — success OR idempotency-skip — may advance its checkpoint. Without
		// this, a later same-table event re-adds the table to lastSuccessfulPerTable and the post-batch
		// checkpoint advances PAST the failed change, permanently skipping it (the subtle re-add bug).
		var failedTables = new HashSet<string>(StringComparer.Ordinal);

		// Resolve the Polly policy once per batch instead of per-event.
		// The policy configuration does not change within a processing cycle.
		var batchPolicy = _policyFactory.GetComprehensivePolicy();

		foreach (var changeEvent in batch)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Idempotency check: skip events already processed in a prior cycle.
			if (_idempotencyFilter is not null
				&& await _idempotencyFilter.IsProcessedAsync(
					changeEvent.TableName, changeEvent.Lsn, changeEvent.SeqVal, cancellationToken)
					.ConfigureAwait(false))
			{
				LogIdempotencyEventSkipped(changeEvent.TableName,
					CdcChangeDetector.ByteArrayToHex(changeEvent.Lsn),
					CdcChangeDetector.ByteArrayToHex(changeEvent.SeqVal));

				// Treat as successful for checkpoint advancement purposes -- but ONLY if this table
				// has not already had a swallowed failure in this batch. Advancing here would move the
				// checkpoint past the earlier failed change, permanently skipping it.
				if (!failedTables.Contains(changeEvent.TableName))
				{
					lastSuccessfulPerTable[changeEvent.TableName] = changeEvent;
				}

				continue;
			}

			var eventSucceeded = false;
			try
			{
				await _orderedEventProcessor.ProcessAsync(async () =>
						await batchPolicy.ExecuteAsync(async () =>
								await eventHandler(changeEvent, cancellationToken)
									.ConfigureAwait(false))
							.ConfigureAwait(false), cancellationToken)
					.ConfigureAwait(false);

				eventSucceeded = true;

				// Mark event as processed for idempotency tracking.
				if (_idempotencyFilter is not null)
				{
					await _idempotencyFilter.MarkProcessedAsync(
						changeEvent.TableName, changeEvent.Lsn, changeEvent.SeqVal, cancellationToken)
						.ConfigureAwait(false);
				}

				EventsProcessedCounter.Add(1, new TagList
				{
					{ CdcTelemetryConstants.TagNames.CaptureInstance, changeEvent.TableName },
				});
			}
			catch (Exception ex)
			{
				EventsFailedCounter.Add(1, new TagList
				{
					{ CdcTelemetryConstants.TagNames.CaptureInstance, changeEvent.TableName },
					{ CdcTelemetryConstants.TagNames.ErrorType, ex.GetType().Name },
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

			// Only track checkpoint for successful events.
			// When onFatalError swallows an exception, the checkpoint must NOT advance
			// past the failed event -- otherwise that event is permanently skipped.
			if (eventSucceeded)
			{
				// Do NOT re-advance a table that already had a swallowed failure in this batch.
				// A later same-table success must not move the checkpoint past the failed change
				// (it would permanently skip it); the failed change must be reprocessed next cycle.
				if (!failedTables.Contains(changeEvent.TableName))
				{
					lastSuccessfulPerTable[changeEvent.TableName] = changeEvent;
				}
			}
			else
			{
				// CRITICAL: Freeze this table's checkpoint at the pre-failure position.
				// Mark the table failed (so no later event -- success OR idempotency-skip -- can
				// re-add it) and remove any position already tracked for it this batch. On the next
				// cycle, processing resumes from the previous cycle's checkpoint, ensuring the failed
				// event is reprocessed.
				_ = failedTables.Add(changeEvent.TableName);
				_ = lastSuccessfulPerTable.Remove(changeEvent.TableName);

				LogCheckpointSkippedForFailedEvent(changeEvent.TableName,
					CdcChangeDetector.ByteArrayToHex(changeEvent.Lsn),
					CdcChangeDetector.ByteArrayToHex(changeEvent.SeqVal));
			}
		}

		// Write a single checkpoint per table after the entire batch completes.
		// This reduces checkpoint I/O from O(batch_size) to O(tracked_tables).
		foreach (var (tableName, lastEvent) in lastSuccessfulPerTable)
		{
			await batchPolicy.ExecuteAsync(() => _checkpointManager.UpdateTableLastProcessedAsync(
				tableName,
				lastEvent.Lsn,
				lastEvent.SeqVal,
				lastEvent.CommitTime,
				cancellationToken)).ConfigureAwait(false);
		}

		BatchDurationHistogram.Record(batchStopwatch.Elapsed.TotalMilliseconds);
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.CdcConsumerStarted, LogLevel.Information,
		"CDC Consumer loop started...")]
	private partial void LogConsumerLoopStarted();

	[LoggerMessage(DataSqlServerEventId.CdcConsumerDisposalRequested, LogLevel.Warning,
		"ConsumerLoop: disposal requested, exit Excalibur.Data.")]
	private partial void LogDisposalRequested();

	[LoggerMessage(DataSqlServerEventId.CdcConsumerNoMoreRecords, LogLevel.Information,
		"No more CDC records. Consumer is exiting gracefully.")]
	private partial void LogNoMoreRecordsConsumer();

	[LoggerMessage(DataSqlServerEventId.CdcConsumerWaitingForProducer, LogLevel.Information,
		"CDC Queue is empty. Waiting for producer...")]
	private partial void LogWaitingForProducer();

	[LoggerMessage(DataSqlServerEventId.CdcConsumerDequeueAttempt, LogLevel.Debug,
		"Attempting to dequeue CDC messages...")]
	private partial void LogAttemptingDequeue();

	[LoggerMessage(DataSqlServerEventId.CdcConsumerDequeued, LogLevel.Debug,
		"Dequeued {BatchSize} messages in {ElapsedMs}ms")]
	private partial void LogDequeuedMessages(int batchSize, double elapsedMs);

	[LoggerMessage(DataSqlServerEventId.CdcConsumerProcessingBatch, LogLevel.Debug,
		"Processing batch of {BatchSize} CDC records")]
	private partial void LogProcessingBatch(int batchSize);

	[LoggerMessage(DataSqlServerEventId.CdcConsumerProcessedBatch, LogLevel.Debug,
		"Processed {BatchSize} CDC records in {ElapsedMs}ms")]
	private partial void LogProcessedBatch(int batchSize, double elapsedMs);

	[LoggerMessage(DataSqlServerEventId.CdcConsumerCanceled, LogLevel.Debug,
		"Consumer canceled")]
	private partial void LogConsumerCanceled();

	[LoggerMessage(DataSqlServerEventId.CdcConsumerError, LogLevel.Error,
		"Error in ConsumerLoop")]
	private partial void LogErrorInConsumer(Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcConsumerCompleted, LogLevel.Information,
		"Completed CDC processing, total events processed: {TotalEvents}")]
	private partial void LogCompletedProcessing(int totalEvents);

	[LoggerMessage(DataSqlServerEventId.CdcConsumerUnhandledException, LogLevel.Critical,
		"Unhandled exception occurred while processing change event for table '{TableName}', LSN {Lsn}, SeqVal {SeqVal}.")]
	private partial void LogUnhandledException(string tableName, string lsn, string seqVal, Exception ex);

	[LoggerMessage(DataSqlServerEventId.CdcCheckpointSkipped, LogLevel.Warning,
		"Checkpoint NOT advanced for failed event on table '{TableName}', LSN {Lsn}, SeqVal {SeqVal}. Event will be reprocessed on next cycle.")]
	private partial void LogCheckpointSkippedForFailedEvent(string tableName, string lsn, string seqVal);

	[LoggerMessage(DataSqlServerEventId.CdcIdempotencyEventSkipped, LogLevel.Debug,
		"CDC event skipped by idempotency filter: table={TableName}, LSN={Lsn}, SeqVal={SeqVal}")]
	private partial void LogIdempotencyEventSkipped(string tableName, string lsn, string seqVal);
}
