// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Validation;

namespace Excalibur.Compliance.SqlServer.Erasure;

/// <summary>
/// Configuration options for the SQL Server data inventory store.
/// </summary>
public sealed class SqlServerDataInventoryStoreOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for data inventory tables.
	/// </summary>
	public string SchemaName { get; set; } = "compliance";

	/// <summary>
	/// Gets or sets the registrations table name.
	/// </summary>
	public string RegistrationsTableName { get; set; } = "DataInventoryRegistrations";

	/// <summary>
	/// Gets or sets the discovered locations table name.
	/// </summary>
	public string DiscoveredLocationsTableName { get; set; } = "DiscoveredDataLocations";

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets whether to auto-create the schema and tables on startup.
	/// </summary>
	public bool AutoCreateSchema { get; set; } = true;

	/// <summary>
	/// Gets the full registrations table name including schema.
	/// </summary>
	public string FullRegistrationsTableName => $"[{SchemaName}].[{RegistrationsTableName}]";

	/// <summary>
	/// Gets the full discovered locations table name including schema.
	/// </summary>
	public string FullDiscoveredLocationsTableName => $"[{SchemaName}].[{DiscoveredLocationsTableName}]";

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when options are invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required for SqlServerDataInventoryStore.");
		}

		if (string.IsNullOrWhiteSpace(SchemaName))
		{
			throw new InvalidOperationException("SchemaName cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(RegistrationsTableName))
		{
			throw new InvalidOperationException("RegistrationsTableName cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(DiscoveredLocationsTableName))
		{
			throw new InvalidOperationException("DiscoveredLocationsTableName cannot be empty.");
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

		if (!SqlIdentifierValidator.IsValid(RegistrationsTableName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(RegistrationsTableName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (!SqlIdentifierValidator.IsValid(DiscoveredLocationsTableName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(DiscoveredLocationsTableName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}
	}
}
