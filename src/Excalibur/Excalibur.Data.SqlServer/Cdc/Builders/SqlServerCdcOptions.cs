// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Configuration options for SQL Server CDC processor.
/// </summary>
public sealed class SqlServerCdcOptions
{
	/// <summary>
	/// Gets or sets the schema name for CDC state tables.
	/// </summary>
	/// <value>The schema name. Default is "Cdc".</value>
	[Required]
	public string SchemaName { get; set; } = "Cdc";

	/// <summary>
	/// Gets or sets the table name for CDC processing state.
	/// </summary>
	/// <value>The table name. Default is "CdcProcessingState".</value>
	[Required]
	public string StateTableName { get; set; } = "CdcProcessingState";

	/// <summary>
	/// Gets the fully qualified table name for CDC state.
	/// </summary>
	public string QualifiedTableName => $"[{SchemaName}].[{StateTableName}]";

	/// <summary>
	/// Gets or sets the polling interval for CDC change detection.
	/// </summary>
	/// <value>The polling interval. Default is 5 seconds.</value>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the batch size for CDC change processing.
	/// </summary>
	/// <value>The batch size. Default is 100.</value>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the command timeout for database operations.
	/// </summary>
	/// <value>The command timeout. Default is 30 seconds.</value>
	public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the connection string for the SQL Server database.
	/// </summary>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(SchemaName))
		{
			throw new InvalidOperationException("SchemaName is required.");
		}

		if (string.IsNullOrWhiteSpace(StateTableName))
		{
			throw new InvalidOperationException("StateTableName is required.");
		}

		if (PollingInterval <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("PollingInterval must be positive.");
		}

		if (BatchSize <= 0)
		{
			throw new InvalidOperationException("BatchSize must be positive.");
		}

		if (CommandTimeout <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("CommandTimeout must be positive.");
		}
	}
}
