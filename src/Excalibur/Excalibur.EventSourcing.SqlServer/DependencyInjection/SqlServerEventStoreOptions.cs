// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.SqlServer.DependencyInjection;

/// <summary>
/// Configuration options for an individual SQL Server event store registration.
/// </summary>
/// <remarks>
/// Use this with <c>AddSqlServerEventStore(Action&lt;SqlServerEventStoreOptions&gt;)</c>
/// for ergonomic per-store configuration without needing a raw <c>Func&lt;SqlConnection&gt;</c>.
/// </remarks>
public sealed class SqlServerEventStoreOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for the event store table. Default: "dbo".
	/// </summary>
	public string Schema { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the event store table name. Default: "EventStoreEvents".
	/// </summary>
	public string Table { get; set; } = "EventStoreEvents";
}
