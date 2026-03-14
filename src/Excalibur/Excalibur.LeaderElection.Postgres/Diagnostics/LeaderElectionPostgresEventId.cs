// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Postgres.Diagnostics;

/// <summary>
/// Event IDs for Postgres leader election logging.
/// </summary>
/// <remarks>
/// Uses the same ID values as the original <c>DataPostgresEventId</c> leader election range
/// to maintain log correlation compatibility.
/// </remarks>
internal static class LeaderElectionPostgresEventId
{
	/// <summary>Leader election started.</summary>
	public const int LeaderElectionStarted = 107100;

	/// <summary>Leader election stopped.</summary>
	public const int LeaderElectionStopped = 107101;

	/// <summary>Lock acquisition failed (non-error, another candidate holds it).</summary>
	public const int LockAcquisitionFailed = 107102;

	/// <summary>Error acquiring advisory lock.</summary>
	public const int LockAcquisitionError = 107103;

	/// <summary>Advisory lock released.</summary>
	public const int LockReleased = 107104;

	/// <summary>Error releasing advisory lock.</summary>
	public const int LockReleaseError = 107105;

	/// <summary>Leader election renewal error.</summary>
	public const int LeaderElectionError = 107106;

	/// <summary>Candidate became leader.</summary>
	public const int BecameLeader = 107107;

	/// <summary>Candidate lost leadership.</summary>
	public const int LostLeadership = 107108;

	/// <summary>Leader election dispose error.</summary>
	public const int LeaderElectionDisposeError = 107109;
}
