// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Actions that can be taken for dead letter messages.
/// </summary>
public enum DeadLetterAction
{
	/// <summary>
	/// Acknowledge the message and remove it from the dead letter queue.
	/// </summary>
	Acknowledge = 0,

	/// <summary>
	/// Reject the message and leave it in the dead letter queue.
	/// </summary>
	Reject = 1,

	/// <summary>
	/// Retry processing the message.
	/// </summary>
	Retry = 2,

	/// <summary>
	/// Archive the message to long-term storage.
	/// </summary>
	Archive = 3,
}
