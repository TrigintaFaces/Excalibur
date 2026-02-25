// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Interface for handling Pub/Sub dead letter messages.
/// </summary>
public interface IPubSubDeadLetterHandler
{
	/// <summary>
	/// Handles a dead letter message.
	/// </summary>
	/// <param name="message"> The dead letter message. </param>
	/// <param name="reason"> The reason the message became a dead letter. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The action to take for the message. </returns>
	Task<DeadLetterAction> HandleDeadLetterAsync(PubSubMessage message, PoisonReason reason, CancellationToken cancellationToken);
}
