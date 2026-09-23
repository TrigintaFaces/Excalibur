// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// The in-memory fetch frontier the change producer reads and advances: which capture instances are
/// being tracked and the next position to fetch for each.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately everything the producer needs and nothing more. In particular it exposes NO way
/// to write the durable resume position. The invariant is: for every capture instance, the durable
/// position is written only by the delivery path, only to the position of a change that was delivered
/// (or skipped as already processed), and never while an earlier change for that instance is pending or
/// failed. The producer runs ahead of delivery through a channel, so a durable write from the producer can
/// land past changes that have been fetched but not yet delivered, and a crash then resumes past them:
/// silent loss.
/// </para>
/// <para>
/// The producer may move this in-memory frontier freely, including past rows that retention cleanup has
/// already purged. It may not move the durable position at all, and holding only this interface is what
/// makes that inexpressible rather than merely avoided.
/// </para>
/// </remarks>
internal interface ICdcFetchProgress
{
	/// <summary>Gets the number of capture instances still being tracked in this run.</summary>
	int TrackingCount { get; }

	/// <summary>Gets the capture instances still being tracked in this run.</summary>
	IEnumerable<string> TrackedTables { get; }

	/// <summary>Gets the in-memory fetch position for a capture instance, or null when it is not tracked.</summary>
	CdcPosition? GetTracking(string tableName);

	/// <summary>Gets the lowest in-memory fetch position across tracked capture instances.</summary>
	byte[]? GetNextLsn();

	/// <summary>Moves the in-memory fetch position for a capture instance; a null LSN stops tracking it.</summary>
	void UpdateLsnTracking(string tableName, byte[]? lsn, byte[]? seqVal);

	/// <summary>Advances the in-memory fetch position after a fetch, dropping the instance once past the captured maximum.</summary>
	void UpdateLsnAfterProcessing(string tableName, byte[]? nextLsn, byte[] maxLsn);
}
