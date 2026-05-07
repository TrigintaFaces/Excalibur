// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Data.DataProcessing;

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
	public Task<CursorFetchResult<LegacyCustomerSnapshot>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		var skip = cursor is null ? 0 : int.Parse(cursor, CultureInfo.InvariantCulture);

		if (skip >= Snapshots.Count)
		{
			return Task.FromResult(new CursorFetchResult<LegacyCustomerSnapshot>([], null));
		}

		var batch = Snapshots.Skip(skip).Take(batchSize).ToList();
		var nextPosition = skip + batch.Count;
		var nextCursor = batch.Count > 0 && nextPosition < Snapshots.Count
			? nextPosition.ToString(CultureInfo.InvariantCulture)
			: null;

		return Task.FromResult(new CursorFetchResult<LegacyCustomerSnapshot>(batch, nextCursor));
	}
}
