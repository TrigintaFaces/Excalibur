// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Represents the progress of a serializer migration operation.
/// </summary>
/// <remarks>
/// <para>
/// This record is used with <see cref="IProgress{T}"/> to report migration progress
/// during batch processing operations in <see cref="SerializerMigrationService"/>.
/// </para>
/// <para>
/// Progress is reported after each batch is processed, allowing consumers to:
/// </para>
/// <list type="bullet">
///   <item>Track migration completion percentage</item>
///   <item>Monitor failure rates</item>
///   <item>Estimate remaining time</item>
///   <item>Log progress for observability</item>
/// </list>
/// <para>
/// See the migration strategy documentation.
/// </para>
/// </remarks>
/// <param name="TotalMigrated">The total number of records successfully migrated so far.</param>
/// <param name="TotalFailed">The total number of records that failed migration so far.</param>
/// <param name="TotalSkipped">The total number of records skipped (already in target format).</param>
/// <param name="CurrentBatchSize">The size of the most recently processed batch.</param>
/// <param name="EstimatedRemaining">
/// The estimated number of records remaining to process, or null if unknown.
/// </param>
public record EncryptionMigrationProgress(
	int TotalMigrated,
	int TotalFailed,
	int TotalSkipped,
	int CurrentBatchSize,
	int? EstimatedRemaining = null)
{
	/// <summary>
	/// Gets the total number of records processed (migrated + failed + skipped).
	/// </summary>
	public int TotalProcessed => TotalMigrated + TotalFailed + TotalSkipped;

	/// <summary>
	/// Gets the success rate as a percentage (0-100).
	/// </summary>
	/// <remarks>
	/// Returns 100 if no records have been processed yet (to avoid division by zero).
	/// Skipped records are not counted as failures.
	/// </remarks>
	public double SuccessRate
	{
		get
		{
			var processed = TotalMigrated + TotalFailed;
			return processed == 0 ? 100.0 : TotalMigrated * 100.0 / processed;
		}
	}

	/// <summary>
	/// Gets the failure rate as a percentage (0-100).
	/// </summary>
	public double FailureRate => 100.0 - SuccessRate;

	/// <summary>
	/// Gets the estimated completion percentage, if EstimatedRemaining is known.
	/// </summary>
	public double? CompletionPercentage
	{
		get
		{
			if (EstimatedRemaining is null)
			{
				return null;
			}

			var total = TotalProcessed + EstimatedRemaining.Value;
			return total == 0 ? 100.0 : TotalProcessed * 100.0 / total;
		}
	}

	/// <summary>
	/// Creates an initial progress instance with zero counts.
	/// </summary>
	public static EncryptionMigrationProgress Initial => new(0, 0, 0, 0);

	/// <summary>
	/// Returns a string representation suitable for logging.
	/// </summary>
	public override string ToString()
	{
		var completion = CompletionPercentage.HasValue
			? $", {CompletionPercentage.Value:F1}% complete"
			: "";

		return $"Migrated: {TotalMigrated}, Failed: {TotalFailed}, Skipped: {TotalSkipped}" +
			   $" (Success rate: {SuccessRate:F1}%{completion})";
	}
}

/// <summary>
/// Options for configuring serializer migration behavior.
/// </summary>
/// <remarks>
/// <para>
/// These options control batch processing, verification, and error handling
/// during migration operations.
/// </para>
/// </remarks>
public sealed class MigrationOptions
{
	/// <summary>
	/// Gets or sets the number of records to process in each batch.
	/// </summary>
	/// <remarks>
	/// Smaller batches reduce memory pressure but increase database round-trips.
	/// Larger batches are more efficient but may cause memory issues with large payloads.
	/// Default: 1000 records per batch.
	/// </remarks>
	public int BatchSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets whether to verify each migrated record by reading it back.
	/// </summary>
	/// <remarks>
	/// When enabled, each migrated record is read back and deserialized to verify
	/// the migration was successful. This adds overhead but provides stronger guarantees.
	/// Default: false (for performance).
	/// </remarks>
	public bool EnableReadBackVerification { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of consecutive failures before aborting.
	/// </summary>
	/// <remarks>
	/// If this many consecutive records fail migration, the operation is aborted.
	/// Set to 0 to disable this check and continue regardless of failures.
	/// Default: 100 consecutive failures.
	/// </remarks>
	public int MaxConsecutiveFailures { get; set; } = 100;

	/// <summary>
	/// Gets or sets whether to continue migration after encountering failures.
	/// </summary>
	/// <remarks>
	/// When true, migration continues even if some records fail.
	/// When false, the first failure aborts the entire operation.
	/// Default: true (continue on failure).
	/// </remarks>
	public bool ContinueOnFailure { get; set; } = true;

	/// <summary>
	/// Gets or sets the delay between batches in milliseconds.
	/// </summary>
	/// <remarks>
	/// Adding a delay between batches can reduce database load during migration.
	/// Set to 0 for maximum throughput.
	/// Default: 0 (no delay).
	/// </remarks>
	public int DelayBetweenBatchesMs { get; set; }
}
