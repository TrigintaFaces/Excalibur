// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Action to take after processing a received message in an <see cref="ITransportSubscriber"/> handler.
/// </summary>
public enum MessageAction
{
	/// <summary>
	/// Acknowledge the message (mark as successfully processed).
	/// </summary>
	Acknowledge,

	/// <summary>
	/// Reject the message (mark as failed, do not requeue).
	/// </summary>
	Reject,

	/// <summary>
	/// Requeue the message for redelivery.
	/// </summary>
	Requeue,
}
