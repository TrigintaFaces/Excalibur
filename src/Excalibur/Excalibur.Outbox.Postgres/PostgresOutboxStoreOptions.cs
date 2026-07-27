// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using DispatchOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Configuration options for Postgres-based outbox message storage.
/// </summary>
public sealed class PostgresOutboxStoreOptions : DispatchOutboxOptions
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
	/// Gets or sets the name of the table that holds the leadership-fencing high-water mark.
	/// </summary>
	/// <value>
	/// The fence control-table name. Defaults to "outbox_fence". This dedicated single-row-per-scope
	/// control table stores the fencing high-water mark durably, independent of the outbox rows themselves,
	/// so the high-water survives the delete-on-sent drain (unlike a live-row <c>MAX(fencing_token)</c>,
	/// which would reset once the winning rows are deleted).
	/// </value>
	[Required]
	public string FenceTableName { get; set; } = "outbox_fence";

	/// <summary>
	/// Gets the fully qualified name for the fence control table.
	/// </summary>
	/// <value>The qualified table name in format "schema"."table".</value>
	public string QualifiedFenceTableName => $"\"{SchemaName}\".\"{FenceTableName}\"";

	/// <summary>
	/// Gets or sets the timeout duration (in seconds) for message reservation operations.
	/// </summary>
	/// <value>The reservation timeout in seconds for outbox message processing. Defaults to 300 (5 minutes).</value>
	[Range(1, int.MaxValue)]
	public int ReservationTimeout { get; set; } = 300;

	/// <summary>
	/// Gets or sets the failure-backoff floor F, in seconds: after <c>MarkFailedAsync</c> records a
	/// sub-ceiling failure, the message is not re-claimable by the drain until F has elapsed. This bounds the
	/// retry cadence of the plain (no fine-grained backoff) path so it cannot hot-loop the drain, while the
	/// message remains eventually re-claimable (at-least-once). F is a DEDICATED backoff floor, decoupled
	/// from <see cref="ReservationTimeout"/> (the reservation window), and MUST exceed the outbox polling
	/// interval (default 5 s) — the default satisfies that for the default polling interval.
	/// </summary>
	/// <value>The failure-backoff floor in seconds. Defaults to 30 (uniform across the outbox family).</value>
	[Range(1, int.MaxValue)]
	public int FailureBackoffFloorSeconds { get; set; } = 30;
}
