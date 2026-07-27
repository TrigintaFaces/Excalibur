// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Validation;

namespace Excalibur.Saga.Oracle;

/// <summary>
/// Configuration options for Oracle saga state storage.
/// </summary>
public sealed class OracleSagaStoreOptions
{
	/// <summary>
	/// Gets or sets the Oracle connection string.
	/// </summary>
	/// <value>The connection string, or <see langword="null"/> when a connection factory,
	/// connection-string name, or bound configuration supplies the connection.</value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for the saga table.
	/// </summary>
	/// <value>The schema name. Defaults to "DISPATCH".</value>
	[Required]
	public string SchemaName { get; set; } = "DISPATCH";

	/// <summary>
	/// Gets or sets the table name for saga entries.
	/// </summary>
	/// <value>The table name. Defaults to "SAGAS".</value>
	[Required]
	public string TableName { get; set; } = "SAGAS";

	/// <summary>
	/// Gets the fully qualified table name (Oracle uses unquoted <c>SCHEMA.TABLE</c>).
	/// </summary>
	public string QualifiedTableName => $"{SchemaName}.{TableName}";

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(SchemaName))
		{
			throw new InvalidOperationException("SchemaName is required.");
		}

		SqlIdentifierValidator.ThrowIfInvalid(SchemaName, nameof(SchemaName));

		if (string.IsNullOrWhiteSpace(TableName))
		{
			throw new InvalidOperationException("TableName is required.");
		}

		SqlIdentifierValidator.ThrowIfInvalid(TableName, nameof(TableName));
	}
}
