// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the result of template migration operations.
/// </summary>
public sealed class TemplateMigrationResult
{
	/// <summary>
	/// Gets a value indicating whether the migration was successful.
	/// </summary>
	/// <value> True if the migration completed successfully, false otherwise. </value>
	public required bool IsSuccessful { get; init; }

	/// <summary>
	/// Gets the collection of migration errors, if any.
	/// </summary>
	/// <value> A collection of migration error messages. </value>
	public IEnumerable<string> Errors { get; init; } = [];

	/// <summary>
	/// Gets the collection of warnings encountered during migration.
	/// </summary>
	/// <value> A collection of migration warning messages. </value>
	public IEnumerable<string> Warnings { get; init; } = [];
}
