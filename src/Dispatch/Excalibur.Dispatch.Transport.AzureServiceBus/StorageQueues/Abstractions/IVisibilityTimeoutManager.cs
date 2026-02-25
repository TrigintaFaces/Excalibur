// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Storage.Queues.Models;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Manages visibility timeout renewal for Azure Storage Queue messages.
/// </summary>
public interface IVisibilityTimeoutManager
{
	/// <summary>
	/// Starts automatic renewal of message visibility timeout.
	/// </summary>
	/// <param name="message"> The queue message to renew. </param>
	/// <param name="renewalIntervalSeconds"> The interval between renewals in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the renewal operation. </returns>
	Task StartRenewalAsync(QueueMessage message, int renewalIntervalSeconds, CancellationToken cancellationToken);

	/// <summary>
	/// Updates the visibility timeout for a specific message.
	/// </summary>
	/// <param name="messageId"> The message ID. </param>
	/// <param name="popReceipt"> The message pop receipt. </param>
	/// <param name="visibilityTimeout"> The new visibility timeout. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the update operation. </returns>
	Task UpdateVisibilityAsync(string messageId, string popReceipt, TimeSpan visibilityTimeout,
		CancellationToken cancellationToken);

	/// <summary>
	/// Stops renewal for a specific message.
	/// </summary>
	/// <param name="messageId"> The message ID to stop renewal for. </param>
	void StopRenewal(string messageId);

	/// <summary>
	/// Stops all active renewals.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the stop operation. </returns>
	Task StopAllRenewalsAsync(CancellationToken cancellationToken);
}
