// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the result of a schema migration.
/// </summary>
public sealed class SchemaMigrationResult
{
	/// <summary>
	/// Gets a value indicating whether the migration succeeded.
	/// </summary>
	/// <value>
	/// A value indicating whether the migration succeeded.
	/// </value>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the migration plan identifier.
	/// </summary>
	/// <value>
	/// The migration plan identifier.
	/// </value>
	public required string PlanId { get; init; }

	/// <summary>
	/// Gets the start time of the migration.
	/// </summary>
	/// <value>
	/// The start time of the migration.
	/// </value>
	public required DateTimeOffset StartTime { get; init; }

	/// <summary>
	/// Gets the end time of the migration.
	/// </summary>
	/// <value>
	/// The end time of the migration.
	/// </value>
	public required DateTimeOffset EndTime { get; init; }

	/// <summary>
	/// Gets the total documents migrated.
	/// </summary>
	/// <value>
	/// The total documents migrated.
	/// </value>
	public long DocumentsMigrated { get; init; }

	/// <summary>
	/// Gets the documents that failed migration.
	/// </summary>
	/// <value>
	/// The documents that failed migration.
	/// </value>
	public long DocumentsFailed { get; init; }

	/// <summary>
	/// Gets the completed steps.
	/// </summary>
	/// <value>
	/// The completed steps.
	/// </value>
	public required IReadOnlyList<StepResult> CompletedSteps { get; init; }

	/// <summary>
	/// Gets any errors that occurred.
	/// </summary>
	/// <value>
	/// Any errors that occurred.
	/// </value>
	public IReadOnlyList<string>? Errors { get; init; }

	/// <summary>
	/// Gets any warnings generated during migration.
	/// </summary>
	/// <value>
	/// Any warnings generated during migration.
	/// </value>
	public IReadOnlyList<string>? Warnings { get; init; }
}
