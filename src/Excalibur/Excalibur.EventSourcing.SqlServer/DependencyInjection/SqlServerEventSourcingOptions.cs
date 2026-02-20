// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.SqlServer.DependencyInjection;

/// <summary>
/// Configuration options for SQL Server event sourcing infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This options class configures the SQL Server implementations of:
/// <list type="bullet">
/// <item><see cref="SqlServerEventStore"/></item>
/// <item><see cref="SqlServerSnapshotStore"/></item>
/// <item><see cref="SqlServerEventSourcedOutboxStore"/></item>
/// </list>
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddSqlServerEventSourcing(options =>
/// {
///     options.ConnectionString = "Server=...";
///     options.EventStoreSchema = "dbo";
///     options.EventStoreTable = "Events";
/// });
/// </code>
/// </para>
/// </remarks>
public sealed class SqlServerEventSourcingOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	/// <remarks>
	/// Required unless using a custom connection factory.
	/// </remarks>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for event store tables. Default: "dbo".
	/// </summary>
	public string EventStoreSchema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the event store table name. Default: "Events".
	/// </summary>
	public string EventStoreTable { get; set; } = "Events";

	/// <summary>
	/// Gets or sets the schema name for snapshot store tables. Default: "dbo".
	/// </summary>
	public string SnapshotStoreSchema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the snapshot store table name. Default: "Snapshots".
	/// </summary>
	public string SnapshotStoreTable { get; set; } = "Snapshots";

	/// <summary>
	/// Gets or sets the schema name for outbox tables. Default: "dbo".
	/// </summary>
	public string OutboxSchema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the outbox table name. Default: "EventSourcedOutbox".
	/// </summary>
	public string OutboxTable { get; set; } = "EventSourcedOutbox";

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
