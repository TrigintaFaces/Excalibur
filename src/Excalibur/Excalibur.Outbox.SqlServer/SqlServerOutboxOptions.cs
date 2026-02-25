// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Configuration options for SQL Server-based outbox message storage.
/// </summary>
public class SqlServerOutboxOptions
{
	/// <summary>
	/// Gets or sets the connection string for the SQL Server database.
	/// </summary>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the application name used for connection pool isolation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When set, this value is applied to the <c>Application Name</c> property of the
	/// connection string. ADO.NET creates separate connection pools for connections with
	/// different <c>Application Name</c> values, providing pool isolation between subsystems.
	/// </para>
	/// <para>
	/// For example, setting this to <c>"Excalibur.Outbox"</c> ensures the outbox uses a
	/// separate pool from the event store (<c>"Excalibur.EventStore"</c>) even when they
	/// share the same server and database.
	/// </para>
	/// </remarks>
	/// <value>The application name for pool isolation. Defaults to "Excalibur.Outbox".</value>
	public string ApplicationName { get; set; } = "Excalibur.Outbox";

	/// <summary>
	/// Gets or sets the name of the database table used for storing outbox messages.
	/// </summary>
	/// <value>The table name for outbox messages. Defaults to "OutboxMessages".</value>
	public string OutboxTableName { get; set; } = "OutboxMessages";

	/// <summary>
	/// Gets or sets the name of the database table used for storing transport delivery records.
	/// </summary>
	/// <value>The table name for transport deliveries. Defaults to "OutboxMessageTransports".</value>
	public string TransportsTableName { get; set; } = "OutboxMessageTransports";

	/// <summary>
	/// Gets or sets the name of the database table used for storing dead letter messages.
	/// </summary>
	/// <value>The table name for dead letter messages. Defaults to "OutboxDeadLetters".</value>
	public string DeadLetterTableName { get; set; } = "OutboxDeadLetters";

	/// <summary>
	/// Gets or sets the command timeout in seconds for SQL operations.
	/// </summary>
	/// <value>The command timeout in seconds. Defaults to 30.</value>
	[Range(1, int.MaxValue)]
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the default batch size for retrieving messages.
	/// </summary>
	/// <value>The default batch size. Defaults to 100.</value>
	[Range(1, int.MaxValue)]
	public int DefaultBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for failed messages.
	/// </summary>
	/// <value>The maximum retry count. Defaults to 3.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay in minutes between retry attempts.
	/// </summary>
	/// <value>The retry delay in minutes. Defaults to 5.</value>
	[Range(1, int.MaxValue)]
	public int RetryDelayMinutes { get; set; } = 5;

	/// <summary>
	/// Gets or sets whether to use row-level locking for concurrent access.
	/// </summary>
	/// <value>True to enable row-level locking. Defaults to true.</value>
	public bool UseRowLocking { get; set; } = true;

	/// <summary>
	/// Gets or sets the schema name for the outbox tables.
	/// </summary>
	/// <value>The schema name. Defaults to "dbo".</value>
	public string SchemaName { get; set; } = "dbo";

	/// <summary>
	/// Gets the fully qualified outbox table name.
	/// </summary>
	public string QualifiedOutboxTableName => $"[{SchemaName}].[{OutboxTableName}]";

	/// <summary>
	/// Gets the fully qualified transports table name.
	/// </summary>
	public string QualifiedTransportsTableName => $"[{SchemaName}].[{TransportsTableName}]";

	/// <summary>
	/// Gets the fully qualified dead letter table name.
	/// </summary>
	public string QualifiedDeadLetterTableName => $"[{SchemaName}].[{DeadLetterTableName}]";
}
