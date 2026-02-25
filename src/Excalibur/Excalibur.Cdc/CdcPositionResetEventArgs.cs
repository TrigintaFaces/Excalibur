// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Provides data for a CDC position reset event when a stale position is detected.
/// </summary>
/// <remarks>
/// <para>
/// This is the canonical event args type for all CDC providers. It contains comprehensive
/// information about a stale position scenario, including the capture instance affected,
/// the invalid position, available recovery positions, and diagnostic context.
/// </para>
/// <para>
/// A stale position occurs when the saved CDC position (e.g., LSN for SQL Server) is no longer
/// valid in the change tables. This can happen due to:
/// <list type="bullet">
/// <item><description>CDC cleanup jobs purging old records</description></item>
/// <item><description>Database backup/restore operations</description></item>
/// <item><description>CDC being disabled and re-enabled</description></item>
/// <item><description>Change stream token expiration (for document databases)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class CdcPositionResetEventArgs
{
	/// <summary>
	/// Gets or sets the unique identifier of the CDC processor instance.
	/// </summary>
	/// <value>The processor identifier, typically the database connection identifier.</value>
	public string ProcessorId { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the type of CDC provider that detected the stale position.
	/// </summary>
	/// <value>The provider type name (e.g., "SqlServer", "Postgres", "MongoDB", "CosmosDb").</value>
	public string ProviderType { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the CDC capture instance (tracked table) affected.
	/// </summary>
	/// <value>
	/// The capture instance name. For SQL Server, this is typically "schema_tablename"
	/// (e.g., "dbo_Orders"). For other providers, this may be the collection or table name.
	/// </value>
	public string CaptureInstance { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the database name where the stale position was detected.
	/// </summary>
	public string DatabaseName { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the stale position that was detected as invalid.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The position format is provider-specific:
	/// <list type="bullet">
	/// <item><description>SQL Server: LSN (Log Sequence Number) as a 10-byte array</description></item>
	/// <item><description>Postgres: WAL position as bytes</description></item>
	/// <item><description>MongoDB: Resume token as bytes</description></item>
	/// <item><description>CosmosDB: Continuation token as bytes</description></item>
	/// </list>
	/// </para>
	/// <para>May be <see langword="null"/> if the position could not be determined.</para>
	/// </remarks>
	public byte[]? StalePosition { get; init; }

	/// <summary>
	/// Gets or sets the position that the processor will resume from after recovery.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This will be either the earliest or latest available position depending on the
	/// configured <see cref="Strategy"/>.
	/// </para>
	/// </remarks>
	public byte[]? NewPosition { get; init; }

	/// <summary>
	/// Gets or sets the earliest available position in the CDC change tables.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This represents the oldest change that can still be read. Resuming from this
	/// position is safe but may result in reprocessing previously handled events.
	/// </para>
	/// </remarks>
	public byte[]? EarliestAvailablePosition { get; init; }

	/// <summary>
	/// Gets or sets the latest available position in the CDC change tables.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This represents the most recent change. Resuming from this position skips
	/// all unprocessed changes between the stale position and now.
	/// </para>
	/// </remarks>
	public byte[]? LatestAvailablePosition { get; init; }

	/// <summary>
	/// Gets or sets the reason code explaining why the position became stale.
	/// </summary>
	/// <value>
	/// A standardized reason code. Common values include:
	/// <list type="bullet">
	/// <item><description><c>CDC_CLEANUP</c> - Position purged by CDC cleanup job</description></item>
	/// <item><description><c>BACKUP_RESTORE</c> - Position invalid after database restore</description></item>
	/// <item><description><c>CDC_REENABLED</c> - CDC was disabled and re-enabled</description></item>
	/// <item><description><c>LSN_OUT_OF_RANGE</c> - LSN is outside the valid range</description></item>
	/// <item><description><c>TOKEN_EXPIRED</c> - Change stream token expired (document databases)</description></item>
	/// <item><description><c>UNKNOWN</c> - Reason could not be determined</description></item>
	/// </list>
	/// </value>
	public string ReasonCode { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the human-readable reason message for the position reset.
	/// </summary>
	public string ReasonMessage { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the original exception that caused the stale position detection.
	/// </summary>
	/// <remarks>
	/// <para>
	/// For SQL Server, this is typically a <c>SqlException</c> with error number 22037 or 22029.
	/// For other providers, this contains the provider-specific exception.
	/// </para>
	/// </remarks>
	public Exception? OriginalException { get; init; }

	/// <summary>
	/// Gets or sets the timestamp when the stale position was detected.
	/// </summary>
	public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the recovery attempt number (1-based).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Recovery may be attempted multiple times based on <see cref="CdcRecoveryOptions.MaxRecoveryAttempts"/>.
	/// This property indicates which attempt is currently in progress.
	/// </para>
	/// </remarks>
	public int AttemptNumber { get; init; }

	/// <summary>
	/// Gets or sets the recovery strategy that will be applied.
	/// </summary>
	public StalePositionRecoveryStrategy Strategy { get; init; }

	/// <summary>
	/// Gets or sets additional context information for diagnostics.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Provider-specific diagnostic information can be included here, such as:
	/// <list type="bullet">
	/// <item><description>SQL Server: Error number, SQL state, procedure name</description></item>
	/// <item><description>MongoDB: Cluster time, operation time</description></item>
	/// <item><description>CosmosDB: Request charge, activity ID</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public IDictionary<string, object>? AdditionalContext { get; init; }

	/// <summary>
	/// Returns a string representation of the event for logging purposes.
	/// </summary>
	/// <returns>A formatted string containing the key properties.</returns>
	public override string ToString()
	{
		var staleHex = StalePosition != null ? $"0x{Convert.ToHexString(StalePosition)}" : "null";
		var newHex = NewPosition != null ? $"0x{Convert.ToHexString(NewPosition)}" : "null";

		return $"CdcPositionResetEventArgs {{ " +
			   $"ProcessorId = {ProcessorId}, " +
			   $"ProviderType = {ProviderType}, " +
			   $"CaptureInstance = {CaptureInstance}, " +
			   $"ReasonCode = {ReasonCode}, " +
			   $"StalePosition = {staleHex}, " +
			   $"NewPosition = {newHex}, " +
			   $"AttemptNumber = {AttemptNumber}, " +
			   $"Strategy = {Strategy}, " +
			   $"DetectedAt = {DetectedAt:O} }}";
	}
}

/// <summary>
/// Represents a handler for CDC position reset events.
/// </summary>
/// <param name="args">The event arguments containing reset details.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>A task representing the asynchronous operation.</returns>
/// <remarks>
/// <para>
/// This delegate is invoked when a stale position is detected and recovery is being attempted.
/// The handler can be used for:
/// <list type="bullet">
/// <item><description>Logging and alerting</description></item>
/// <item><description>Custom recovery logic (when using <see cref="StalePositionRecoveryStrategy.InvokeCallback"/>)</description></item>
/// <item><description>Metrics and monitoring</description></item>
/// </list>
/// </para>
/// </remarks>
public delegate Task CdcPositionResetHandler(CdcPositionResetEventArgs args, CancellationToken cancellationToken);
