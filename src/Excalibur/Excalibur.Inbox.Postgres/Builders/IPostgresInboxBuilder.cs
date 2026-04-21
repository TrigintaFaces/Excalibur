// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Inbox.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres inbox store settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides 5 canonical connection overloads plus subsystem-specific configuration
/// for schema, table, and retry settings. Connection overloads are mutually exclusive (last-wins).
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcaliburInbox(inbox =&gt;
/// {
///     inbox.UsePostgres(pg =&gt;
///     {
///         pg.ConnectionString("Host=localhost;Database=MyApp;")
///           .SchemaName("public")
///           .TableName("inbox_messages");
///     });
/// });
/// </code>
/// </para>
/// </remarks>
public interface IPostgresInboxBuilder
{
	// --- Connection overloads (canonical 5) ---

	/// <summary>
	/// Sets the Postgres connection string.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresInboxBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates an <see cref="NpgsqlDataSource"/>.
	/// </summary>
	/// <param name="dataSourceFactory">
	/// A factory receiving <see cref="IServiceProvider"/> and returning an <see cref="NpgsqlDataSource"/>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresInboxBuilder DataSourceFactory(Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory);

	/// <summary>
	/// Sets a pre-configured <see cref="NpgsqlDataSource"/> directly.
	/// </summary>
	/// <param name="dataSource">The Npgsql data source.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresInboxBuilder DataSource(NpgsqlDataSource dataSource);

	/// <summary>
	/// Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>.
	/// </summary>
	/// <param name="name">The connection string name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresInboxBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresInboxBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the schema name for the inbox table. Default: "public".
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresInboxBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the inbox table name. Default: "inbox_messages".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresInboxBuilder TableName(string tableName);

	/// <summary>
	/// Sets the maximum retry count for failed messages. Default: 3.
	/// </summary>
	/// <param name="maxRetryCount">The maximum number of retries.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresInboxBuilder MaxRetryCount(int maxRetryCount);
}
