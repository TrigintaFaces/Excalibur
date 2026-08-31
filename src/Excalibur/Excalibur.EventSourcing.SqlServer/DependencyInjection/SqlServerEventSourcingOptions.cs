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
/// <item><c>SqlServerEventSourcedOutboxStore</c></item>
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
	/// Not required when using <see cref="ISqlServerEventSourcingBuilder.ConnectionFactory"/>,
	/// <see cref="ISqlServerEventSourcingBuilder.ConnectionStringName"/>, or
	/// <see cref="ISqlServerEventSourcingBuilder.BindConfiguration"/>.
	/// Connection validation is handled by <c>SqlServerEventSourcingOptionsValidator</c>.
	/// </remarks>
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
	/// Gets or sets the health check configuration options.
	/// </summary>
	/// <value>Health check options including registration flag and custom names.</value>
	public SqlServerEventSourcingHealthCheckOptions HealthChecks { get; set; } = new();

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
