// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.MultiTenancy;

/// <summary>
/// Options for first-class multi-tenancy composition, consumed by <c>AddMultiTenancy(...)</c>.
/// </summary>
/// <remarks>
/// A multi-tenant host selects exactly one <see cref="TenantIsolationStrategy"/>. The strategy is validated
/// at startup (<c>ValidateOnStart</c>) and again at composition time; leaving it
/// <see cref="TenantIsolationStrategy.Unspecified"/> fails fast rather than silently disabling isolation.
/// </remarks>
public sealed class MultiTenancyOptions
{
	/// <summary>
	/// Gets or sets the tenant-isolation strategy applied across the event store, projections, and sagas.
	/// </summary>
	/// <value>
	/// The chosen strategy. Defaults to <see cref="TenantIsolationStrategy.Unspecified"/>, which is rejected —
	/// a valid strategy (<see cref="TenantIsolationStrategy.RowDiscriminator"/> or
	/// <see cref="TenantIsolationStrategy.Sharding"/>) must be set.
	/// </value>
	public TenantIsolationStrategy Strategy { get; set; }
}
