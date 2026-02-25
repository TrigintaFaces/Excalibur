// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents options for controlling template migration operations.
/// </summary>
public sealed class TemplateMigrationOptions
{
	/// <summary>
	/// Gets a value indicating whether to perform validation before migration.
	/// </summary>
	/// <value> True to validate before migration, false otherwise. Defaults to true. </value>
	public bool ValidateBeforeMigration { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to create a backup of the existing template.
	/// </summary>
	/// <value> True to create a backup, false otherwise. Defaults to true. </value>
	public bool CreateBackup { get; init; } = true;

	/// <summary>
	/// Gets the maximum time to wait for migration completion.
	/// </summary>
	/// <value> The migration timeout. Defaults to 5 minutes. </value>
	public TimeSpan MigrationTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
