// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Defines the contract for configuring Postgres CDC source and state store connections.
/// </summary>
public interface IPostgresCdcConnectionBuilder
{
	/// <summary>
	/// Sets the connection string for the Postgres CDC source database.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	IPostgresCdcBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates Postgres connections for the CDC source database.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates Postgres connections.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="connectionFactory"/> is null.
	/// </exception>
	IPostgresCdcBuilder ConnectionFactory(Func<IServiceProvider, Func<NpgsqlConnection>> connectionFactory);

	/// <summary>
	/// Resolves the source connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at registration time.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	IPostgresCdcBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds Postgres CDC source options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Cdc:Source").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	IPostgresCdcBuilder BindConfiguration(string sectionPath);

	/// <summary>
	/// Configures a separate connection for CDC state persistence.
	/// </summary>
	/// <param name="configure">An action to configure state store connection, schema, and table settings.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	IPostgresCdcBuilder WithStateStore(Action<ICdcStateStoreBuilder> configure);

}
