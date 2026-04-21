// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.AuditLogging.SqlServer;

/// <summary>
/// Configuration options for the SQL Server audit annotation store.
/// </summary>
public sealed class SqlServerAuditAnnotationStoreOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for the annotations table. Default is "audit".
	/// </summary>
	public string SchemaName { get; set; } = "audit";

	/// <summary>
	/// Gets or sets the annotations table name. Default is "AuditAnnotations".
	/// </summary>
	public string TableName { get; set; } = "AuditAnnotations";

	/// <summary>
	/// Gets or sets the command timeout for SQL operations in seconds. Default is 30.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets the fully qualified table name.
	/// </summary>
	public string FullyQualifiedTableName => $"[{SchemaName}].[{TableName}]";
}
