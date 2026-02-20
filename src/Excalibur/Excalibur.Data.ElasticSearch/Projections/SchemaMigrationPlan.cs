// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a schema migration plan.
/// </summary>
public sealed class SchemaMigrationPlan
{
	/// <summary>
	/// Gets the unique identifier for this migration plan.
	/// </summary>
	/// <value>
	/// The unique identifier for this migration plan.
	/// </value>
	public required string PlanId { get; init; }

	/// <summary>
	/// Gets the projection type being migrated.
	/// </summary>
	/// <value>
	/// The projection type being migrated.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the migration strategy.
	/// </summary>
	/// <value>
	/// The migration strategy.
	/// </value>
	public required MigrationStrategy Strategy { get; init; }

	/// <summary>
	/// Gets the ordered list of migration steps.
	/// </summary>
	/// <value>
	/// The ordered list of migration steps.
	/// </value>
	public required IReadOnlyList<MigrationStep> Steps { get; init; }

	/// <summary>
	/// Gets the estimated duration of the migration.
	/// </summary>
	/// <value>
	/// The estimated duration of the migration.
	/// </value>
	public TimeSpan? EstimatedDuration { get; init; }

	/// <summary>
	/// Gets the estimated data volume to migrate.
	/// </summary>
	/// <value>
	/// The estimated data volume to migrate.
	/// </value>
	public long? EstimatedDocuments { get; init; }

	/// <summary>
	/// Gets a value indicating whether the migration can be rolled back.
	/// </summary>
	/// <value>
	/// A value indicating whether the migration can be rolled back.
	/// </value>
	public bool IsReversible { get; init; }

	/// <summary>
	/// Gets the rollback plan if reversible.
	/// </summary>
	/// <value>
	/// The rollback plan if reversible.
	/// </value>
	public IReadOnlyList<MigrationStep>? RollbackSteps { get; init; }

	/// <summary>
	/// Gets validation checks to perform.
	/// </summary>
	/// <value>
	/// Validation checks to perform.
	/// </value>
	public IReadOnlyList<string>? ValidationChecks { get; init; }
}
