// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a schema version registration.
/// </summary>
public sealed class SchemaVersionRegistration
{
	/// <summary>
	/// Gets the projection type.
	/// </summary>
	/// <value>
	/// The projection type.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the version number.
	/// </summary>
	/// <value>
	/// The version number.
	/// </value>
	public required string Version { get; init; }

	/// <summary>
	/// Gets the schema definition.
	/// </summary>
	/// <value>
	/// The schema definition.
	/// </value>
	public required object Schema { get; init; }

	/// <summary>
	/// Gets the registration timestamp.
	/// </summary>
	/// <value>
	/// The registration timestamp.
	/// </value>
	public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the description of changes in this version.
	/// </summary>
	/// <value>
	/// The description of changes in this version.
	/// </value>
	public string? Description { get; init; }

	/// <summary>
	/// Gets migration notes from the previous version.
	/// </summary>
	/// <value>
	/// Migration notes from the previous version.
	/// </value>
	public string? MigrationNotes { get; init; }
}
