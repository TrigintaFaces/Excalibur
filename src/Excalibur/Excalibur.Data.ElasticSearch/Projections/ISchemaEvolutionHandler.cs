// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Manages schema evolution and migrations for ElasticSearch projections.
/// </summary>
public interface ISchemaEvolutionHandler
{
	/// <summary>
	/// Analyzes schema differences between source and target indices.
	/// </summary>
	/// <param name="sourceIndex"> The source index name. </param>
	/// <param name="targetIndex"> The target index name or mapping. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A schema comparison result detailing the differences. </returns>
	Task<SchemaComparisonResult> CompareSchemaAsync(
		string sourceIndex,
		string targetIndex,
		CancellationToken cancellationToken);

	/// <summary>
	/// Plans a schema migration from one version to another.
	/// </summary>
	/// <param name="request"> The migration planning request. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A migration plan detailing the steps required. </returns>
	Task<SchemaMigrationPlan> PlanMigrationAsync(
		SchemaMigrationRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes a schema migration plan.
	/// </summary>
	/// <param name="plan"> The migration plan to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the migration execution. </returns>
	Task<SchemaMigrationResult> ExecuteMigrationAsync(
		SchemaMigrationPlan plan,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates that a schema change is backwards compatible.
	/// </summary>
	/// <param name="currentSchema"> The current schema definition. </param>
	/// <param name="newSchema"> The proposed new schema definition. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A validation result indicating compatibility. </returns>
	Task<SchemaCompatibilityResult> ValidateBackwardsCompatibilityAsync(
		object currentSchema,
		object newSchema,
		CancellationToken cancellationToken);

	/// <summary>
	/// Registers a schema version for tracking.
	/// </summary>
	/// <param name="registration"> The schema version registration details. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous registration operation. </returns>
	Task RegisterSchemaVersionAsync(
		SchemaVersionRegistration registration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the schema version history for a projection type.
	/// </summary>
	/// <param name="projectionType"> The projection type. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The version history of the schema. </returns>
	Task<SchemaVersionHistory> GetSchemaHistoryAsync(
		string projectionType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Performs a dry run of a schema migration without applying changes.
	/// </summary>
	/// <param name="plan"> The migration plan to test. </param>
	/// <param name="sampleSize"> The number of documents to test with. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The dry run results. </returns>
	Task<SchemaMigrationDryRunResult> DryRunMigrationAsync(
		SchemaMigrationPlan plan,
		int sampleSize,
		CancellationToken cancellationToken);
}
