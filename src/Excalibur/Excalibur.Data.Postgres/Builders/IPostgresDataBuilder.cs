// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Data.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres data provider settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides 5 canonical connection overloads following the Postgres builder convention.
/// Connection overloads are mutually exclusive (last-wins).
/// </para>
/// <para>
/// This builder replaces <c>IPostgresPersistenceBuilder</c> with canonical naming
/// consistent with other Postgres subsystem builders.
/// </para>
/// </remarks>
public interface IPostgresDataBuilder
{
	// --- Connection overloads (canonical 5) ---

	/// <summary>Sets the Postgres connection string.</summary>
	IPostgresDataBuilder ConnectionString(string connectionString);

	/// <summary>Sets a factory function that creates an <see cref="NpgsqlDataSource"/>.</summary>
	IPostgresDataBuilder DataSourceFactory(Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory);

	/// <summary>Sets a pre-configured <see cref="NpgsqlDataSource"/> directly.</summary>
	IPostgresDataBuilder DataSource(NpgsqlDataSource dataSource);

	/// <summary>Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>.</summary>
	IPostgresDataBuilder ConnectionStringName(string name);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IPostgresDataBuilder BindConfiguration(string sectionPath);
}
