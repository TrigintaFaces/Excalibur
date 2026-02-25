// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the possible processing states of a message in the inbox store.
/// </summary>
/// <remarks>
/// The inbox status tracks the lifecycle of a message from initial receipt through processing completion or failure. This enables proper
/// duplicate detection and retry handling.
/// </remarks>
public enum InboxStatus
{
	/// <summary>
	/// The message has been received and is awaiting processing.
	/// </summary>
	/// <remarks>
	/// This is the initial state when a message is first stored in the inbox. Messages in this state are eligible for immediate processing.
	/// </remarks>
	Received = 0,

	/// <summary>
	/// The message is currently being processed by a handler.
	/// </summary>
	/// <remarks>
	/// This state prevents concurrent processing of the same message by multiple instances or threads. Messages in this state should not be
	/// processed again until they transition to a final state.
	/// </remarks>
	Processing = 1,

	/// <summary>
	/// The message has been successfully processed and completed.
	/// </summary>
	/// <remarks>
	/// This is a terminal state indicating successful message handling. Messages in this state will not be processed again and may be
	/// eligible for cleanup after a retention period.
	/// </remarks>
	Processed = 2,

	/// <summary>
	/// The message processing failed and may be eligible for retry.
	/// </summary>
	/// <remarks>
	/// Messages in this state have experienced processing failures but may be retried based on retry policies. After maximum retries are
	/// exceeded, they become permanently failed.
	/// </remarks>
	Failed = 3,
}
