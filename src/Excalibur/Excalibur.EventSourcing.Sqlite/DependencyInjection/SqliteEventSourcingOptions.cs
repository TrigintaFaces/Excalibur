// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Sqlite.DependencyInjection;

/// <summary>
/// Configuration options for SQLite event sourcing.
/// </summary>
public sealed class SqliteEventSourcingOptions
{
	/// <summary>
	/// Gets or sets the SQLite connection string.
	/// </summary>
	/// <example><c>Data Source=events.db</c></example>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the event store table name. Default: "Events".
	/// </summary>
	public string EventStoreTable { get; set; } = "Events";

	/// <summary>
	/// Gets or sets the snapshot store table name. Default: "Snapshots".
	/// </summary>
	public string SnapshotStoreTable { get; set; } = "Snapshots";
}
