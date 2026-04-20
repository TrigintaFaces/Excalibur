// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// Fluent builder interface for configuring SQL Server saga store settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides the canonical 4 connection overloads plus subsystem-specific configuration
/// for saga schema and table names. Follows the builder pattern established by
/// <see cref="Excalibur.EventSourcing.SqlServer.ISqlServerEventSourcingBuilder"/>.
/// </para>
/// <para>
/// <b>Connection overloads are mutually exclusive (last-wins):</b> If multiple connection
/// methods are called, the last one takes effect. Each call overwrites any previously
/// configured connection.
/// </para>
/// <para>
/// The configured connection is shared by the saga store, timeout store, and monitoring service.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcalibur(x => x.AddSagas(saga =&gt;
/// {
///     saga.UseSqlServer(sql =&gt;
///     {
///         sql.ConnectionString("Server=...;Database=Sagas;...")
///            .SchemaName("dispatch")
///            .TableName("sagas");
///     })
///     .WithOrchestration()
///     .WithTimeouts();
/// }));
/// </code>
/// </para>
/// </remarks>
public interface ISqlServerSagaBuilder
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
	ISqlServerSagaBuilder ConnectionString(string connectionString);

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
	ISqlServerSagaBuilder ConnectionFactory(
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
	ISqlServerSagaBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Saga:SqlServer").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	ISqlServerSagaBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the schema name for the saga table. Default: "dispatch".
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	ISqlServerSagaBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the saga table name. Default: "sagas".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	ISqlServerSagaBuilder TableName(string tableName);
}
