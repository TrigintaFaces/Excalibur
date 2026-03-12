// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Configuration options for Postgres CDC state storage.
/// </summary>
public sealed class PostgresCdcStateStoreOptions
{
	/// <summary>Gets or sets the schema name for the CDC state table.</summary>
	[Required]
	public string SchemaName { get; set; } = "excalibur";

	/// <summary>Gets or sets the table name for CDC state.</summary>
	[Required]
	public string TableName { get; set; } = "cdc_state";

	/// <summary>Gets the fully qualified table name.</summary>
	public string QualifiedTableName => $"\"{SchemaName}\".\"{TableName}\"";

	/// <summary>Validates the options and throws if invalid.</summary>
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
