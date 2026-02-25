// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.DataProcessing;

public abstract partial class DataProcessor<TRecord>
{
	// Source-generated logging methods

	// Core disposal methods (107100-107107)
	[LoggerMessage(DataProcessingEventId.DisposeAsync, LogLevel.Information,
		"Disposing DataProcessor resources asynchronously.")]
	private partial void LogDisposeAsync();

	[LoggerMessage(DataProcessingEventId.ConsumerNotCompletedAsync, LogLevel.Warning,
		"Disposing DataProcessor but Consumer has not completed.")]
	private partial void LogConsumerNotCompletedAsync();

	[LoggerMessage(DataProcessingEventId.ConsumerTimeoutAsync, LogLevel.Warning,
		"Consumer did not complete in time during async disposal.")]
	private partial void LogConsumerTimeoutAsync();

	[LoggerMessage(DataProcessingEventId.DisposeAsyncError, LogLevel.Error,
		"Error disposing DataProcessor asynchronously.")]
	private partial void LogDisposeAsyncError(Exception ex);

	[LoggerMessage(DataProcessingEventId.DisposeSync, LogLevel.Information,
		"Disposing OutboxManager resources synchronously.")]
	private partial void LogDisposeSync();

	[LoggerMessage(DataProcessingEventId.ConsumerNotCompletedSync, LogLevel.Warning,
		"Disposing OutboxManager but Consumer has not completed.")]
	private partial void LogConsumerNotCompletedSync();

	[LoggerMessage(DataProcessingEventId.ConsumerTimeoutSync, LogLevel.Warning,
		"Consumer did not complete in time during synchronous disposal.")]
	private partial void LogConsumerTimeoutSync();

	[LoggerMessage(DataProcessingEventId.DisposeError, LogLevel.Error,
		"Error while disposing DataProcessor on application shutdown.")]
	private partial void LogDisposeError(Exception ex);

	// Consumer methods (107200-107209)
	[LoggerMessage(DataProcessingEventId.ConsumerDisposalRequested, LogLevel.Warning,
		"ConsumerLoop: disposal requested, exit Excalibur.Data.")]
	private partial void LogConsumerDisposalRequested();

	[LoggerMessage(DataProcessingEventId.NoMoreRecordsConsumerExit, LogLevel.Information,
		"No more DataProcessor records. Consumer is exiting.")]
	private partial void LogNoMoreRecordsConsumerExit();

	[LoggerMessage(DataProcessingEventId.QueueEmptyWaiting, LogLevel.Information,
		"DataProcessor Queue is empty. Waiting for examples.AdvancedSample.Producer...")]
	private partial void LogQueueEmptyWaiting();

	[LoggerMessage(DataProcessingEventId.ProcessingBatch, LogLevel.Information,
		"DataProcessor processing batch of {BatchSize} records")]
	private partial void LogProcessingBatch(int batchSize);

	[LoggerMessage(DataProcessingEventId.ProcessingRecordError, LogLevel.Error,
		"Error processing DataProcessor record {RecordId} of type {RecordType}")]
	private partial void LogProcessingRecordError(string recordId, string recordType, Exception ex);

	[LoggerMessage(DataProcessingEventId.CompletedBatch, LogLevel.Debug,
		"Completed DataProcessor batch of {BatchSize} events")]
	private partial void LogCompletedBatch(int batchSize);

	[LoggerMessage(DataProcessingEventId.CompletedProcessing, LogLevel.Information,
		"Completed DataProcessor processing, total events processed: {TotalEvents}")]
	private partial void LogCompletedProcessing(long totalEvents);

	[LoggerMessage(DataProcessingEventId.ConsumerCanceled, LogLevel.Debug,
		"Consumer canceled")]
	private partial void LogConsumerCanceled();

	[LoggerMessage(DataProcessingEventId.ConsumerError, LogLevel.Error,
		"Error in ConsumerLoop")]
	private partial void LogConsumerError(Exception ex);

	[LoggerMessage(DataProcessingEventId.NoHandlerFound, LogLevel.Error,
		"No handler found for {RecordType}. Skipping record.")]
	private partial void LogNoHandlerFound(string recordType);

	// Producer methods (107300-107308)
	[LoggerMessage(DataProcessingEventId.NoMoreRecordsProducerExit, LogLevel.Information,
		"No more DataProcessor records. Producer exiting.")]
	private partial void LogNoMoreRecordsProducerExit();

	[LoggerMessage(DataProcessingEventId.EnqueuingRecords, LogLevel.Information,
		"Enqueuing {BatchSize} DataProcessor records")]
	private partial void LogEnqueuingRecords(int batchSize);

	[LoggerMessage(DataProcessingEventId.SuccessfullyEnqueued, LogLevel.Information,
		"Successfully enqueued {EnqueuedRowCount} DataProcessor records")]
	private partial void LogSuccessfullyEnqueued(int enqueuedRowCount);

	[LoggerMessage(DataProcessingEventId.ProducerCanceled, LogLevel.Debug,
		"DataProcessor Producer canceled")]
	private partial void LogProducerCanceled();

	[LoggerMessage(DataProcessingEventId.ProducerError, LogLevel.Error,
		"Error in DataProcessor ProducerLoop")]
	private partial void LogProducerError(Exception ex);

	[LoggerMessage(DataProcessingEventId.ProducerCompleted, LogLevel.Information,
		"DataProcessor Producer has completed execution. Channel marked as complete.")]
	private partial void LogProducerCompleted();

	[LoggerMessage(DataProcessingEventId.ApplicationStopping, LogLevel.Information,
		"Application is stopping. Cancelling DataProcessor producer immediately.")]
	private partial void LogApplicationStopping();

	[LoggerMessage(DataProcessingEventId.ProducerCancellationRequested, LogLevel.Information,
		"DataProcessor Producer cancellation requested.")]
	private partial void LogProducerCancellationRequested();

	[LoggerMessage(DataProcessingEventId.WaitingForConsumer, LogLevel.Information,
		"Waiting for DataProcessor consumer to finish remaining work...")]
	private partial void LogWaitingForConsumer();
}
