// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Postgres.DependencyInjection;

/// <summary>
/// Health check configuration options for Postgres event sourcing infrastructure.
/// </summary>
/// <remarks>
/// This sub-options class is part of the <see cref="PostgresEventSourcingOptions"/> ISP split
/// to keep each class within the 10-property gate.
/// </remarks>
public sealed class PostgresEventSourcingHealthCheckOptions
{
	/// <summary>
	/// Gets or sets whether to register health checks for event sourcing stores.
	/// Default: true.
	/// </summary>
	public bool RegisterHealthChecks { get; set; } = true;

	/// <summary>
	/// Gets or sets the health check name for the event store. Default: "Postgres-event-store".
	/// </summary>
	public string EventStoreHealthCheckName { get; set; } = "Postgres-event-store";

	/// <summary>
	/// Gets or sets the health check name for the snapshot store. Default: "Postgres-snapshot-store".
	/// </summary>
	public string SnapshotStoreHealthCheckName { get; set; } = "Postgres-snapshot-store";

	/// <summary>
	/// Gets or sets the health check name for the outbox store. Default: "Postgres-outbox-store".
	/// </summary>
	public string OutboxStoreHealthCheckName { get; set; } = "Postgres-outbox-store";
}
