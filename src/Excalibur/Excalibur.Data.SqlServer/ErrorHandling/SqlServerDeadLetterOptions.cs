// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.SqlServer.ErrorHandling;

/// <summary>
/// Configuration options for SQL Server dead letter store.
/// </summary>
public sealed class SqlServerDeadLetterOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	/// <value>The connection string for SQL Server.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the database schema for dead letter tables.
	/// </summary>
	/// <value>The schema name. Defaults to "dbo".</value>
	[Required]
	public string SchemaName { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the table name for dead letter messages.
	/// </summary>
	/// <value>The table name. Defaults to "DeadLetterMessages".</value>
	[Required]
	public string TableName { get; set; } = "DeadLetterMessages";
}
