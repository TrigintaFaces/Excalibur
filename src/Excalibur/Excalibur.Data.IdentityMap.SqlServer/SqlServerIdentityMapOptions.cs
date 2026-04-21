// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.IdentityMap.SqlServer;

/// <summary>
/// Configuration options for the SQL Server identity map store.
/// </summary>
internal sealed class SqlServerIdentityMapOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the database schema name for the identity map table.
	/// </summary>
	public string SchemaName { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the identity map table name.
	/// </summary>
	public string TableName { get; set; } = "IdentityMap";

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the maximum number of items in a single batch resolve operation.
	/// Batches exceeding this limit are chunked into multiple queries.
	/// </summary>
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets the fully qualified table name with bracket escaping.
	/// </summary>
	internal string QualifiedTableName => $"[{SchemaName}].[{TableName}]";
}
