// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Abstractions.Validation;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Configuration options for the SQL Server CDC idempotency filter.
/// </summary>
/// <remarks>
/// <para>
/// Controls the schema, table name, retention period, and cleanup behavior
/// for the persistent idempotency filter that tracks processed CDC events
/// in a SQL Server table.
/// </para>
/// </remarks>
public sealed class SqlServerCdcIdempotencyFilterOptions
{
	/// <summary>
	/// Gets or sets the schema name for the processed events table.
	/// </summary>
	/// <value>The schema name. Defaults to "Cdc".</value>
	[Required]
	public string SchemaName { get; set; } = "Cdc";

	/// <summary>
	/// Gets or sets the table name for tracking processed CDC events.
	/// </summary>
	/// <value>The table name. Defaults to "CdcProcessedEvents".</value>
	[Required]
	public string TableName { get; set; } = "CdcProcessedEvents";

	/// <summary>
	/// Gets the fully qualified table name with bracket-escaping.
	/// </summary>
	public string QualifiedTableName => $"[{SchemaName}].[{TableName}]";

	/// <summary>
	/// Gets or sets the retention period for processed event records.
	/// Records older than this are eligible for cleanup.
	/// </summary>
	/// <value>The retention period. Defaults to 24 hours.</value>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets or sets the maximum number of records to delete per cleanup batch.
	/// Prevents long-running DELETE transactions from blocking CDC processing.
	/// </summary>
	/// <value>The cleanup batch size. Defaults to 1000.</value>
	public int CleanupBatchSize { get; set; } = 1000;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when schema/table names are missing or contain invalid characters.
	/// </exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(SchemaName))
		{
			throw new InvalidOperationException("SchemaName is required.");
		}

		if (string.IsNullOrWhiteSpace(TableName))
		{
			throw new InvalidOperationException("TableName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(SchemaName))
		{
			throw new InvalidOperationException(
				$"SchemaName contains invalid characters. Only alphanumeric characters and underscores are allowed: '{SchemaName}'");
		}

		if (!SqlIdentifierValidator.IsValid(TableName))
		{
			throw new InvalidOperationException(
				$"TableName contains invalid characters. Only alphanumeric characters and underscores are allowed: '{TableName}'");
		}

		if (RetentionPeriod <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("RetentionPeriod must be positive.");
		}

		if (CleanupBatchSize <= 0)
		{
			throw new InvalidOperationException("CleanupBatchSize must be positive.");
		}
	}
}
