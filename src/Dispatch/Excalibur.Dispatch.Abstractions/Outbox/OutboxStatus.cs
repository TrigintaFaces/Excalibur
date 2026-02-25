// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

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
	/// This state prevents concurrent delivery attempts of the same message by multiple background services or threads. Messages in this
	/// state should not be processed again until they transition to a final state.
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
}
