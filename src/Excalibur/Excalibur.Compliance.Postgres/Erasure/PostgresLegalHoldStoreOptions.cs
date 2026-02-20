// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Abstractions.Validation;

namespace Excalibur.Compliance.Postgres.Erasure;

/// <summary>
/// Configuration options for the Postgres legal hold store.
/// </summary>
public sealed class PostgresLegalHoldStoreOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for legal hold tables.
	/// </summary>
	[Required]
	public string SchemaName { get; set; } = "compliance";

	/// <summary>
	/// Gets or sets the legal holds table name.
	/// </summary>
	[Required]
	public string TableName { get; set; } = "legal_holds";

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	[Range(1, 3600)]
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets whether to auto-create the schema and tables on startup.
	/// </summary>
	public bool AutoCreateSchema { get; set; } = true;

	/// <summary>
	/// Gets the full table name including schema.
	/// </summary>
	public string FullTableName => $"\"{SchemaName}\".\"{TableName}\"";

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when options are invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required for PostgresLegalHoldStore.");
		}

		if (string.IsNullOrWhiteSpace(SchemaName))
		{
			throw new InvalidOperationException("SchemaName cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(TableName))
		{
			throw new InvalidOperationException("TableName cannot be empty.");
		}

		if (CommandTimeoutSeconds <= 0)
		{
			throw new InvalidOperationException("CommandTimeoutSeconds must be positive.");
		}

		if (!SqlIdentifierValidator.IsValid(SchemaName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(SchemaName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (!SqlIdentifierValidator.IsValid(TableName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(TableName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}
	}
}
