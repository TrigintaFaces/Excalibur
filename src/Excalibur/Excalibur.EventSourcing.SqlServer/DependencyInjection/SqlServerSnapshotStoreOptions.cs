// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.SqlServer.DependencyInjection;

/// <summary>
/// Configuration options for an individual SQL Server snapshot store registration.
/// </summary>
/// <remarks>
/// Use this with <c>AddSqlServerSnapshotStore(Action&lt;SqlServerSnapshotStoreOptions&gt;)</c>
/// for ergonomic per-store configuration without needing a raw <c>Func&lt;SqlConnection&gt;</c>.
/// </remarks>
public sealed class SqlServerSnapshotStoreOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for the snapshot store table. Default: "dbo".
	/// </summary>
	public string Schema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the snapshot store table name. Default: "EventStoreSnapshots".
	/// </summary>
	public string Table { get; set; } = "EventStoreSnapshots";
}
