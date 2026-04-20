// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace CdcAntiCorruption.Models;

/// <summary>
/// Represents customer data that has been adapted from the legacy schema
/// to the current domain model format.
/// </summary>
/// <remarks>
/// This record serves as the anti-corruption layer's output, providing a clean
/// domain-centric view of customer data regardless of the source schema version.
/// </remarks>
public sealed record AdaptedCustomerData
{
	/// <summary>
	/// Gets the external identifier from the legacy system.
	/// </summary>
	public required string ExternalId { get; init; }

	/// <summary>
	/// Gets the customer's name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the customer's email address.
	/// </summary>
	public required string Email { get; init; }

	/// <summary>
	/// Gets the customer's phone number, if available.
	/// </summary>
	public string? Phone { get; init; }

	/// <summary>
	/// Gets a value indicating whether the customer is active.
	/// </summary>
	public bool IsActive { get; init; } = true;

	/// <summary>
	/// Gets the timestamp when the change occurred.
	/// </summary>
	public DateTime ChangedAt { get; init; }
}
