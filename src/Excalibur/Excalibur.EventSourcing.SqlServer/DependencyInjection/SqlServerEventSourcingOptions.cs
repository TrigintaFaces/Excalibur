// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

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
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for event store tables. Default: "dbo".
	/// </summary>
	public string EventStoreSchema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the event store table name. Default: "EventStoreEvents".
	/// </summary>
	public string EventStoreTable { get; set; } = "EventStoreEvents";

	/// <summary>
	/// Gets or sets the schema name for snapshot store tables. Default: "dbo".
	/// </summary>
	public string SnapshotStoreSchema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the snapshot store table name. Default: "EventStoreSnapshots".
	/// </summary>
	public string SnapshotStoreTable { get; set; } = "EventStoreSnapshots";

	/// <summary>
	/// Gets or sets the schema name for outbox tables. Default: "dbo".
	/// </summary>
	public string OutboxSchema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the outbox table name. Default: "EventSourcedOutbox".
	/// </summary>
	public string OutboxTable { get; set; } = "EventSourcedOutbox";

	/// <summary>
	/// Gets or sets the health check configuration options.
	/// </summary>
	/// <value>Health check options including registration flag and custom names.</value>
	public SqlServerEventSourcingHealthCheckOptions HealthChecks { get; set; } = new();
}
