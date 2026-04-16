// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres event sourcing settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides 5 canonical connection overloads (adds <see cref="DataSource(NpgsqlDataSource)"/>
/// beyond the SQL Server pattern's 4) plus subsystem-specific configuration
/// for event store and snapshot store schemas and tables.
/// </para>
/// <para>
/// <b>Connection overloads are mutually exclusive (last-wins):</b> If multiple connection
/// methods are called, the last one takes effect.
/// </para>
/// <para>
/// <b>All connection paths converge to <see cref="NpgsqlDataSource"/>:</b>
/// <see cref="ConnectionString(string)"/> and <see cref="ConnectionStringName(string)"/>
/// create an <see cref="NpgsqlDataSource"/> internally for proper connection pooling.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcaliburEventSourcing(es =&gt;
/// {
///     es.UsePostgres(pg =&gt;
///     {
///         pg.ConnectionString("Host=localhost;Database=EventStore;...")
///           .EventStoreSchema("public")
///           .EventStoreTable("events")
///           .SnapshotStoreSchema("public")
///           .SnapshotStoreTable("snapshots");
///     })
///     .AddRepository&lt;OrderAggregate, Guid&gt;();
/// });
/// </code>
/// </para>
/// </remarks>
public interface IPostgresEventSourcingBuilder
{
	// --- Connection overloads (canonical 5) ---

	/// <summary>
	/// Sets the Postgres connection string. An <see cref="NpgsqlDataSource"/> is created
	/// internally for proper connection pooling.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	IPostgresEventSourcingBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates an <see cref="NpgsqlDataSource"/>.
	/// Use for advanced scenarios like custom SSL, certificate auth, or dynamic configuration.
	/// </summary>
	/// <param name="dataSourceFactory">
	/// A factory receiving <see cref="IServiceProvider"/> and returning an
	/// <see cref="NpgsqlDataSource"/>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="dataSourceFactory"/> is null.
	/// </exception>
	IPostgresEventSourcingBuilder DataSourceFactory(
		Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory);

	/// <summary>
	/// Sets a pre-configured <see cref="NpgsqlDataSource"/> directly.
	/// The consumer owns the lifecycle of the provided data source.
	/// </summary>
	/// <param name="dataSource">The Npgsql data source.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="dataSource"/> is null.
	/// </exception>
	IPostgresEventSourcingBuilder DataSource(NpgsqlDataSource dataSource);

	/// <summary>
	/// Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at service resolution time. An <see cref="NpgsqlDataSource"/> is created
	/// internally for proper connection pooling.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	IPostgresEventSourcingBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "EventSourcing:Postgres").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	IPostgresEventSourcingBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the schema name for event store tables. Default: "public".
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	IPostgresEventSourcingBuilder EventStoreSchema(string schema);

	/// <summary>
	/// Sets the event store table name. Default: "events".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	IPostgresEventSourcingBuilder EventStoreTable(string tableName);

	/// <summary>
	/// Sets the schema name for snapshot store tables. Default: "public".
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	IPostgresEventSourcingBuilder SnapshotStoreSchema(string schema);

	/// <summary>
	/// Sets the snapshot store table name. Default: "event_store_snapshots".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	IPostgresEventSourcingBuilder SnapshotStoreTable(string tableName);
}
