// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.LeaderElection.Watch;

/// <summary>
/// Represents a leader change event observed by <see cref="ILeaderElectionWatcher"/>.
/// </summary>
/// <param name="PreviousLeader">The identifier of the previous leader, or <see langword="null"/> if there was no leader.</param>
/// <param name="NewLeader">The identifier of the new leader, or <see langword="null"/> if leadership was vacated.</param>
/// <param name="ChangedAt">The timestamp when the change was detected.</param>
/// <param name="Reason">The reason for the leader change.</param>
public sealed record LeaderChangeEvent(
	string? PreviousLeader,
	string? NewLeader,
	DateTimeOffset ChangedAt,
	LeaderChangeReason Reason);
