// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.EventSourcing;

/// <summary>
/// Configuration options for Postgres event store.
/// </summary>
public sealed class PostgresEventStoreOptions
{
	/// <summary>
	/// Gets the name of the database table for storing events.
	/// </summary>
	/// <value>The table name. Defaults to "event_store_events".</value>
	[Required]
	public string EventsTableName { get; init; } = "event_store_events";

	/// <summary>
	/// Gets the database schema name.
	/// </summary>
	/// <value>The schema name. Defaults to "public".</value>
	[Required]
	public string SchemaName { get; init; } = "public";
}
