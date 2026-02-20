// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Detects poison messages that should not be retried.
/// </summary>
public interface IPoisonMessageDetector
{
	/// <summary>
	/// Determines if a message is a poison message.
	/// </summary>
	/// <param name="message"> The message to check. </param>
	/// <param name="exception"> The exception that occurred while processing. </param>
	/// <returns> True if the message is poisonous and should not be retried. </returns>
	bool IsPoisonMessage(PubsubMessage message, Exception exception);

	/// <summary>
	/// Records a processing failure for tracking poison messages.
	/// </summary>
	/// <param name="messageId"> The message identifier. </param>
	/// <param name="exception"> The exception that occurred. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task RecordFailureAsync(string messageId, Exception exception);

	/// <summary>
	/// Gets the failure count for a message.
	/// </summary>
	/// <param name="messageId"> The message identifier. </param>
	/// <returns> The number of times the message has failed. </returns>
	int GetFailureCount(string messageId);

	/// <summary>
	/// Resets the failure count for a message.
	/// </summary>
	/// <param name="messageId"> The message identifier. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task ResetFailureCountAsync(string messageId);
}
