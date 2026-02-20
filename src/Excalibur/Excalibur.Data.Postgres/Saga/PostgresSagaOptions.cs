// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.Saga;

/// <summary>
/// Configuration options for the Postgres saga store.
/// </summary>
/// <remarks>
/// <para>
/// The saga store uses Postgres's JSONB column type for efficient saga state storage,
/// enabling fast reads and writes while preserving the full state structure.
/// </para>
/// <para>
/// The default configuration uses the "dispatch" schema with a "sagas" table,
/// following Postgres naming conventions with snake_case column names.
/// </para>
/// </remarks>
public class PostgresSagaOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	/// <value>The connection string for the Postgres database.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for the saga table.
	/// </summary>
	/// <value>The database schema name. Defaults to "dispatch".</value>
	[Required]
	public string Schema { get; set; } = "dispatch";

	/// <summary>
	/// Gets or sets the table name for saga entries.
	/// </summary>
	/// <value>The table name. Defaults to "sagas".</value>
	[Required]
	public string TableName { get; set; } = "sagas";

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value>The timeout duration for database commands. Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets the fully qualified table name including schema.
	/// </summary>
	/// <value>The qualified table name in format "schema"."table".</value>
	public string QualifiedTableName => $"\"{Schema}\".\"{TableName}\"";

	/// <summary>
	/// Validates the configuration options.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// Thrown when <see cref="ConnectionString"/>, <see cref="Schema"/>, or <see cref="TableName"/> is null or whitespace.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <see cref="CommandTimeoutSeconds"/> is less than or equal to zero.
	/// </exception>
	public void Validate()
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(Schema);
		ArgumentException.ThrowIfNullOrWhiteSpace(TableName);

		if (CommandTimeoutSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(CommandTimeoutSeconds),
				CommandTimeoutSeconds,
				"Command timeout must be greater than zero.");
		}
	}
}
