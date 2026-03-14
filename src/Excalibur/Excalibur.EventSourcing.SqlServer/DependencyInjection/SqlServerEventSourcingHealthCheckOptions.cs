// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.SqlServer.DependencyInjection;

/// <summary>
/// Health check configuration options for SQL Server event sourcing infrastructure.
/// </summary>
/// <remarks>
/// This sub-options class is part of the <see cref="SqlServerEventSourcingOptions"/> ISP split
/// to keep each class within the 10-property gate.
/// </remarks>
public sealed class SqlServerEventSourcingHealthCheckOptions
{
	/// <summary>
	/// Gets or sets whether to register health checks for event sourcing stores.
	/// Default: true.
	/// </summary>
	public bool RegisterHealthChecks { get; set; } = true;

	/// <summary>
	/// Gets or sets the health check name for the event store. Default: "sqlserver-event-store".
	/// </summary>
	public string EventStoreHealthCheckName { get; set; } = "sqlserver-event-store";

	/// <summary>
	/// Gets or sets the health check name for the snapshot store. Default: "sqlserver-snapshot-store".
	/// </summary>
	public string SnapshotStoreHealthCheckName { get; set; } = "sqlserver-snapshot-store";

	/// <summary>
	/// Gets or sets the health check name for the outbox store. Default: "sqlserver-outbox-store".
	/// </summary>
	public string OutboxStoreHealthCheckName { get; set; } = "sqlserver-outbox-store";
}
