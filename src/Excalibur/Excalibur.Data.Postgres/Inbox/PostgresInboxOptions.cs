// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.Inbox;

/// <summary>
/// Configuration options for the Postgres inbox store.
/// </summary>
public sealed class PostgresInboxOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	/// <value>The connection string for the Postgres database.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for the inbox table.
	/// </summary>
	/// <value>The database schema name. Defaults to "public".</value>
	[Required]
	public string SchemaName { get; set; } = "public";

	/// <summary>
	/// Gets or sets the table name for inbox entries.
	/// </summary>
	/// <value>The table name. Defaults to "inbox_messages".</value>
	[Required]
	public string TableName { get; set; } = "inbox_messages";

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value>The timeout duration. Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for failed messages.
	/// </summary>
	/// <value>The maximum retry count. Defaults to 3.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryCount { get; set; } = 3;

	/// <summary>
	/// Gets the fully qualified table name including schema.
	/// </summary>
	/// <value>The qualified table name in format "schema.table".</value>
	public string QualifiedTableName => $"\"{SchemaName}\".\"{TableName}\"";
}
