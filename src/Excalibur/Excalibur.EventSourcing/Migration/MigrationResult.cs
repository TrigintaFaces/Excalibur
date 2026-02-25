// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Migration;

/// <summary>
/// Represents the result of an event batch migration operation.
/// </summary>
/// <param name="EventsMigrated">The total number of events migrated.</param>
/// <param name="EventsSkipped">The total number of events skipped by the filter.</param>
/// <param name="StreamsMigrated">The total number of streams processed.</param>
/// <param name="IsDryRun">Whether this was a dry run.</param>
/// <param name="Errors">Any errors encountered during migration.</param>
public sealed record EventMigrationResult(
	long EventsMigrated,
	long EventsSkipped,
	int StreamsMigrated,
	bool IsDryRun,
	IReadOnlyList<string> Errors);
