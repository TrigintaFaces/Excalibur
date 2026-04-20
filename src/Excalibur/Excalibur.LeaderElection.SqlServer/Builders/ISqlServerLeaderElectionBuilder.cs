// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>
/// Fluent builder interface for configuring SQL Server leader election settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides the canonical 4 connection overloads plus <see cref="LockResource"/>
/// configuration. Follows the builder pattern established by
/// <see cref="Excalibur.Cdc.SqlServer.ISqlServerCdcConnectionBuilder"/>.
/// </para>
/// <para>
/// <b>Connection overloads are mutually exclusive (last-wins):</b> If multiple connection
/// methods are called, the last one takes effect. Each call overwrites any previously
/// configured connection.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcalibur(x => x.AddLeaderElection(le =&gt;
///     le.UseSqlServer(sql =&gt;
///     {
///         sql.ConnectionString("Server=...;Database=...")
///            .LockResource("MyApp.Leader");
///     })
///     .WithHealthChecks()));
/// </code>
/// </para>
/// </remarks>
public interface ISqlServerLeaderElectionBuilder
{
	// --- Connection overloads (canonical 4) ---

	/// <summary>
	/// Sets the SQL Server connection string.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	ISqlServerLeaderElectionBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates SQL connections.
	/// Use for Azure Managed Identity, Key Vault, or custom connection pooling.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory receiving <see cref="IServiceProvider"/> and returning a
	/// <c>Func&lt;SqlConnection&gt;</c> that creates connections on demand.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="connectionFactory"/> is null.
	/// </exception>
	ISqlServerLeaderElectionBuilder ConnectionFactory(
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory);

	/// <summary>
	/// Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at service resolution time.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	ISqlServerLeaderElectionBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "LeaderElection:SqlServer").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	ISqlServerLeaderElectionBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the lock resource name for SQL Server application locks. Required.
	/// </summary>
	/// <param name="lockResource">
	/// The lock resource name (e.g., "MyApp.Leader"). Must be non-null and non-whitespace.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="lockResource"/> is null or whitespace.
	/// </exception>
	ISqlServerLeaderElectionBuilder LockResource(string lockResource);
}
