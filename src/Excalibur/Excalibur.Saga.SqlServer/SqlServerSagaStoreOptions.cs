// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// Configuration options for SQL Server saga state storage.
/// </summary>
public sealed class SqlServerSagaStoreOptions
{
	/// <summary>
	/// Gets or sets the schema name for the saga table.
	/// </summary>
	/// <value>The schema name. Defaults to "dispatch".</value>
	public string SchemaName { get; set; } = "dispatch";

	/// <summary>
	/// Gets or sets the table name for saga entries.
	/// </summary>
	/// <value>The table name. Defaults to "sagas".</value>
	public string TableName { get; set; } = "sagas";

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
	}
}
