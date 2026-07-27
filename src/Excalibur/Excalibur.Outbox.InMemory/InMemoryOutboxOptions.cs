// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Outbox.InMemory;

/// <summary>
/// Configuration options for the in-memory outbox store.
/// </summary>
public sealed class InMemoryOutboxOptions
{
	/// <summary>
	/// Gets or sets the maximum number of messages to retain.
	/// </summary>
	/// <value>The maximum message count. Zero means unlimited. Defaults to 10000.</value>
	[Range(0, int.MaxValue)]
	public int MaxMessages { get; set; } = 10000;

	/// <summary>
	/// Gets or sets the default retention period for sent messages.
	/// </summary>
	/// <value>The retention period. Defaults to 7 days.</value>
	public TimeSpan DefaultRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the number of seconds a claim lease is honored before it is considered stale and
	/// eligible for reclamation by another concurrent poller within the same process.
	/// </summary>
	/// <value>The lease timeout in seconds. Defaults to 120.</value>
	[Range(1, int.MaxValue)]
	public int LeaseTimeoutSeconds { get; set; } = 120;

	/// <summary>
	/// Gets or sets the failure-backoff floor F, in seconds: after <c>MarkFailedAsync</c> records a
	/// sub-ceiling failure, the message is not re-claimable by the drain until F has elapsed from the
	/// failure instant. This bounds the retry cadence of the plain (no fine-grained backoff) path so it
	/// cannot hot-loop the drain, while the message remains eventually re-claimable (at-least-once).
	/// </summary>
	/// <remarks>
	/// F is a DEDICATED backoff floor, deliberately decoupled from <see cref="LeaseTimeoutSeconds"/> (the
	/// crash-recovery abandonment window) so the two can be tuned independently. It MUST exceed the outbox
	/// polling interval (default 5 s) or the failed message could be re-claimed on the very next poll — the
	/// default is chosen to satisfy that for the default polling interval.
	/// </remarks>
	/// <value>The failure-backoff floor in seconds. Defaults to 30 (uniform across the outbox family).</value>
	[Range(1, int.MaxValue)]
	public int FailureBackoffFloorSeconds { get; set; } = 30;
}
