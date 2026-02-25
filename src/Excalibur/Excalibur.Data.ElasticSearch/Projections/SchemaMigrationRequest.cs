// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a request to migrate a schema.
/// </summary>
public sealed class SchemaMigrationRequest
{
	/// <summary>
	/// Gets the projection type being migrated.
	/// </summary>
	/// <value>
	/// The projection type being migrated.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the source index name.
	/// </summary>
	/// <value>
	/// The source index name.
	/// </value>
	public required string SourceIndex { get; init; }

	/// <summary>
	/// Gets the target index name.
	/// </summary>
	/// <value>
	/// The target index name.
	/// </value>
	public required string TargetIndex { get; init; }

	/// <summary>
	/// Gets the migration strategy to use.
	/// </summary>
	/// <value>
	/// The migration strategy to use.
	/// </value>
	public required MigrationStrategy Strategy { get; init; }

	/// <summary>
	/// Gets the new schema definition.
	/// </summary>
	/// <value>
	/// The new schema definition.
	/// </value>
	public required object NewSchema { get; init; }

	/// <summary>
	/// Gets field mappings for renamed or transformed fields.
	/// </summary>
	/// <value>
	/// Field mappings for renamed or transformed fields.
	/// </value>
	public IDictionary<string, string>? FieldMappings { get; init; }

	/// <summary>
	/// Gets transformation scripts for complex field changes.
	/// </summary>
	/// <value>
	/// Transformation scripts for complex field changes.
	/// </value>
	public IDictionary<string, string>? TransformationScripts { get; init; }

	/// <summary>
	/// Gets a value indicating whether to validate data during migration.
	/// </summary>
	/// <value>
	/// A value indicating whether to validate data during migration.
	/// </value>
	public bool ValidateData { get; init; } = true;

	/// <summary>
	/// Gets the batch size for migration operations.
	/// </summary>
	/// <value>
	/// The batch size for migration operations.
	/// </value>
	public int BatchSize { get; init; } = 1000;
}
