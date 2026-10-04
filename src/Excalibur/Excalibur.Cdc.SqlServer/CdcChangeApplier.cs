// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;

using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch;
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

	// Computed once from the configuration this applier is bound to. Held as a field rather than built at
	// each call so the two uses below cannot drift apart, and so the dedupe identity is derived in exactly
	// one place.
	private readonly CdcConsumerIdentity _consumer;
	private readonly IMessageFailureClassifier? _failureClassifier;

	private static readonly Counter<long> EventsProcessedCounter = CdcTelemetryConstants.Meter.CreateCounter<long>(
		CdcTelemetryConstants.MetricNames.EventsProcessed,
		"{events}",
		"Total CDC events processed successfully");

	private static readonly Counter<long> EventsFailedCounter = CdcTelemetryConstants.Meter.CreateCounter<long>(
		CdcTelemetryConstants.MetricNames.EventsFailed,
		"{events}",
		"Total CDC event processing failures");

	private static readonly Histogram<double> BatchDurationHistogram = CdcTelemetryConstants.Meter.CreateHistogram<double>(
		CdcTelemetryConstants.MetricNames.BatchDuration,
		"ms",
		"Duration of batch processing in milliseconds");

	private static readonly Histogram<int> BatchSizeHistogram = CdcTelemetryConstants.Meter.CreateHistogram<int>(
		CdcTelemetryConstants.MetricNames.BatchSize,
		"{events}",
		"Number of events in a processed batch");

	internal CdcChangeApplier(
		IDatabaseOptions dbConfig,
		IDataAccessPolicyFactory policyFactory,
		CdcCheckpointManager checkpointManager,
		Domain.OrderedEventProcessor orderedEventProcessor,
		ILogger logger,
		CdcFatalErrorHandler<DataChangeEvent>? onFatalError,
		ICdcIdempotencyFilter? idempotencyFilter = null,
		IMessageFailureClassifier? failureClassifier = null)
	{
		_dbConfig = dbConfig;
		_consumer = new CdcConsumerIdentity(dbConfig.DatabaseConnectionIdentifier, dbConfig.DatabaseName);
		_policyFactory = policyFactory;
		_checkpointManager = checkpointManager;
		_orderedEventProcessor = orderedEventProcessor;
		_logger = logger;
		_onFatalError = onFatalError;
		_idempotencyFilter = idempotencyFilter;
		_failureClassifier = failureClassifier;
	}

    // Read only after joining this attempt. Retains completed-batch accounting on fault/cancellation.
    internal int CompletedBatchEventCount { get; private set; }

    internal void ResetBatchAccounting() => CompletedBatchEventCount = 0;

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
        CompletedBatchEventCount = 0;

		// The failed-table barrier belongs to the RUN, not to one dequeued batch. Its job is to stop a
		// durable checkpoint moving past a change that was handed to the fatal-error callback and swallowed,
		// and the durable checkpoint outlives every individual batch — so a barrier scoped to a batch stops
		// protecting the moment the next batch is dequeued. With ConsumerBatchSize=1 that is immediately:
		// the failing change occupies a batch by itself, the barrier is discarded with that batch, and the
		// NEXT same-table change — succeeding, or merely skipped as already-processed — writes a checkpoint
		// past the change that never succeeded. That change is then never redelivered.
		//
		// Run scope is sufficient AND necessary. Sufficient because a run that bars a table writes no
		// checkpoint for it, so the next run re-reads the same durable position and redelivers the failed
		// change. Necessary because nothing shorter spans the interval over which the checkpoint can move.
		var failedTables = new HashSet<string>(StringComparer.Ordinal);

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

				await ProcessBatchAsync(batch, eventHandler, failedTables, cancellationToken).ConfigureAwait(false);

				LogProcessedBatch(batch.Length, stopwatch.Elapsed.TotalMilliseconds);

				totalProcessedCount += batch.Length;
                CompletedBatchEventCount = totalProcessedCount;
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
		HashSet<string> failedTables,
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

		// Tables that had a swallowed (onFatalError) failure anywhere in this RUN — supplied by the caller,
		// which owns the run. Once a table is in here, NO later event for it — success OR idempotency-skip,
		// in this batch or any later one — may advance its checkpoint.

		// Resolve the Polly policy once per batch instead of per-event.
		// The policy configuration does not change within a processing cycle.
		var batchPolicy = _policyFactory.GetComprehensivePolicy();

		foreach (var changeEvent in batch)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Idempotency check: skip events already processed in a prior cycle.
			if (_idempotencyFilter is not null
				&& await _idempotencyFilter.IsProcessedAsync(
					changeEvent.TableName,
					changeEvent.Lsn,
					changeEvent.SeqVal,
					_consumer,
					cancellationToken)
					.ConfigureAwait(false))
			{
				LogIdempotencyEventSkipped(changeEvent.TableName,
					CdcChangeDetector.ByteArrayToHex(changeEvent.Lsn),
					CdcChangeDetector.ByteArrayToHex(changeEvent.SeqVal));

				// Treat as successful for checkpoint advancement purposes -- but ONLY if this table
				// has not already had a swallowed failure in this run. Advancing here would move the
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
					// The SAME TUPLE the checkpoint advances under -- connection identifier AND database
					// name. This comment previously claimed that sourcing both from one field meant the
					// filter and the position it guards could not disagree about who is asking. Sourcing
					// them from ONE field is what made them disagree: the checkpoint store matches on
					// (DatabaseConnectionIdentifier, DatabaseName, TableName), three axes, while the dedupe
					// key carried two of them. The dedupe namespace was therefore strictly coarser than the
					// namespace of the position it guards, and a coarser dedupe namespace SUPPRESSES -- the
					// first consumer to reach a position marks it done for everyone sharing the coarser key.
					await _idempotencyFilter.MarkProcessedAsync(
						changeEvent.TableName,
						changeEvent.Lsn,
						changeEvent.SeqVal,
						_consumer,
						cancellationToken)
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
				// Do NOT re-advance a table that already had a swallowed failure in this run.
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
				// Mark the table failed for the REST OF THE RUN (so no later event -- success OR
				// idempotency-skip, in this batch or any later one -- can re-add it) and remove any
				// position already tracked for it in this batch. Because the run then writes no
				// checkpoint for this table, the next cycle resumes from the previous cycle's durable
				// position and the failed event is redelivered.
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
			try
			{
				await batchPolicy.ExecuteAsync(() => _checkpointManager.UpdateTableLastProcessedAsync(
					tableName,
					lastEvent.Lsn,
					lastEvent.SeqVal,
					lastEvent.CommitTime,
					cancellationToken)).ConfigureAwait(false);
			}
			catch (Exception ex) when (CdcFatalGuard.Decide(ex, _failureClassifier).Stop)
			{
				// Route the checkpoint-write fatal decision through the shared CdcFatalGuard, matching the
				// Cosmos/Dynamo/Mongo/Firestore/Postgres providers. A fatal fault — chiefly a demoted leader's
				// CdcLeadershipSupersededException from losing the fencing CAS — must STOP the loop, never be
				// retried: retrying the same stale-token write is rejected identically and would spin the
				// demoted instance. The comprehensive retry policy handles ONLY transient SqlException numbers /
				// timeouts (its allow-list predicate), so a fatal is structurally never retried before reaching
				// here; rethrowing propagates it out of the consumer loop and stops the processor.
				LogFatalCheckpoint(tableName, ex);
				throw;
			}
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

	[LoggerMessage(DataSqlServerEventId.CdcConsumerFatalCheckpoint, LogLevel.Critical,
		"Fatal fault writing CDC checkpoint for table '{TableName}'; leadership superseded or non-retryable. Stopping processor.")]
	private partial void LogFatalCheckpoint(string tableName, Exception ex);
}
