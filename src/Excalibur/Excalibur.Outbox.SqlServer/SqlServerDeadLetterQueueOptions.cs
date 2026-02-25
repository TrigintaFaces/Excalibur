// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Configuration options for SQL Server-based dead letter queue storage.
/// </summary>
public class SqlServerDeadLetterQueueOptions
{
	/// <summary>
	/// Gets or sets the connection string for the SQL Server database.
	/// </summary>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the application name used for connection pool isolation.
	/// </summary>
	/// <remarks>
	/// When set, this value is applied to the <c>Application Name</c> property of the
	/// connection string to provide separate ADO.NET connection pools per subsystem.
	/// </remarks>
	/// <value>The application name for pool isolation. Defaults to "Excalibur.DeadLetterQueue".</value>
	public string ApplicationName { get; set; } = "Excalibur.DeadLetterQueue";

	/// <summary>
	/// Gets or sets the name of the database table used for storing dead letter entries.
	/// </summary>
	/// <value>The table name for dead letter entries. Defaults to "DeadLetterQueue".</value>
	public string TableName { get; set; } = "DeadLetterQueue";

	/// <summary>
	/// Gets or sets the schema name for the dead letter queue table.
	/// </summary>
	/// <value>The schema name. Defaults to "dbo".</value>
	public string SchemaName { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the command timeout in seconds for SQL operations.
	/// </summary>
	/// <value>The command timeout in seconds. Defaults to 30.</value>
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the default retention period for dead letter entries.
	/// Entries older than this will be purged by cleanup operations.
	/// </summary>
	/// <value>The default retention period. Defaults to 30 days.</value>
	public TimeSpan DefaultRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

	/// <summary>
	/// Gets the fully qualified table name.
	/// </summary>
	public string QualifiedTableName => $"[{SchemaName}].[{TableName}]";
}
