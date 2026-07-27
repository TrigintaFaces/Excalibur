// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Inbox.Oracle;

/// <summary>
/// Configuration options for the Oracle inbox store.
/// </summary>
public sealed class OracleInboxOptions
{
	/// <summary>
	/// Gets or sets the Oracle connection string.
	/// </summary>
	/// <value>The connection string used to create <c>OracleConnection</c> instances.</value>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for the inbox table.
	/// </summary>
	/// <value>The database schema name. When empty, the connection's default schema is used.</value>
	public string SchemaName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the table name for inbox entries.
	/// </summary>
	/// <value>The table name. Defaults to "INBOX_MESSAGES".</value>
	public string TableName { get; set; } = "INBOX_MESSAGES";

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
	/// Gets the fully qualified table name including schema when configured.
	/// </summary>
	/// <value>The qualified table name in the form "SCHEMA.TABLE", or just "TABLE" when no schema is set.</value>
	public string QualifiedTableName =>
		string.IsNullOrWhiteSpace(SchemaName) ? TableName : $"{SchemaName}.{TableName}";
}
