// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.Snapshots;

/// <summary>
/// Configuration options for Postgres snapshot store.
/// </summary>
public sealed class PostgresSnapshotStoreOptions
{
	/// <summary>
	/// Gets the database schema name.
	/// </summary>
	/// <value>The schema name. Defaults to "public".</value>
	[Required]
	public string SchemaName { get; init; } = "public";

	/// <summary>
	/// Gets the name of the database table for storing snapshots.
	/// </summary>
	/// <value>The table name. Defaults to "snapshots".</value>
	[Required]
	public string TableName { get; init; } = "snapshots";
}
