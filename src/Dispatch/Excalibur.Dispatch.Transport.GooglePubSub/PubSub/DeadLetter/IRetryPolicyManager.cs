// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

using Polly;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Manages retry policies for dead letter queue message processing.
/// </summary>
public interface IRetryPolicyManager
{
	/// <summary>
	/// Determines if a message should be retried.
	/// </summary>
	/// <param name="message"> The message to evaluate. </param>
	/// <param name="exception"> The exception that occurred. </param>
	/// <returns> True if the message should be retried; otherwise, false. </returns>
	bool ShouldRetry(PubsubMessage message, Exception exception);

	/// <summary>
	/// Gets the retry policy for a specific message type.
	/// </summary>
	/// <param name="messageType"> The type of message. </param>
	/// <returns> The retry policy. </returns>
	IAsyncPolicy GetRetryPolicy(string messageType);

	/// <summary>
	/// Executes an action with the appropriate retry policy.
	/// </summary>
	/// <typeparam name="TResult"> The result type. </typeparam>
	/// <param name="messageType"> The message type. </param>
	/// <param name="action"> The action to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the action. </returns>
	Task<TResult> ExecuteWithRetryAsync<TResult>(
		string messageType,
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets retry statistics for monitoring.
	/// </summary>
	/// <returns> The retry statistics. </returns>
	RetryPolicyStatistics GetStatistics();
}
