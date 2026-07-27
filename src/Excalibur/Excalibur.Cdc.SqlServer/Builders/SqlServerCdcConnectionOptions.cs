// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Connection-identity options for the SQL Server CDC processor.
/// </summary>
public sealed class SqlServerCdcConnectionOptions
{
	/// <summary>
	/// Gets or sets the connection string for the SQL Server database.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the database name for CDC processing.
	/// </summary>
	/// <value>The database name, or <see langword="null"/> if not configured via the builder.</value>
	public string? DatabaseName { get; set; }

	/// <summary>
	/// Gets or sets the unique identifier for the CDC source database connection.
	/// </summary>
	/// <value>The connection identifier, or <see langword="null"/> if not configured via the builder.</value>
	public string? DatabaseConnectionIdentifier { get; set; }

	/// <summary>
	/// Gets or sets the unique identifier for the state store database connection.
	/// </summary>
	/// <value>The connection identifier, or <see langword="null"/> if not configured via the builder.</value>
	public string? StateConnectionIdentifier { get; set; }
}
