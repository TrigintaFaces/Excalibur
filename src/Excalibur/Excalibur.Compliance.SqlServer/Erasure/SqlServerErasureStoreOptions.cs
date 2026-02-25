// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.Validation;

namespace Excalibur.Compliance.SqlServer.Erasure;

/// <summary>
/// Configuration options for the SQL Server erasure store.
/// </summary>
public sealed class SqlServerErasureStoreOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for erasure tables.
	/// </summary>
	public string SchemaName { get; set; } = "compliance";

	/// <summary>
	/// Gets or sets the erasure requests table name.
	/// </summary>
	public string RequestsTableName { get; set; } = "ErasureRequests";

	/// <summary>
	/// Gets or sets the erasure certificates table name.
	/// </summary>
	public string CertificatesTableName { get; set; } = "ErasureCertificates";

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets whether to auto-create the schema and tables on startup.
	/// </summary>
	public bool AutoCreateSchema { get; set; } = true;

	/// <summary>
	/// Gets the full requests table name including schema.
	/// </summary>
	public string FullRequestsTableName => $"[{SchemaName}].[{RequestsTableName}]";

	/// <summary>
	/// Gets the full certificates table name including schema.
	/// </summary>
	public string FullCertificatesTableName => $"[{SchemaName}].[{CertificatesTableName}]";

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when options are invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(Resources.SqlServerErasureStoreOptions_ConnectionStringRequired);
		}

		if (string.IsNullOrWhiteSpace(SchemaName))
		{
			throw new InvalidOperationException(Resources.SqlServerErasureStoreOptions_SchemaNameEmpty);
		}

		if (string.IsNullOrWhiteSpace(RequestsTableName))
		{
			throw new InvalidOperationException(Resources.SqlServerErasureStoreOptions_RequestsTableNameEmpty);
		}

		if (string.IsNullOrWhiteSpace(CertificatesTableName))
		{
			throw new InvalidOperationException(Resources.SqlServerErasureStoreOptions_CertificatesTableNameEmpty);
		}

		if (CommandTimeoutSeconds <= 0)
		{
			throw new InvalidOperationException(Resources.SqlServerErasureStoreOptions_CommandTimeoutPositive);
		}

		if (!SqlIdentifierValidator.IsValid(SchemaName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(SchemaName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (!SqlIdentifierValidator.IsValid(RequestsTableName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(RequestsTableName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (!SqlIdentifierValidator.IsValid(CertificatesTableName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(CertificatesTableName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}
	}
}
