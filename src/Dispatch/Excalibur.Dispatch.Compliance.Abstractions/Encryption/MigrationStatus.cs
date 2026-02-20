// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents the current status of a migration operation.
/// </summary>
public sealed record MigrationStatus
{
	/// <summary>
	/// Gets the unique identifier for this migration.
	/// </summary>
	public required string MigrationId { get; init; }

	/// <summary>
	/// Gets the current state of the migration.
	/// </summary>
	public required MigrationState State { get; init; }

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
	/// Gets the timestamp when the migration started.
	/// </summary>
	public required DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when the migration last updated.
	/// </summary>
	public required DateTimeOffset LastUpdatedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when the migration completed, if completed.
	/// </summary>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>
	/// Gets the error message if the migration failed or was cancelled.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets additional details about the migration.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Details { get; init; }

	/// <summary>
	/// Gets the completion percentage.
	/// </summary>
	public double PercentComplete => TotalItems > 0 ? (double)CompletedItems / TotalItems * 100 : 0;
}

/// <summary>
/// Represents the state of a migration operation.
/// </summary>
public enum MigrationState
{
	/// <summary>
	/// The migration is pending and has not started.
	/// </summary>
	Pending = 0,

	/// <summary>
	/// The migration is currently running.
	/// </summary>
	Running = 1,

	/// <summary>
	/// The migration is paused and can be resumed.
	/// </summary>
	Paused = 2,

	/// <summary>
	/// The migration completed successfully.
	/// </summary>
	Completed = 3,

	/// <summary>
	/// The migration failed.
	/// </summary>
	Failed = 4,

	/// <summary>
	/// The migration was cancelled.
	/// </summary>
	Cancelled = 5,
}
