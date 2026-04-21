// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Fluent builder interface for configuring SQL Server CDC settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface composes three focused sub-interfaces for consumers that need only a subset of capabilities:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="ISqlServerCdcConnectionBuilder"/> — Connection, config binding, and state store configuration.</description></item>
/// <item><description><see cref="ISqlServerCdcProcessingBuilder"/> — Schema, table, polling, batch, and timeout settings.</description></item>
/// <item><description><see cref="ISqlServerCdcDatabaseBuilder"/> — Database naming, identifiers, capture instances, and handler behavior.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// cdc.UseSqlServer(sql =>
/// {
///     sql.ConnectionString(connectionString)
///        .SchemaName("cdc")
///        .StateTableName("CdcProcessingState")
///        .PollingInterval(TimeSpan.FromSeconds(5))
///        .BatchSize(100)
///        .CommandTimeout(TimeSpan.FromSeconds(30));
/// });
/// </code>
/// </example>
public interface ISqlServerCdcBuilder
	: ISqlServerCdcConnectionBuilder,
	  ISqlServerCdcProcessingBuilder,
	  ISqlServerCdcDatabaseBuilder
{
}
