// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Configures CDC state store persistence settings (schema, table, connection binding).
/// </summary>
/// <remarks>
/// <para>
/// This builder is used with <c>WithStateStore</c> on provider-specific CDC builders
/// (e.g., <c>ISqlServerCdcBuilder</c>, <c>IPostgresCdcBuilder</c>) to configure
/// the state store independently from the CDC source connection.
/// </para>
/// <para>
/// Follows the Microsoft Change Feed Processor pattern where lease/checkpoint storage
/// can be configured separately from the monitored source.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// cdc.UseSqlServer(sql =&gt;
/// {
///     sql.ConnectionString(sourceConnectionString)
///        .WithStateStore(state =&gt;
///        {
///            state.ConnectionString(stateConnectionString)
///                 .SchemaName("dbo")
///                 .TableName("CdcProcessingState");
///        });
/// });
/// </code>
/// </example>
public interface ICdcStateStoreBuilder
{
	/// <summary>
	/// Sets the database schema for the CDC checkpoint table.
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	ICdcStateStoreBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the table name for CDC checkpoint persistence.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	ICdcStateStoreBuilder TableName(string tableName);

	/// <summary>
	/// Sets the state store connection string directly.
	/// </summary>
	/// <param name="connectionString">The state store connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	ICdcStateStoreBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Resolves the state store connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at DI resolution time.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	ICdcStateStoreBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds state store options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">
	/// The configuration section path (e.g., "Cdc:State").
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Uses <c>OptionsBuilder&lt;T&gt;.BindConfiguration()</c> with
	/// <c>ValidateDataAnnotations</c> and <c>ValidateOnStart</c>.
	/// When the bound section contains a <c>ConnectionString</c> property,
	/// it acts as an implicit <c>WithStateStore</c> call.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	ICdcStateStoreBuilder BindConfiguration(string sectionPath);
}
