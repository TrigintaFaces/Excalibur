// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.DynamoDb.Inbox;

public sealed partial class DynamoDbInboxStore
{
	// Source-generated logging methods

	[LoggerMessage(DataDynamoDbEventId.InboxMessageStored, LogLevel.Debug,
		"Created inbox entry for message {MessageId} and handler {HandlerType}")]
	private partial void LogCreatedEntry(string messageId, string handlerType);

	[LoggerMessage(DataDynamoDbEventId.InboxMessageCompleted, LogLevel.Debug,
		"Marked inbox entry as processed for message {MessageId} and handler {HandlerType}")]
	private partial void LogMarkedProcessed(string messageId, string handlerType);

	[LoggerMessage(DataDynamoDbEventId.InboxMessageAlreadyProcessed, LogLevel.Debug,
		"First processor for message {MessageId} and handler {HandlerType}")]
	private partial void LogFirstProcessor(string messageId, string handlerType);

	[LoggerMessage(DataDynamoDbEventId.InboxMessageProcessingError, LogLevel.Debug,
		"Duplicate detected for message {MessageId} and handler {HandlerType}")]
	private partial void LogDuplicateDetected(string messageId, string handlerType);

	[LoggerMessage(DataDynamoDbEventId.InboxMessageStoringError, LogLevel.Warning,
		"Marked inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}")]
	private partial void LogMarkedFailed(string messageId, string handlerType, string error);

	[LoggerMessage(DataDynamoDbEventId.InboxCleanupComplete, LogLevel.Information,
		"Cleaned up {Count} processed inbox entries older than {CutoffDate}")]
	private partial void LogCleanedUp(int count, DateTimeOffset cutoffDate);
}
