// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines strategies for handling messages that cannot be processed.
/// </summary>
public enum DeadLetterStrategy
{
	/// <summary>
	/// Drop the message without further action.
	/// </summary>
	Drop = 0,

	/// <summary>
	/// Move the message to a dead letter queue.
	/// </summary>
	MoveToDeadLetterQueue = 1,

	/// <summary>
	/// Retry indefinitely with backoff.
	/// </summary>
	RetryIndefinitely = 2,

	/// <summary>
	/// Invoke a custom handler for the failed message.
	/// </summary>
	CustomHandler = 3,
}
