// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Represents a migration that has been applied to the database.
/// </summary>
/// <remarks>
/// This record is used both for tracking migrations that have been applied to the database
/// and for describing migrations that were applied during a migration operation.
/// </remarks>
public sealed record AppliedMigration
{
	/// <summary>
	/// Gets the unique identifier for this migration.
	/// </summary>
	/// <value>
	/// A unique string identifier, typically in format "YYYYMMDDHHMMSS_MigrationName"
	/// to ensure ordering and uniqueness.
	/// </value>
	public required string MigrationId { get; init; }

	/// <summary>
	/// Gets the timestamp when this migration was applied.
	/// </summary>
	/// <value>The UTC timestamp of when the migration was applied to the database.</value>
	public required DateTimeOffset AppliedAt { get; init; }

	/// <summary>
	/// Gets an optional description of what this migration does.
	/// </summary>
	/// <value>A human-readable description, or <see langword="null"/> if not provided.</value>
	public string? Description { get; init; }

	/// <summary>
	/// Gets the checksum of the migration script for integrity verification.
	/// </summary>
	/// <value>A hash of the migration content, or <see langword="null"/> if not tracked.</value>
	public string? Checksum { get; init; }
}
