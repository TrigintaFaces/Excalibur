// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// Configuration options for the SQL Server projection store.
/// </summary>
/// <remarks>
/// <para>
/// Supports connection string or connection factory patterns.
/// The projection store uses JSON serialization for projection data with MERGE for upserts.
/// </para>
/// </remarks>
public sealed class SqlServerProjectionStoreOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the table name for projections.
	/// </summary>
	/// <remarks>
	/// When <see langword="null"/>, defaults to the projection type name.
	/// </remarks>
	public string? TableName { get; set; }

	/// <summary>
	/// Gets or sets the schema holding the projection table.
	/// </summary>
	/// <value>The schema name. Defaults to <c>dbo</c>.</value>
	/// <remarks>
	/// <b>Honoured by every statement the store runs, which is the only form of this setting worth
	/// having.</b> The store previously addressed the table unqualified, so it resolved through the
	/// connection's default schema — and a consumer who created the table in a named schema got a table
	/// the store could not find, with no error until the first read. A schema setting the data access
	/// does not use is worse than none, because it reads as supported.
	/// </remarks>
	public string SchemaName { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the JSON serializer options for projection data.
	/// </summary>
	/// <remarks>
	/// When <see langword="null"/>, defaults to camelCase naming with no indentation.
	/// </remarks>
	public JsonSerializerOptions? JsonSerializerOptions { get; set; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(
				"A connection string is required for the SQL Server projection store. " +
				"Set the ConnectionString property or use the overload that accepts a connection string directly.");
		}
	}
}
