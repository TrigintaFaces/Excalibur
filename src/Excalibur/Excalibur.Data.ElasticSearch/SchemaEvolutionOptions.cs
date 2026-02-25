// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures schema evolution and migration settings.
/// </summary>
public sealed class SchemaEvolutionOptions
{
	/// <summary>
	/// Gets a value indicating whether schema evolution tracking is enabled.
	/// </summary>
	/// <value>
	/// A value indicating whether schema evolution tracking is enabled.
	/// </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to validate backwards compatibility.
	/// </summary>
	/// <value>
	/// A value indicating whether to validate backwards compatibility.
	/// </value>
	public bool ValidateBackwardsCompatibility { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to allow breaking changes.
	/// </summary>
	/// <value>
	/// A value indicating whether to allow breaking changes.
	/// </value>
	public bool AllowBreakingChanges { get; init; }

	/// <summary>
	/// Gets the default migration strategy.
	/// </summary>
	/// <value>
	/// The default migration strategy.
	/// </value>
	public string DefaultMigrationStrategy { get; init; } = "AliasSwitch";

	/// <summary>
	/// Gets a value indicating whether to automatically create backups before migrations.
	/// </summary>
	/// <value>
	/// A value indicating whether to automatically create backups before migrations.
	/// </value>
	public bool AutoBackup { get; init; } = true;
}
