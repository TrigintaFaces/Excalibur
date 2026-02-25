// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Firestore;

public sealed partial class FirestoreEventStore
{
	// Source-generated logging methods

	[LoggerMessage(EventSourcingEventId.CloudStoreInitializing, LogLevel.Information,
		"Initializing Firestore event store with collection '{Collection}'")]
	partial void LogInitializing(string collection);

	[LoggerMessage(EventSourcingEventId.CloudStoreLoadedEvents, LogLevel.Debug,
		"Loaded events for stream '{StreamId}', count: {Count}")]
	partial void LogLoadedEvents(string streamId, long count);

	[LoggerMessage(EventSourcingEventId.CloudStoreAppendingEvents, LogLevel.Debug,
		"Appending {Count} events to stream '{StreamId}'")]
	partial void LogAppendingEvents(string streamId, int count);

	[LoggerMessage(EventSourcingEventId.CloudStoreConcurrencyConflict, LogLevel.Warning,
		"Concurrency conflict for stream '{StreamId}': {Message}")]
	partial void LogConcurrencyConflict(string streamId, string message);
}
