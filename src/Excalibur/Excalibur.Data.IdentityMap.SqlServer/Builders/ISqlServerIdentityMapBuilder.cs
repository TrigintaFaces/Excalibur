// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap.SqlServer.Builders;

/// <summary>
/// Fluent builder interface for configuring the SQL Server identity map store.
/// </summary>
/// <example>
/// <code>
/// identity.UseSqlServer(sql =>
/// {
///     sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
///        .SchemaName("dbo")
///        .TableName("IdentityMap")
///        .CommandTimeout(TimeSpan.FromSeconds(60))
///        .MaxBatchSize(200);
/// });
/// </code>
/// </example>
public interface ISqlServerIdentityMapBuilder
{
	/// <summary>
	/// Sets the SQL Server connection string.
	/// </summary>
	/// <param name="connectionString">The connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets the database schema name for the identity map table.
	/// </summary>
	/// <param name="schemaName">The schema name. Defaults to "dbo".</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder SchemaName(string schemaName);

	/// <summary>
	/// Sets the identity map table name.
	/// </summary>
	/// <param name="tableName">The table name. Defaults to "IdentityMap".</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder TableName(string tableName);

	/// <summary>
	/// Sets the command timeout.
	/// </summary>
	/// <param name="timeout">The command timeout.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder CommandTimeout(TimeSpan timeout);

	/// <summary>
	/// Sets the maximum number of items in a single batch resolve operation.
	/// </summary>
	/// <param name="maxBatchSize">The maximum batch size. Defaults to 100.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder MaxBatchSize(int maxBatchSize);
}
