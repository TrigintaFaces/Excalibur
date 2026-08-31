// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Outbox.Marten;

/// <summary>
/// Configuration options for the Marten-based outbox store.
/// </summary>
/// <remarks>
/// The Marten outbox composes an <c>IDocumentSession</c> per operation from the consumer-supplied
/// <c>IDocumentStore</c>. Connection, schema, and serialization are configured on the Marten store
/// itself (via <c>services.AddMarten(...)</c>); these options cover only outbox-specific behavior.
/// </remarks>
public sealed class MartenOutboxStoreOptions
{
	/// <summary>
	/// Gets or sets the default retention period after which sent messages are eligible for cleanup.
	/// </summary>
	/// <value>The retention period. Must be positive. Defaults to 7 days.</value>
	public TimeSpan DefaultRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the maximum number of messages returned by a single cleanup pass.
	/// </summary>
	/// <value>The cleanup batch size. Must be greater than zero. Defaults to 500.</value>
	[Range(1, int.MaxValue)]
	public int CleanupBatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets how long a dispatcher's claim on a message is honoured before another may take it.
	/// </summary>
	/// <remarks>
	/// A dispatcher that crashes between claiming a message and marking it sent leaves the claim behind.
	/// Once this elapses another dispatcher reclaims the message, so a crash costs a delay rather than a
	/// permanently stranded message. Set it comfortably above the longest expected send: a value shorter
	/// than that lets a second dispatcher take a message the first is still working on, which turns the
	/// at-least-once delivery this store provides into duplicate delivery far more often than a crash would.
	/// </remarks>
	/// <value>The claim duration. Must be positive. Defaults to 5 minutes.</value>
	public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the schema holding the claim table.
	/// </summary>
	/// <value>The schema name. Defaults to <c>public</c>.</value>
	public string ClaimsSchemaName { get; set; } = "public";

	/// <summary>
	/// Gets or sets the name of the claim table.
	/// </summary>
	/// <remarks>
	/// The claims live in a table this store owns rather than in the Marten document itself. Marten keeps
	/// a document's fields inside a <c>jsonb</c> column whose property names come from the serializer the
	/// CONSUMER configured on their own <c>IDocumentStore</c> — so SQL reaching into that document would
	/// silently stop matching under a different casing convention. A table this store defines has columns
	/// it controls, and the claim is expressible as a single atomic statement against them.
	/// </remarks>
	/// <value>The table name. Defaults to <c>excalibur_outbox_claims</c>.</value>
	public string ClaimsTableName { get; set; } = "excalibur_outbox_claims";

	/// <summary>
	/// Gets or sets the failure-backoff floor F, in seconds: after <see cref="MartenOutboxStore.MarkFailedAsync"/>
	/// records a plain failure, the message becomes re-claimable only once F has elapsed from the failure
	/// instant. This bounds the retry cadence of the plain path so it cannot hot-loop the drain against a
	/// persistently failing destination, while the message stays eventually re-claimable rather than being
	/// dropped. F must exceed the drain polling interval; the validator enforces that at startup.
	/// </summary>
	/// <value>The failure-backoff floor in seconds. Defaults to 30 (uniform across the outbox family).</value>
	public int FailureBackoffFloorSeconds { get; set; } = 30;
}
