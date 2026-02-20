// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Specifies the delivery guarantee level for outbox message processing.
/// </summary>
/// <remarks>
/// <para>
/// This enum controls how the outbox processor handles message completion and the trade-offs
/// between throughput, failure window, and delivery guarantees.
/// </para>
/// <para>
/// <b>Throughput vs. Failure Window Trade-off:</b>
/// Higher throughput modes batch operations but increase the window during which failures
/// could cause message redelivery. Lower throughput modes complete messages individually
/// for tighter guarantees.
/// </para>
/// </remarks>
public enum OutboxDeliveryGuarantee
{
	/// <summary>
	/// Batched completion with highest throughput and larger failure window.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Messages are read in batches, published, and then marked complete as a batch.
	/// If a failure occurs after publishing but before batch completion, all messages
	/// in the batch may be redelivered on restart.
	/// </para>
	/// <para>
	/// <b>Use when:</b> High throughput is critical and handlers are idempotent.
	/// </para>
	/// <para>
	/// <b>Throughput:</b> Highest (batch operations reduce database round-trips).
	/// </para>
	/// <para>
	/// <b>Failure window:</b> Entire batch (could be 100+ messages).
	/// </para>
	/// </remarks>
	AtLeastOnce = 0,

	/// <summary>
	/// Individual completion with lower throughput and smaller failure window.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each message is marked complete immediately after successful publish.
	/// If a failure occurs, only the current message may be redelivered.
	/// </para>
	/// <para>
	/// <b>Use when:</b> Minimizing duplicate deliveries is more important than throughput.
	/// </para>
	/// <para>
	/// <b>Throughput:</b> Lower (one database round-trip per message).
	/// </para>
	/// <para>
	/// <b>Failure window:</b> Single message.
	/// </para>
	/// </remarks>
	MinimizedWindow = 1,

	/// <summary>
	/// Exactly-once delivery when the message transport uses the same database.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When the outbox and message destination share the same database, the publish
	/// and completion can be wrapped in a single transaction, achieving exactly-once
	/// semantics. If the transport uses a different database or external service,
	/// this mode falls back to <see cref="MinimizedWindow"/> behavior.
	/// </para>
	/// <para>
	/// <b>Use when:</b> Exactly-once delivery is required and transport supports transactional publish.
	/// </para>
	/// <para>
	/// <b>Throughput:</b> Depends on transport (transactional overhead if supported).
	/// </para>
	/// <para>
	/// <b>Failure window:</b> Zero when transactional; single message otherwise.
	/// </para>
	/// </remarks>
	TransactionalWhenApplicable = 2,
}
