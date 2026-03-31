// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Provides context for snapshot decision evaluation in <see cref="AutoSnapshotOptions.CustomPolicy"/>.
/// </summary>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AggregateType">The aggregate type name.</param>
/// <param name="CurrentVersion">The aggregate version after the save.</param>
/// <param name="LastSnapshotVersion">The version of the last snapshot, or <see langword="null"/> if never snapshotted.</param>
/// <param name="LastSnapshotTimestamp">The timestamp of the last snapshot, or <see langword="null"/> if never snapshotted.</param>
/// <param name="EventsSinceSnapshot">The number of events committed since the last snapshot.</param>
public sealed record SnapshotDecisionContext(
	string AggregateId,
	string AggregateType,
	long CurrentVersion,
	long? LastSnapshotVersion,
	DateTimeOffset? LastSnapshotTimestamp,
	int EventsSinceSnapshot);
