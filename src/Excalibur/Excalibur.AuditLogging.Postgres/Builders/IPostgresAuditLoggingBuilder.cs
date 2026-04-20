// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.AuditLogging.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres audit logging settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides 5 canonical connection overloads plus subsystem-specific configuration
/// for schema and table names. Connection overloads are mutually exclusive (last-wins).
/// </para>
/// </remarks>
public interface IPostgresAuditLoggingBuilder
{
	// --- Connection overloads (canonical 5) ---

	/// <summary>Sets the Postgres connection string.</summary>
	IPostgresAuditLoggingBuilder ConnectionString(string connectionString);

	/// <summary>Sets a factory function that creates an <see cref="NpgsqlDataSource"/>.</summary>
	IPostgresAuditLoggingBuilder DataSourceFactory(Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory);

	/// <summary>Sets a pre-configured <see cref="NpgsqlDataSource"/> directly.</summary>
	IPostgresAuditLoggingBuilder DataSource(NpgsqlDataSource dataSource);

	/// <summary>Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>.</summary>
	IPostgresAuditLoggingBuilder ConnectionStringName(string name);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IPostgresAuditLoggingBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>Sets the schema name for audit tables. Default: "audit".</summary>
	IPostgresAuditLoggingBuilder SchemaName(string schema);

	/// <summary>Sets the audit events table name. Default: "audit_events".</summary>
	IPostgresAuditLoggingBuilder TableName(string tableName);
}
