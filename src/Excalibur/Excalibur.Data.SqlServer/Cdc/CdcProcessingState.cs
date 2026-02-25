// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Represents the state of CDC (Change Data Capture) processing, including the last processed log sequence number (LSN), sequence value,
/// and other metadata.
/// </summary>
public sealed class CdcProcessingState
{
	/// <summary>
	/// Gets the last processed Log Sequence Number (LSN) in the CDC process.
	/// </summary>
	/// <value> The last processed Log Sequence Number (LSN) in the CDC process. </value>
	/// <remarks> The LSN is a unique identifier for a change operation in the database. This is used to track the progress of CDC processing. </remarks>
	public byte[] LastProcessedLsn { get; init; } = new byte[10];

	/// <summary>
	/// Gets the last processed sequence value in the CDC process.
	/// </summary>
	/// <value> The last processed sequence value in the CDC process. </value>
	/// <remarks> This value may be used to track sequence changes within a batch or transaction, if applicable. </remarks>
	public byte[]? LastProcessedSequenceValue { get; init; }

	/// <summary>
	/// Gets the timestamp of the last commit operation that was processed.
	/// </summary>
	/// <value> The timestamp of the last commit operation that was processed. </value>
	/// <remarks> This value indicates the latest commit time for the data captured by the CDC process. </remarks>
	public DateTime LastCommitTime { get; init; }

	/// <summary>
	/// Gets the timestamp when the CDC processing state was updated.
	/// </summary>
	/// <value> The timestamp when the CDC processing state was updated. </value>
	/// <remarks> This value reflects the time at which the CDC processing state was last persisted or recorded. </remarks>
	public DateTimeOffset ProcessedAt { get; init; }

	/// <summary>
	/// Gets the identifier for the database connection used in the CDC process.
	/// </summary>
	/// <value> The identifier for the database connection used in the CDC process. </value>
	/// <remarks>
	/// This value uniquely identifies the database connection, which can be useful for scenarios where multiple connections are used in the
	/// CDC process.
	/// </remarks>
	public string DatabaseConnectionIdentifier { get; init; } = string.Empty;

	/// <summary>
	/// Gets the name of the database being processed.
	/// </summary>
	/// <value> The name of the database being processed. </value>
	/// <remarks> This property identifies the database for which CDC is being tracked. </remarks>
	public string DatabaseName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the name of the table being processed.
	/// </summary>
	/// <value> The name of the table being processed. </value>
	/// <remarks> This property identifies the table for which CDC is being tracked. </remarks>
	public string TableName { get; init; } = string.Empty;
}
