// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Postgres.DependencyInjection;

/// <summary>
/// Configuration options for Postgres event sourcing infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This options class configures the Postgres implementations of:
/// <list type="bullet">
/// <item><see cref="PostgresEventStore"/></item>
/// <item><see cref="PostgresSnapshotStore"/></item>
/// <item><see cref="PostgresEventSourcedOutboxStore"/></item>
/// </list>
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddPostgresEventSourcing(options =>
/// {
///     options.ConnectionString = "Host=localhost;Database=mydb;Username=user;Password=pass";
///     options.EventStoreSchema = "public";
///     options.EventStoreTable = "events";
/// });
/// </code>
/// </para>
/// <para>
/// <b>Postgres-specific features:</b>
/// <list type="bullet">
/// <item>Uses <c>jsonb</c> for metadata storage</item>
/// <item>Uses <c>uuid</c> for event/aggregate IDs</item>
/// <item>Uses <c>timestamptz</c> for timestamps</item>
/// <item>Uses <c>bigserial</c> for auto-incrementing positions</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PostgresEventSourcingOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	/// <remarks>
	/// Required unless using a custom NpgsqlDataSource.
	/// </remarks>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for event store tables. Default: "public".
	/// </summary>
	public string EventStoreSchema { get; set; } = "public";

	/// <summary>
	/// Gets or sets the event store table name. Default: "events".
	/// </summary>
	public string EventStoreTable { get; set; } = "events";

	/// <summary>
	/// Gets or sets the schema name for snapshot store tables. Default: "public".
	/// </summary>
	public string SnapshotStoreSchema { get; set; } = "public";

	/// <summary>
	/// Gets or sets the snapshot store table name. Default: "snapshots".
	/// </summary>
	public string SnapshotStoreTable { get; set; } = "snapshots";

	/// <summary>
	/// Gets or sets the schema name for outbox tables. Default: "public".
	/// </summary>
	public string OutboxSchema { get; set; } = "public";

	/// <summary>
	/// Gets or sets the outbox table name. Default: "event_sourced_outbox".
	/// </summary>
	public string OutboxTable { get; set; } = "event_sourced_outbox";

	/// <summary>
	/// Gets or sets the health check configuration options.
	/// </summary>
	/// <value>Health check options including registration flag and custom names.</value>
	public PostgresEventSourcingHealthCheckOptions HealthChecks { get; set; } = new();
}
