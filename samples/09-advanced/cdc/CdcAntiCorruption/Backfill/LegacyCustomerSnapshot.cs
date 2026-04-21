// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace CdcAntiCorruption.Backfill;

/// <summary>
/// Represents a point-in-time customer snapshot used for historical backfill.
/// </summary>
public sealed record LegacyCustomerSnapshot
{
	/// <summary>
	/// Gets the external customer identifier from the legacy system.
	/// </summary>
	public required string ExternalId { get; init; }

	/// <summary>
	/// Gets the customer name from the source record.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the customer email from the source record.
	/// </summary>
	public required string Email { get; init; }

	/// <summary>
	/// Gets the optional customer phone number.
	/// </summary>
	public string? Phone { get; init; }

	/// <summary>
	/// Gets the source-system timestamp associated with the snapshot.
	/// </summary>
	public DateTime ChangedAtUtc { get; init; }
}
