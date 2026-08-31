// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Validation;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Configuration options for SQL Server-based dead letter queue storage.
/// </summary>
public sealed class SqlServerDeadLetterQueueOptions
{
	/// <summary>
	/// Gets or sets the connection string for the SQL Server database.
	/// </summary>
	[Required]
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
	/// <remarks>
	/// Both identifiers are whitelisted through <see cref="SqlIdentifierValidator"/> before they are
	/// bracketed. An identifier cannot be parameterized, so validation is the only control available:
	/// bracket quoting alone does not contain a name carrying a <c>]</c>, which closes the quoting and
	/// leaves the remainder to be read as SQL. The value is the application's own configuration rather
	/// than request data, so this is defense-in-depth - and it is the same routing every event-store
	/// sibling already uses, which is the invariant the dead-letter queue was the sole exception to.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <see cref="SchemaName"/> or <see cref="TableName"/> is not a valid SQL identifier.
	/// </exception>
	public string QualifiedTableName
	{
		get
		{
			SqlIdentifierValidator.ThrowIfInvalid(SchemaName, nameof(SchemaName));
			SqlIdentifierValidator.ThrowIfInvalid(TableName, nameof(TableName));
			return $"[{SchemaName}].[{TableName}]";
		}
	}
}
