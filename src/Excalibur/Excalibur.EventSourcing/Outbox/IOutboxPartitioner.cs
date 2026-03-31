// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Outbox;

/// <summary>
/// Determines which outbox partition a message should be routed to.
/// </summary>
public interface IOutboxPartitioner
{
	/// <summary>
	/// Gets the partition index for the given tenant.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <returns>The zero-based partition index.</returns>
	int GetPartition(string tenantId);

	/// <summary>
	/// Gets the total number of partitions.
	/// </summary>
	int PartitionCount { get; }
}
