// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Saga.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres saga store settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides 5 canonical connection overloads plus subsystem-specific configuration
/// for schema and table names. Connection overloads are mutually exclusive (last-wins).
/// </para>
/// <para>
/// <b>All connection paths converge to <see cref="NpgsqlDataSource"/>:</b>
/// <see cref="ConnectionString(string)"/> and <see cref="ConnectionStringName(string)"/>
/// create an <see cref="NpgsqlDataSource"/> internally for proper connection pooling.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcaliburSagas(saga =&gt;
/// {
///     saga.UsePostgres(pg =&gt;
///     {
///         pg.ConnectionString("Host=localhost;Database=MyApp;")
///           .SchemaName("dispatch")
///           .TableName("sagas");
///     });
/// });
/// </code>
/// </para>
/// </remarks>
public interface IPostgresSagaBuilder
{
	// --- Connection overloads (canonical 5) ---

	/// <summary>
	/// Sets the Postgres connection string.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresSagaBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates an <see cref="NpgsqlDataSource"/>.
	/// </summary>
	/// <param name="dataSourceFactory">
	/// A factory receiving <see cref="IServiceProvider"/> and returning an <see cref="NpgsqlDataSource"/>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresSagaBuilder DataSourceFactory(Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory);

	/// <summary>
	/// Sets a pre-configured <see cref="NpgsqlDataSource"/> directly.
	/// </summary>
	/// <param name="dataSource">The Npgsql data source.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresSagaBuilder DataSource(NpgsqlDataSource dataSource);

	/// <summary>
	/// Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>.
	/// </summary>
	/// <param name="name">The connection string name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresSagaBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresSagaBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the schema name for the saga table. Default: "dispatch".
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresSagaBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the saga table name. Default: "sagas".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresSagaBuilder TableName(string tableName);
}
