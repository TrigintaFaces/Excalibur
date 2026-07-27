// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Validation;

namespace Excalibur.Saga.Oracle;

/// <summary>
/// Configuration options for Oracle saga timeout storage.
/// </summary>
public sealed class OracleSagaTimeoutStoreOptions
{
	/// <summary>
	/// Gets or sets the Oracle connection string.
	/// </summary>
	/// <value>The connection string. Required.</value>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for the saga timeout table.
	/// </summary>
	/// <value>The schema name. Defaults to "DISPATCH".</value>
	[Required]
	public string SchemaName { get; set; } = "DISPATCH";

	/// <summary>
	/// Gets or sets the table name for saga timeouts.
	/// </summary>
	/// <value>The table name. Defaults to "SAGATIMEOUTS".</value>
	[Required]
	public string TableName { get; set; } = "SAGATIMEOUTS";

	/// <summary>
	/// Gets the fully qualified table name (Oracle uses unquoted <c>SCHEMA.TABLE</c>).
	/// </summary>
	public string QualifiedTableName => $"{SchemaName}.{TableName}";

	/// <summary>
	/// Gets or sets the identifier this processor uses when claiming due timeouts.
	/// </summary>
	/// <value>
	/// A value that uniquely identifies this process among all processors sharing the timeout
	/// table. Defaults to <c>{MachineName}:{ProcessId}</c>.
	/// </value>
	[Required]
	public string ProcessorId { get; set; } = $"{Environment.MachineName}:{Environment.ProcessId}";

	/// <summary>
	/// Gets or sets the number of seconds a claim lease is held before it is considered stale
	/// and eligible for another processor to reclaim.
	/// </summary>
	/// <value>The lease duration, in seconds. Defaults to 120 seconds.</value>
	[Range(1, int.MaxValue)]
	public int LeaseTimeoutSeconds { get; set; } = 120;

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

		if (string.IsNullOrWhiteSpace(ProcessorId))
		{
			throw new InvalidOperationException("ProcessorId is required.");
		}

		// Each claim stamps ClaimedBy with "{ProcessorId}:{32-hex-guid}" so a batch can be read back by
		// exactly the rows it claimed. ClaimedBy is VARCHAR2(200 CHAR), so an over-long ProcessorId would
		// overflow the column and fail the claim at runtime, in a background delivery loop, as ORA-12899.
		// Reject it at startup instead, where an operator can still act on it.
		const int ClaimedByColumnLength = 200;
		const int ClaimTokenSuffixLength = 33; // ':' + Guid("N")

		if (ProcessorId.Length > ClaimedByColumnLength - ClaimTokenSuffixLength)
		{
			throw new InvalidOperationException(
				$"ProcessorId must be at most {ClaimedByColumnLength - ClaimTokenSuffixLength} characters: each "
				+ $"claim stores it in the {ClaimedByColumnLength}-character ClaimedBy column alongside a "
				+ $"{ClaimTokenSuffixLength}-character batch token.");
		}
	}
}
