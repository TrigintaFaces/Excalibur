// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Postgres.DependencyInjection;

/// <summary>
/// Configuration options for an individual Postgres snapshot store registration.
/// </summary>
/// <remarks>
/// Use this with <c>AddPostgresSnapshotStore(Action&lt;PostgresSnapshotStoreOptions&gt;)</c>
/// for ergonomic per-store configuration without needing a raw <c>NpgsqlDataSource</c>.
/// </remarks>
public sealed class PostgresSnapshotStoreOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for the snapshot store table. Default: "public".
	/// </summary>
	public string Schema { get; set; } = "public";

	/// <summary>
	/// Gets or sets the snapshot store table name. Default: "event_store_snapshots".
	/// </summary>
	public string Table { get; set; } = "event_store_snapshots";
}
