// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace CdcAntiCorruption.Backfill;

/// <summary>
/// In-memory snapshot source used by the sample to emulate historical legacy data.
/// </summary>
public sealed class InMemoryLegacyCustomerSnapshotSource : ILegacyCustomerSnapshotSource
{
	private static readonly IReadOnlyList<LegacyCustomerSnapshot> Snapshots =
	[
		new LegacyCustomerSnapshot
		{
			ExternalId = "CUST-000",
			Name = "Jane Legacy",
			Email = "jane.legacy@example.com",
			Phone = "+1-555-0100",
			ChangedAtUtc = DateTime.UtcNow.AddMonths(-2),
		},
		new LegacyCustomerSnapshot
		{
			ExternalId = "CUST-001",
			Name = "John D. Smith",
			Email = "john.smith@example.com",
			Phone = "+1-555-0123",
			ChangedAtUtc = DateTime.UtcNow.AddDays(-20),
		},
		new LegacyCustomerSnapshot
		{
			ExternalId = "CUST-002",
			Name = "Mia Replay",
			Email = "mia.replay@example.com",
			Phone = "+1-555-0142",
			ChangedAtUtc = DateTime.UtcNow.AddDays(-5),
		},
	];

	/// <inheritdoc />
	public Task<IEnumerable<LegacyCustomerSnapshot>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		ArgumentOutOfRangeException.ThrowIfNegative(skip);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		if (skip >= Snapshots.Count)
		{
			return Task.FromResult<IEnumerable<LegacyCustomerSnapshot>>([]);
		}

		var startIndex = (int)Math.Min(skip, int.MaxValue);
		var result = Snapshots.Skip(startIndex).Take(batchSize).ToArray();
		return Task.FromResult<IEnumerable<LegacyCustomerSnapshot>>(result);
	}
}
