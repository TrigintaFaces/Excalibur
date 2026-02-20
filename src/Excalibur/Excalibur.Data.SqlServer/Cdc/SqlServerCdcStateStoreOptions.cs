// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Abstractions.Validation;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Configuration options for SQL Server CDC state storage.
/// </summary>
public sealed class SqlServerCdcStateStoreOptions
{
	/// <summary>
	/// Gets or sets the schema name for CDC state tables.
	/// </summary>
	/// <value>The schema name. Defaults to "Cdc".</value>
	[Required]
	public string SchemaName { get; set; } = "Cdc";

	/// <summary>
	/// Gets or sets the table name for CDC processing state.
	/// </summary>
	/// <value>The table name. Defaults to "CdcProcessingState".</value>
	[Required]
	public string TableName { get; set; } = "CdcProcessingState";

	/// <summary>
	/// Gets the fully qualified table name.
	/// </summary>
	public string QualifiedTableName => $"[{SchemaName}].[{TableName}]";

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
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
	}
}
