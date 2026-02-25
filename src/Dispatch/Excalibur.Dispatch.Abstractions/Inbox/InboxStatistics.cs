// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents statistical information about inbox entries.
/// </summary>
public record InboxStatistics
{
	/// <summary>
	/// Gets the total number of entries in the inbox.
	/// </summary>
	/// <value> The current <see cref="TotalEntries" /> value. </value>
	public int TotalEntries { get; init; }

	/// <summary>
	/// Gets the number of successfully processed entries.
	/// </summary>
	/// <value> The current <see cref="ProcessedEntries" /> value. </value>
	public int ProcessedEntries { get; init; }

	/// <summary>
	/// Gets the number of failed entries.
	/// </summary>
	/// <value> The current <see cref="FailedEntries" /> value. </value>
	public int FailedEntries { get; init; }

	/// <summary>
	/// Gets the number of pending entries.
	/// </summary>
	/// <value> The current <see cref="PendingEntries" /> value. </value>
	public int PendingEntries { get; init; }
}
