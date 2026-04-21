// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.LeaderElection.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres leader election settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides 5 canonical connection overloads plus a <see cref="LockKey(long)"/> method
/// for the advisory lock key. Connection overloads are mutually exclusive (last-wins).
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcalibur(excalibur =&gt;
/// {
///     excalibur.AddLeaderElection(le =&gt;
///     {
///         le.UsePostgres(pg =&gt;
///         {
///             pg.ConnectionString("Host=localhost;Database=MyApp;")
///               .LockKey(42);
///         });
///     });
/// });
/// </code>
/// </para>
/// </remarks>
public interface IPostgresLeaderElectionBuilder
{
	// --- Connection overloads (canonical 5) ---

	/// <summary>
	/// Sets the Postgres connection string.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresLeaderElectionBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates an <see cref="NpgsqlDataSource"/>.
	/// </summary>
	/// <param name="dataSourceFactory">
	/// A factory receiving <see cref="IServiceProvider"/> and returning an <see cref="NpgsqlDataSource"/>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresLeaderElectionBuilder DataSourceFactory(Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory);

	/// <summary>
	/// Sets a pre-configured <see cref="NpgsqlDataSource"/> directly.
	/// </summary>
	/// <param name="dataSource">The Npgsql data source.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresLeaderElectionBuilder DataSource(NpgsqlDataSource dataSource);

	/// <summary>
	/// Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>.
	/// </summary>
	/// <param name="name">The connection string name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresLeaderElectionBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresLeaderElectionBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the advisory lock key for leader election. Default: 1.
	/// </summary>
	/// <param name="lockKey">The advisory lock key (must be positive).</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresLeaderElectionBuilder LockKey(long lockKey);
}
