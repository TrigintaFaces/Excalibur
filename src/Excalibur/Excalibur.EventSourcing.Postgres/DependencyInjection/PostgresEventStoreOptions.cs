// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Postgres.DependencyInjection;

/// <summary>
/// Configuration options for an individual Postgres event store registration.
/// </summary>
/// <remarks>
/// Use this with <c>AddPostgresEventStore(Action&lt;PostgresEventStoreOptions&gt;)</c>
/// for ergonomic per-store configuration without needing a raw <c>NpgsqlDataSource</c>.
/// </remarks>
public sealed class PostgresEventStoreOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for the event store table. Default: "public".
	/// </summary>
	public string Schema { get; set; } = "public";

	/// <summary>
	/// Gets or sets the event store table name. Default: "events".
	/// </summary>
	public string Table { get; set; } = "events";
}
