// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents the result of a batch encryption migration operation.
/// </summary>
public sealed record EncryptionBatchMigrationResult
{
	/// <summary>
	/// Gets a value indicating whether all items were migrated successfully.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the migration identifier for this batch.
	/// </summary>
	public required string MigrationId { get; init; }

	/// <summary>
	/// Gets the total number of items in the batch.
	/// </summary>
	public required int TotalItems { get; init; }

	/// <summary>
	/// Gets the number of items that were successfully migrated.
	/// </summary>
	public required int SucceededCount { get; init; }

	/// <summary>
	/// Gets the number of items that failed migration.
	/// </summary>
	public required int FailedCount { get; init; }

	/// <summary>
	/// Gets the number of items that were skipped.
	/// </summary>
	public int SkippedCount { get; init; }

	/// <summary>
	/// Gets the successful migration results indexed by item ID.
	/// </summary>
	public IReadOnlyDictionary<string, EncryptionMigrationResult>? SuccessResults { get; init; }

	/// <summary>
	/// Gets the failed migration results indexed by item ID.
	/// </summary>
	public IReadOnlyDictionary<string, EncryptionMigrationResult>? FailureResults { get; init; }

	/// <summary>
	/// Gets the total duration of the batch migration.
	/// </summary>
	public required TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets the timestamp when the migration started.
	/// </summary>
	public required DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when the migration completed.
	/// </summary>
	public required DateTimeOffset CompletedAt { get; init; }

	/// <summary>
	/// Gets the success rate as a percentage.
	/// </summary>
	public double SuccessRate => TotalItems > 0 ? (double)SucceededCount / TotalItems * 100 : 0;

	/// <summary>
	/// Gets a value indicating whether the migration was partially successful.
	/// </summary>
	public bool IsPartialSuccess => !Success && SucceededCount > 0;
}
