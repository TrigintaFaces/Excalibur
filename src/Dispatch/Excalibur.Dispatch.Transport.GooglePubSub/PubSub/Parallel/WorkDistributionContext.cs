// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Context for work distribution decisions.
/// </summary>
public sealed class WorkDistributionContext
{
	/// <summary>
	/// Gets the ordering key of the message.
	/// </summary>
	/// <value>
	/// The ordering key of the message.
	/// </value>
	public string? OrderingKey { get; init; }

	/// <summary>
	/// Gets the message size in bytes.
	/// </summary>
	/// <value>
	/// The message size in bytes.
	/// </value>
	public int MessageSize { get; init; }

	/// <summary>
	/// Gets the total number of workers.
	/// </summary>
	/// <value>
	/// The total number of workers.
	/// </value>
	public int TotalWorkers { get; init; }

	/// <summary>
	/// Gets current pending work counts per worker.
	/// </summary>
	/// <value>
	/// Current pending work counts per worker.
	/// </value>
	public int[] PendingWorkCounts { get; init; } = [];
}
