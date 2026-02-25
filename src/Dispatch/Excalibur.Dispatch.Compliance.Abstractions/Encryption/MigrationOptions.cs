// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Options controlling batch encryption migration behavior.
/// </summary>
public sealed record BatchMigrationOptions
{
	/// <summary>
	/// Gets the maximum degree of parallelism for batch migrations.
	/// </summary>
	public int MaxDegreeOfParallelism { get; init; } = 4;

	/// <summary>
	/// Gets the batch size for processing items.
	/// </summary>
	public int BatchSize { get; init; } = 100;

	/// <summary>
	/// Gets a value indicating whether to continue on individual item failures.
	/// </summary>
	public bool ContinueOnError { get; init; } = true;

	/// <summary>
	/// Gets the timeout for individual item migrations.
	/// </summary>
	public TimeSpan ItemTimeout { get; init; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets the overall timeout for the batch migration.
	/// </summary>
	public TimeSpan? TotalTimeout { get; init; }

	/// <summary>
	/// Gets a value indicating whether to track detailed progress.
	/// </summary>
	public bool TrackProgress { get; init; } = true;

	/// <summary>
	/// Gets the identifier for this migration run (for resumability).
	/// </summary>
	public string? MigrationId { get; init; }

	/// <summary>
	/// Gets the callback for progress updates.
	/// </summary>
	public IProgress<EncryptionMigrationProgress>? Progress { get; init; }

	/// <summary>
	/// Gets the default migration options.
	/// </summary>
	public static BatchMigrationOptions Default => new();
}

/// <summary>
/// Represents progress information for a migration operation.
/// </summary>
public sealed record EncryptionMigrationProgress
{
	/// <summary>
	/// Gets the total number of items to migrate.
	/// </summary>
	public required int TotalItems { get; init; }

	/// <summary>
	/// Gets the number of items completed.
	/// </summary>
	public required int CompletedItems { get; init; }

	/// <summary>
	/// Gets the number of items that succeeded.
	/// </summary>
	public required int SucceededItems { get; init; }

	/// <summary>
	/// Gets the number of items that failed.
	/// </summary>
	public required int FailedItems { get; init; }

	/// <summary>
	/// Gets the completion percentage.
	/// </summary>
	public double PercentComplete => TotalItems > 0 ? (double)CompletedItems / TotalItems * 100 : 0;

	/// <summary>
	/// Gets the current item being processed.
	/// </summary>
	public string? CurrentItemId { get; init; }

	/// <summary>
	/// Gets the elapsed time since the migration started.
	/// </summary>
	public TimeSpan Elapsed { get; init; }

	/// <summary>
	/// Gets the estimated time remaining.
	/// </summary>
	public TimeSpan? EstimatedRemaining { get; init; }
}
