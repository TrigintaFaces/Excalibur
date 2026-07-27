// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// Aggregate counts of durable workflow instances by lifecycle status, for an administrative overview.
/// </summary>
public sealed record WorkflowStoreStatistics
{
	/// <summary>
	/// Gets the number of instances that are running (executing or awaiting a timer or signal).
	/// </summary>
	/// <value>The running instance count.</value>
	public long Running { get; init; }

	/// <summary>
	/// Gets the number of instances that have completed successfully.
	/// </summary>
	/// <value>The completed instance count.</value>
	public long Completed { get; init; }

	/// <summary>
	/// Gets the number of instances that have faulted.
	/// </summary>
	/// <value>The faulted instance count.</value>
	public long Faulted { get; init; }

	/// <summary>
	/// Gets the total number of instances across all statuses.
	/// </summary>
	/// <value>The total instance count.</value>
	public long Total { get; init; }
}
