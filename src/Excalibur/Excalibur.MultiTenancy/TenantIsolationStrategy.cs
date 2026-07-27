// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.MultiTenancy;

/// <summary>
/// Selects how tenant data is isolated across the event store, projections, and sagas.
/// </summary>
public enum TenantIsolationStrategy
{
	/// <summary>
	/// No strategy selected. This is the default and is rejected at startup — a multi-tenant host must
	/// choose an explicit strategy.
	/// </summary>
	Unspecified = 0,

	/// <summary>
	/// A single shared store per subsystem with a <c>TenantId</c> discriminator column applied inside every
	/// query. Fail-closed tenant scoping is enforced by wrapping each store with its tenant-scoping decorator.
	/// </summary>
	RowDiscriminator = 1,

	/// <summary>
	/// A dedicated store (database/schema/connection) per tenant, selected per operation by tenant-aware
	/// routing. Routing is configured on the event-sourcing builder via <c>EnableTenantSharding(...)</c>.
	/// </summary>
	Sharding = 2,
}
