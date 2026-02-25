// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the version history of a schema.
/// </summary>
public sealed class SchemaVersionHistory
{
	/// <summary>
	/// Gets the projection type.
	/// </summary>
	/// <value>
	/// The projection type.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the current version.
	/// </summary>
	/// <value>
	/// The current version.
	/// </value>
	public required string CurrentVersion { get; init; }

	/// <summary>
	/// Gets all versions in chronological order.
	/// </summary>
	/// <value>
	/// All versions in chronological order.
	/// </value>
	public required IReadOnlyList<SchemaVersionRegistration> Versions { get; init; }

	/// <summary>
	/// Gets the migration history between versions.
	/// </summary>
	/// <value>
	/// The migration history between versions.
	/// </value>
	public IReadOnlyList<SchemaMigrationResult>? MigrationHistory { get; init; }
}
