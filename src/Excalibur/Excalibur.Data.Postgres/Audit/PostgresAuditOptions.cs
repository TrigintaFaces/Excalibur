// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.Audit;

/// <summary>
/// Configuration options for the Postgres audit store.
/// </summary>
public sealed class PostgresAuditOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for audit tables.
	/// </summary>
	[Required]
	public string SchemaName { get; set; } = "audit";

	/// <summary>
	/// Gets or sets the table name for audit events.
	/// </summary>
	[Required]
	public string TableName { get; set; } = "audit_events";

	/// <summary>
	/// Gets or sets a value indicating whether to automatically create the schema and table.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool AutoCreateTable { get; set; } = true;

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required.");
		}

		if (string.IsNullOrWhiteSpace(SchemaName))
		{
			throw new InvalidOperationException("SchemaName is required.");
		}

		if (string.IsNullOrWhiteSpace(TableName))
		{
			throw new InvalidOperationException("TableName is required.");
		}
	}
}
