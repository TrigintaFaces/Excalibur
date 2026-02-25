// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Event arguments for leader change events.
/// </summary>
/// <param name="previousLeaderId">The identifier of the previous leader, or null if there was no previous leader.</param>
/// <param name="newLeaderId">The identifier of the new leader, or null if leadership was relinquished.</param>
/// <param name="resourceName">The name of the resource for which leadership changed.</param>
public sealed class LeaderChangedEventArgs(string? previousLeaderId, string? newLeaderId, string resourceName) : EventArgs
{
	/// <summary>
	/// Gets the previous leader ID.
	/// </summary>
	/// <value>the previous leader ID.</value>
	public string? PreviousLeaderId { get; } = previousLeaderId;

	/// <summary>
	/// Gets the new leader ID.
	/// </summary>
	/// <value>the new leader ID.</value>
	public string? NewLeaderId { get; } = newLeaderId;

	/// <summary>
	/// Gets the resource name.
	/// </summary>
	/// <value>the resource name.</value>
	public string ResourceName { get; } = resourceName;

	/// <summary>
	/// Gets when the change occurred.
	/// </summary>
	/// <value>when the change occurred.</value>
	public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
