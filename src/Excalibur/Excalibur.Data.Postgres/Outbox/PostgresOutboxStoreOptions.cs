// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Configuration options for Postgres-based outbox message storage.
/// </summary>
public sealed class PostgresOutboxStoreOptions : OutboxOptions
{
	/// <summary>
	/// Gets or sets the schema name for outbox tables.
	/// </summary>
	/// <value>The schema name. Defaults to "public".</value>
	[Required]
	public string SchemaName { get; set; } = "public";

	/// <summary>
	/// Gets or sets the name of the database table used for storing outbox messages.
	/// </summary>
	/// <value>The table name for outbox messages. Defaults to "outbox".</value>
	[Required]
	public string OutboxTableName { get; set; } = "outbox";

	/// <summary>
	/// Gets or sets the name of the database table used for storing failed outbox messages.
	/// </summary>
	/// <value>The table name for dead letter outbox messages. Defaults to "outbox_dead_letters".</value>
	[Required]
	public string DeadLetterTableName { get; set; } = "outbox_dead_letters";

	/// <summary>
	/// Gets the fully qualified name for the outbox table.
	/// </summary>
	/// <value>The qualified table name in format "schema"."table".</value>
	public string QualifiedOutboxTableName => $"\"{SchemaName}\".\"{OutboxTableName}\"";

	/// <summary>
	/// Gets the fully qualified name for the dead letter table.
	/// </summary>
	/// <value>The qualified table name in format "schema"."table".</value>
	public string QualifiedDeadLetterTableName =>
			$"\"{SchemaName}\".\"{DeadLetterTableName}\"";

	/// <summary>
	/// Gets or sets the timeout duration (in seconds) for message reservation operations.
	/// </summary>
	/// <value>The reservation timeout in seconds for outbox message processing. Defaults to 300 (5 minutes).</value>
	[Range(1, int.MaxValue)]
	public int ReservationTimeout { get; set; } = 300;
}
