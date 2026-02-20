// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Jobs.SqlServer;

/// <summary>
/// Configuration options for the SQL Server job coordinator.
/// </summary>
public sealed class SqlServerJobCoordinatorOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	/// <value>The SQL Server connection string.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the database schema name for job coordination tables.
	/// </summary>
	/// <value>The schema name. Defaults to "Jobs".</value>
	public string SchemaName { get; set; } = "Jobs";

	/// <summary>
	/// Gets or sets the default lock timeout for distributed job locks.
	/// </summary>
	/// <value>The lock timeout. Defaults to 30 seconds.</value>
	public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the instance registration TTL for heartbeat-based expiry.
	/// </summary>
	/// <value>The instance TTL. Defaults to 5 minutes.</value>
	public TimeSpan InstanceTtl { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the completion record retention period.
	/// </summary>
	/// <value>The completion retention period. Defaults to 1 hour.</value>
	public TimeSpan CompletionRetention { get; set; } = TimeSpan.FromHours(1);
}
