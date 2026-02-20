// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.DynamoDb;

public sealed partial class DynamoDbEventStore
{
	// Source-generated logging methods

	[LoggerMessage(EventSourcingEventId.CloudStoreAppendingEvents, LogLevel.Debug,
		"Appending events to stream {StreamId} for aggregate {AggregateType}")]
	private partial void LogAppendingEvents(string streamId, string aggregateType);

	[LoggerMessage(EventSourcingEventId.CloudStoreEventsAppended, LogLevel.Information,
		"Appended {Count} events to stream {StreamId}, consumed {Capacity} capacity units")]
	private partial void LogEventsAppended(string streamId, int count, double capacity);

	[LoggerMessage(EventSourcingEventId.CloudStoreConcurrencyConflict, LogLevel.Warning,
		"Concurrency conflict on stream {StreamId}, expected version {ExpectedVersion}")]
	private partial void LogConcurrencyConflict(string streamId, long expectedVersion);

	[LoggerMessage(EventSourcingEventId.CloudStoreLoadingEvents, LogLevel.Debug,
		"Loaded {Count} events for stream {StreamId}")]
	private partial void LogLoadingEvents(string streamId, int count);
}
