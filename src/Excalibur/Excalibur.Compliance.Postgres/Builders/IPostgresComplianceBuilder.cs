// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Compliance.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres compliance settings.
/// </summary>
/// <remarks>
/// <para>
/// Unifies connection configuration for Erasure, DataInventory, and LegalHold stores.
/// All three sub-stores share the same connection. Provides 5 canonical connection overloads.
/// Connection overloads are mutually exclusive (last-wins).
/// </para>
/// </remarks>
public interface IPostgresComplianceBuilder
{
	// --- Connection overloads (canonical 5) ---

	/// <summary>Sets the Postgres connection string.</summary>
	IPostgresComplianceBuilder ConnectionString(string connectionString);

	/// <summary>Sets a factory function that creates an <see cref="NpgsqlDataSource"/>.</summary>
	IPostgresComplianceBuilder DataSourceFactory(Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory);

	/// <summary>Sets a pre-configured <see cref="NpgsqlDataSource"/> directly.</summary>
	IPostgresComplianceBuilder DataSource(NpgsqlDataSource dataSource);

	/// <summary>Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>.</summary>
	IPostgresComplianceBuilder ConnectionStringName(string name);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IPostgresComplianceBuilder BindConfiguration(string sectionPath);
}
