// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Represents a Change Data Capture (CDC) row retrieved from SQL Server.
/// </summary>
/// <remarks>
/// A CDC row contains metadata about the operation (e.g., insert, update, delete), the associated LSN (Log Sequence Number), and the
/// actual data changes.
/// </remarks>
public record CdcRow
{
	/// <summary>
	/// Gets or initializes the name of the table from which the CDC row originates.
	/// </summary>
	/// <value>
	/// The name of the table from which the CDC row originates.
	/// </value>
	public required string TableName { get; init; }

	/// <summary>
	/// Gets the Log Sequence Number (LSN) associated with the CDC row.
	/// </summary>
	/// <value>
	/// The Log Sequence Number (LSN) associated with the CDC row.
	/// </value>
	/// <remarks> The LSN serves as a unique identifier for a transaction and its order in the CDC log. </remarks>
	public required byte[] Lsn { get; init; }

	/// <summary>
	/// Gets the sequence value for the change.
	/// </summary>
	/// <value>
	/// The sequence value for the change.
	/// </value>
	/// <remarks> The sequence value helps identify multiple changes within the same transaction. </remarks>
	public required byte[] SeqVal { get; init; }

	/// <summary>
	/// Gets the operation code indicating the type of change.
	/// </summary>
	/// <value>
	/// The operation code indicating the type of change.
	/// </value>
	/// <remarks> Possible values are defined in the <see cref="CdcOperationCodes" /> enumeration. </remarks>
	public CdcOperationCodes OperationCode { get; init; }

	/// <summary>
	/// Gets the commit time of the transaction that caused the change.
	/// </summary>
	/// <value>
	/// The commit time of the transaction that caused the change.
	/// </value>
	public DateTime CommitTime { get; init; }

	/// <summary>
	/// Gets a dictionary containing the actual data changes for the CDC row.
	/// </summary>
	/// <value>
	/// A dictionary containing the actual data changes for the CDC row.
	/// </value>
	/// <remarks> The dictionary contains column names as keys and their corresponding new values as values. </remarks>
	public required IDictionary<string, object> Changes { get; init; }

	/// <summary>
	/// Gets a dictionary mapping column names to their data types.
	/// </summary>
	/// <value>
	/// A dictionary mapping column names to their data types.
	/// </value>
	/// <remarks> This is useful for interpreting the data changes with their corresponding data types. </remarks>
	public required Dictionary<string, Type> DataTypes { get; init; }
}
