// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Saga.Oracle;

/// <summary>
/// Fluent builder interface for configuring Oracle saga store settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides the canonical 4 connection overloads plus subsystem-specific configuration for saga schema
/// and table names. Connection overloads are mutually exclusive (last-wins): if multiple connection
/// methods are called, the last one takes effect.
/// </para>
/// <para>The configured connection is shared by the saga store and the timeout store.</para>
/// </remarks>
public interface IOracleSagaBuilder
{
	/// <summary>
	/// Sets the Oracle connection string.
	/// </summary>
	/// <param name="connectionString">The Oracle connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IOracleSagaBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates Oracle connections.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory receiving <see cref="IServiceProvider"/> and returning a
	/// <c>Func&lt;OracleConnection&gt;</c> that creates connections on demand.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	IOracleSagaBuilder ConnectionFactory(
		Func<IServiceProvider, Func<OracleConnection>> connectionFactory);

	/// <summary>
	/// Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at service resolution time.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IOracleSagaBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Saga:Oracle").</param>
	/// <returns>The builder for fluent chaining.</returns>
	IOracleSagaBuilder BindConfiguration(string sectionPath);

	/// <summary>
	/// Sets the schema name for the saga table. Default: "DISPATCH".
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IOracleSagaBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the saga table name. Default: "SAGAS".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IOracleSagaBuilder TableName(string tableName);
}
