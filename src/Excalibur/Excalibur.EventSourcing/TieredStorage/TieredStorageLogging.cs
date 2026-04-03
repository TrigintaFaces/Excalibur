// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// High-performance [LoggerMessage] source-generated log methods for tiered event storage.
/// Event IDs 3020-3039.
/// </summary>
internal static partial class TieredStorageLogging
{
	// TieredEventStoreDecorator
	[LoggerMessage(EventId = 3020, Level = LogLevel.Debug,
		Message = "Loading events for {AggregateId} from cold storage (no hot events)")]
	internal static partial void LoadingFromColdStorage(this ILogger logger, string aggregateId);

	[LoggerMessage(EventId = 3021, Level = LogLevel.Debug,
		Message = "Loading events for {AggregateId}: {HotCount} hot, checking cold for versions before {FromVersion}")]
	internal static partial void LoadingColdAndHotEvents(
		this ILogger logger, string aggregateId, int hotCount, long fromVersion);

	// EventArchiveService
	[LoggerMessage(EventId = 3022, Level = LogLevel.Information,
		Message = "EventArchiveService started")]
	internal static partial void ArchiveServiceStarted(this ILogger logger);

	[LoggerMessage(EventId = 3023, Level = LogLevel.Error,
		Message = "Archive cycle failed; will retry next interval")]
	internal static partial void ArchiveCycleFailed(this ILogger logger, Exception exception);

	[LoggerMessage(EventId = 3024, Level = LogLevel.Information,
		Message = "EventArchiveService stopped")]
	internal static partial void ArchiveServiceStopped(this ILogger logger);

	[LoggerMessage(EventId = 3025, Level = LogLevel.Debug,
		Message = "Archive policy has no criteria configured; skipping cycle")]
	internal static partial void NoCriteriaConfigured(this ILogger logger);

	[LoggerMessage(EventId = 3026, Level = LogLevel.Debug,
		Message = "No archive candidates found")]
	internal static partial void NoCandidatesFound(this ILogger logger);

	[LoggerMessage(EventId = 3027, Level = LogLevel.Information,
		Message = "Found {CandidateCount} aggregates eligible for archival")]
	internal static partial void CandidatesFound(this ILogger logger, int candidateCount);

	[LoggerMessage(EventId = 3028, Level = LogLevel.Warning,
		Message = "Failed to archive aggregate {AggregateId}: {ErrorMessage}")]
	internal static partial void ArchiveAggregateFailed(
		this ILogger logger, string aggregateId, string errorMessage);

	[LoggerMessage(EventId = 3029, Level = LogLevel.Information,
		Message = "Archive cycle complete: {ArchivedCount}/{TotalCount} aggregates archived, {EventCount} events moved")]
	internal static partial void ArchiveCycleComplete(
		this ILogger logger, int archivedCount, int totalCount, long eventCount);

	[LoggerMessage(EventId = 3030, Level = LogLevel.Debug,
		Message = "Archiving {AggregateId}: {EventCount} events from version {FromVersion} to {ToVersion}")]
	internal static partial void ArchivingAggregate(
		this ILogger logger, string aggregateId, int eventCount, long fromVersion, long toVersion);
}
