// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch;

/// <summary>
/// Defines the possible delivery states of an outbound message in the outbox.
/// </summary>
/// <remarks>
/// The outbox status tracks the lifecycle of an outbound message from staging through delivery completion or failure. This enables proper
/// retry handling and delivery guarantees.
/// </remarks>
public enum OutboxStatus
{
	/// <summary>
	/// The message has been staged and is awaiting delivery.
	/// </summary>
	/// <remarks>
	/// This is the initial state when a message is first stored in the outbox. Messages in this state are eligible for immediate delivery
	/// unless they have a future scheduled delivery time.
	/// </remarks>
	Staged = 0,

	/// <summary>
	/// The message is currently being delivered.
	/// </summary>
	/// <remarks>
	/// Concurrent delivery is guarded by the store's lease columns (e.g. <c>LeasedAt</c>/<c>LeasedBy</c>), not by this status — a claimed
	/// message is leased while its status remains <see cref="Staged"/> until it reaches a terminal state. This member describes the
	/// in-flight phase of the multi-transport delivery lifecycle and is not persisted by the single-row outbox claim path.
	/// </remarks>
	Sending = 1,

	/// <summary>
	/// The message has been successfully delivered.
	/// </summary>
	/// <remarks>
	/// This is a terminal state indicating successful message delivery. Messages in this state will not be sent again and may be eligible
	/// for cleanup after a retention period.
	/// </remarks>
	Sent = 2,

	/// <summary>
	/// The message delivery failed and may be eligible for retry.
	/// </summary>
	/// <remarks>
	/// Messages in this state have experienced delivery failures but may be retried based on retry policies. After maximum retries are
	/// exceeded, they become permanently failed and may require manual intervention.
	/// </remarks>
	Failed = 3,

	/// <summary>
	/// The message was partially delivered (some transports succeeded, some failed).
	/// </summary>
	/// <remarks>
	/// This state applies to multi-transport scenarios where a message needs to be delivered
	/// to multiple transports. Some transports succeeded while others failed. Failed transports
	/// may be retried while successful ones are not repeated.
	/// </remarks>
	PartiallyFailed = 4,

	/// <summary>
	/// The message has permanently failed delivery after exhausting its retry policy and has been dead-lettered.
	/// </summary>
	/// <remarks>
	/// This is a terminal state. A dead-lettered message has been routed to the dead-letter queue and MUST NOT be
	/// claimed for delivery again. Outbox stores exclude this status from their claim predicate; how each one does
	/// so differs, and a store whose terminal transition removes the row satisfies it with no predicate at all.
	/// The obligation that a terminal message is not returned to the claimable set by a later completion is stated
	/// on <see cref="IOutboxStore.MarkFailedAsync"/>, which is where a store's duty is defined rather than
	/// restated here. Messages reach this state when their retry attempts are exhausted.
	/// </remarks>
	DeadLettered = 5,
}
