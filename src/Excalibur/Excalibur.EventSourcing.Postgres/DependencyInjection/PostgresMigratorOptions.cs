// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Reflection;

namespace Excalibur.EventSourcing.Postgres.DependencyInjection;

/// <summary>
/// Configuration options for Postgres schema migration.
/// </summary>
public sealed class PostgresMigratorOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	/// <value>The connection string.</value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the assembly containing migration scripts as embedded resources.
	/// </summary>
	/// <value>The migration assembly.</value>
	public Assembly? MigrationAssembly { get; set; }

	/// <summary>
	/// Gets or sets the namespace prefix for migration resources.
	/// </summary>
	/// <value>The migration namespace (e.g., "MyApp.Migrations").</value>
	public string? MigrationNamespace { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to automatically run migrations on startup.
	/// </summary>
	/// <value><see langword="true"/> to run migrations on startup; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool AutoMigrateOnStartup { get; set; }
}
