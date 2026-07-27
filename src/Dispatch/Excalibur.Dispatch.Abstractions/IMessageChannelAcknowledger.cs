// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Defines the acknowledgement contract for a message channel.
/// </summary>
/// <typeparam name="TMessage"> The type of message handled by the channel. </typeparam>
public interface IMessageChannelAcknowledger<TMessage>
	where TMessage : class
{
	/// <summary>
	/// Acknowledges successful processing of a message.
	/// </summary>
	/// <param name="message"> The message to acknowledge. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous acknowledge operation. </returns>
	Task AcknowledgeAsync(TMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Rejects a message, potentially moving it to a dead letter queue.
	/// </summary>
	/// <param name="message"> The message to reject. </param>
	/// <param name="reason"> The reason for rejection. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous reject operation. </returns>
	Task RejectAsync(TMessage message, string reason, CancellationToken cancellationToken);
}
