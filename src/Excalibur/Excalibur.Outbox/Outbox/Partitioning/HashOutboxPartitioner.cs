// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Hashing;
using System.Text;

namespace Excalibur.Outbox.Partitioning;

/// <summary>
/// Partitions outbox messages by hashing the tenant ID using XxHash32.
/// Provides consistent, deterministic partition assignment without requiring shard infrastructure.
/// </summary>
internal sealed class HashOutboxPartitioner : IOutboxPartitioner
{
	private readonly int _partitionCount;

	internal HashOutboxPartitioner(int partitionCount)
	{
		if (partitionCount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(partitionCount), "Partition count must be positive.");
		}

		_partitionCount = partitionCount;
	}

	/// <inheritdoc />
	public int GetPartition(string tenantId)
	{
		ArgumentNullException.ThrowIfNull(tenantId);

		// XxHash32: deterministic, fast, excellent distribution.
		// Stable across processes, .NET versions, and platforms.
		var byteCount = Encoding.UTF8.GetByteCount(tenantId);
		byte[]? rented = null;
		var buffer = byteCount <= 256
			? stackalloc byte[byteCount]
			: (rented = new byte[byteCount]).AsSpan();
		Encoding.UTF8.GetBytes(tenantId, buffer);
		var hash = XxHash32.HashToUInt32(buffer);
		_ = rented; // suppress unused warning
		return (int)(hash % (uint)_partitionCount);
	}

	/// <inheritdoc />
	public int PartitionCount => _partitionCount;
}
