// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// <item><c>PostgresEventSourcedOutboxStore</c></item>
/// </list>
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
/// {
///     es.UsePostgres(pg =&gt;
///     {
///         pg.ConnectionString("Host=localhost;Database=mydb;Username=user;Password=&lt;your-password&gt;")
///           .EventStoreSchema("public")
///           .EventStoreTable("events");
///     });
/// }));
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
	/// Not required when using <see cref="IPostgresEventSourcingBuilder.DataSource"/>,
	/// <see cref="IPostgresEventSourcingBuilder.DataSourceFactory"/>,
	/// <see cref="IPostgresEventSourcingBuilder.ConnectionStringName"/>, or
	/// <see cref="IPostgresEventSourcingBuilder.BindConfiguration"/>.
	/// Connection validation is handled by <c>PostgresEventSourcingOptionsValidator</c>.
	/// </remarks>
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
	/// Gets or sets the snapshot store table name. Default: "event_store_snapshots".
	/// </summary>
	public string SnapshotStoreTable { get; set; } = "event_store_snapshots";

	/// <summary>
	/// Gets or sets the health check configuration options.
	/// </summary>
	/// <value>Health check options including registration flag and custom names.</value>
	public PostgresEventSourcingHealthCheckOptions HealthChecks { get; set; } = new();

	/// <summary>
	/// Gets or sets a source-generated JSON type-info resolver covering the application's domain event types
	/// and the runtime types of the values it places in <see cref="Excalibur.Dispatch.IDomainEvent.Metadata"/>,
	/// enabling a reflection-free serialization path under trimming and native AOT.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Domain events are consumer types the framework cannot source-generate, so with no resolver the store
	/// serializes them through the reflection-based <see cref="System.Text.Json.JsonSerializer"/>. That works
	/// under the JIT, but a native-AOT application published with reflection-based serialization disabled has
	/// no reflection path to fall back on and the first append fails. Set this to a
	/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> (or any
	/// <see cref="System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver"/>) covering those types to
	/// remove that dependency.
	/// </para>
	/// <para>
	/// The stored wire format does not vary with this setting. The resolver supplies type metadata only; the
	/// property naming policy, string-enum representation and null handling are the store's own and are
	/// applied to whichever resolver is in use, so events written with a resolver are byte-identical to events
	/// written without one and remain readable by a host configured either way.
	/// </para>
	/// <para>
	/// Metadata values are typed <see cref="object"/> and are therefore written as their runtime type. Declare
	/// each closed value type the application actually stores -- <c>string</c>, <c>int</c>, <c>bool</c> and so
	/// on. Do not declare <c>Dictionary&lt;string, object&gt;</c> as a shortcut: it compiles and then throws on
	/// the values it was meant to cover.
	/// </para>
	/// </remarks>
	/// <value>The consumer's event type-info resolver, or <see langword="null"/> to serialize through
	/// reflection.</value>
	public System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? EventTypeInfoResolver { get; set; }
}
