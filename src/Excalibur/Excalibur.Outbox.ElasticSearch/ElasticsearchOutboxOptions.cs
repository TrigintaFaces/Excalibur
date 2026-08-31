// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Outbox.ElasticSearch;

/// <summary>
/// Configuration options for the Elasticsearch outbox store.
/// </summary>
public sealed class ElasticsearchOutboxOptions
{
	/// <summary>
	/// Gets or sets the index name for outbox entries.
	/// </summary>
	/// <value>The index name for outbox entries.</value>
	[Required]
	public string IndexName { get; set; } = "excalibur-outbox";

	/// <summary>
	/// Gets or sets the default batch size for retrieving unsent messages.
	/// </summary>
	/// <value>The default batch size. Defaults to 100.</value>
	[Range(1, 10000)]
	public int DefaultBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the refresh policy for index operations.
	/// </summary>
	/// <value>The refresh policy. Defaults to "wait_for" for consistency.</value>
	public string RefreshPolicy { get; set; } = "wait_for";

	/// <summary>
	/// Gets or sets the lease (visibility) timeout in seconds for a claimed message.
	/// </summary>
	/// <value>The lease timeout in seconds. Defaults to 300 (5 minutes).</value>
	/// <remarks>
	/// When a poller claims staged messages for delivery, each claimed message is leased for this
	/// duration: it is hidden from other pollers (disjoint claim) until either it reaches a terminal
	/// state (sent/failed/dead-lettered, which clears the lease) or the lease expires. An expired lease
	/// makes the message claimable again, so a poller that crashes mid-delivery cannot strand a message
	/// permanently. Set this comfortably longer than the slowest expected delivery: if the lease expires
	/// while a delivery is still in flight, another poller may claim the same message and the consumer
	/// observes a duplicate. Delivery is at-least-once, so handlers must be idempotent regardless.
	/// </remarks>
	[Range(1, int.MaxValue)]
	public int LeaseTimeoutSeconds { get; set; } = 300;

	/// <summary>
	/// Gets or sets the failure-backoff floor F, in seconds: after <see cref="ElasticsearchOutboxStore.MarkFailedAsync"/>
	/// records a plain failure, the message becomes re-claimable only once F has elapsed from the failure
	/// instant (its <c>NextAttemptAt</c> gate). This bounds the retry cadence of the plain path so it cannot
	/// hot-loop the drain against a persistently failing destination, while the message stays eventually
	/// re-claimable rather than being dropped. F must exceed the drain polling interval; the validator
	/// enforces that cross-options invariant at startup.
	/// </summary>
	/// <value>The failure-backoff floor in seconds. Defaults to 30 (uniform across the outbox family).</value>
	[Range(1, int.MaxValue)]
	public int FailureBackoffFloorSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the identifier recorded as the owner of a claimed message's lease.
	/// </summary>
	/// <value>The lease-owner identifier, or <see langword="null"/> to generate a stable per-instance id.</value>
	/// <remarks>
	/// Used for diagnostics — which poller currently holds a message. Claim disjointness does not depend
	/// on this value: it is enforced by the store's compare-and-swap on the document's optimistic
	/// concurrency tokens, so two pollers sharing an identifier still receive disjoint batches.
	/// </remarks>
	public string? ProcessorId { get; set; }
}
