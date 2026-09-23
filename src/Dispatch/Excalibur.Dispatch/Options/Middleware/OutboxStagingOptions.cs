// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Outbox;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for outbox staging middleware.
/// </summary>
public sealed class OutboxStagingOptions
{
	/// <summary>
	/// Gets or sets the outbox consistency mode. This is a STARTUP REQUIREMENT, not a behaviour selector:
	/// setting it to <see cref="OutboxConsistencyMode.Transactional"/> makes startup fail unless the
	/// transactional infrastructure this package can see is registered. It does not choose a write path.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>What it checks, and what it cannot.</b> The validator confirms an outbox store and the transaction
	/// middleware are registered. It cannot confirm that an event store supports transactional staging,
	/// because that capability is declared in the event-sourcing packages and this package does not depend
	/// on them. A configuration that satisfies this option can therefore still resolve to an
	/// eventually-consistent write path at runtime.
	/// </para>
	/// <para>
	/// <b>Which switch selects the path.</b> For event-sourced aggregates the write path is chosen by the
	/// staging strategy on the event-sourcing builder, which prefers the transactional path when both a
	/// transactional outbox writer and a transactional event store are present and falls back otherwise.
	/// Setting this option does not change that choice; it only refuses to start without the parts it can
	/// see.
	/// </para>
	/// </remarks>
	/// <value>Default is <see cref="OutboxConsistencyMode.EventuallyConsistent"/>.</value>
	public OutboxConsistencyMode ConsistencyMode { get; set; }
		= OutboxConsistencyMode.EventuallyConsistent;

	/// <summary>
	/// Gets or sets a value indicating whether outbox staging is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of outbound messages to stage per processing operation.
	/// </summary>
	/// <value> Default is 100. </value>
	public int MaxOutboundMessagesPerOperation { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to compress message data in the outbox.
	/// </summary>
	/// <value> Default is false. </value>
	public bool CompressMessageData { get; set; }

	/// <summary>
	/// Gets or sets message types that bypass outbox staging.
	/// </summary>
	/// <value> The current <see cref="BypassOutboxForTypes" /> value. </value>
	public string[]? BypassOutboxForTypes { get; set; }
}
