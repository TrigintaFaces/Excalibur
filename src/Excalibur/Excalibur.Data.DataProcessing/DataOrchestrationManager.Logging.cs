// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.DataProcessing;

public sealed partial class DataOrchestrationManager
{
	// Source-generated logging methods
	[LoggerMessage(DataProcessingEventId.ProcessorNotFound, LogLevel.Warning,
		"Error processing back fill for table '{RecordType}'. No processor found.")]
	private partial void LogProcessorNotFound(string recordType);

	[LoggerMessage(DataProcessingEventId.ProcessingDataTaskError, LogLevel.Error,
		"Error processing data task for {RecordType}. Attempts: {Attempts}")]
	private partial void LogProcessingDataTaskError(string recordType, int attempts, Exception ex);

	[LoggerMessage(DataProcessingEventId.UpdateCompletedCountMismatch, LogLevel.Warning,
		"UpdateCompletedCount did not match any rows for DataTaskId {DataTaskId}. " +
		"Task row may have been deleted or database may have been restored.")]
	private partial void LogUpdateCompletedCountMismatch(Guid dataTaskId);

	[LoggerMessage(DataProcessingEventId.DataTaskStale, LogLevel.Warning,
		"Data task {DataTaskId} for {RecordType} is stale — task row no longer exists. " +
		"Processing aborted. This typically occurs after a database restore.")]
	private partial void LogDataTaskStale(Guid dataTaskId, string recordType);

	[LoggerMessage(DataProcessingEventId.UpdateAttemptsFailed, LogLevel.Error,
		"Failed to update attempt count for data task {DataTaskId}. Database may be unavailable.")]
	private partial void LogUpdateAttemptsFailed(Guid dataTaskId, Exception ex);

	[LoggerMessage(DataProcessingEventId.DeleteTaskFailed, LogLevel.Error,
		"Failed to delete completed data task {DataTaskId}. " +
		"Task will be re-processed on next poll but should be idempotent.")]
	private partial void LogDeleteTaskFailed(Guid dataTaskId, Exception ex);
}
