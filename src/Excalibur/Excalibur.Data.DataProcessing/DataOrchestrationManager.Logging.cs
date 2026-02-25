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
		"UpdateCompletedCount did not match any rows for DataTaskId {DataTaskId}")]
	private partial void LogUpdateCompletedCountMismatch(Guid dataTaskId);
}
