// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides background publishing of outbound messages from the outbox store.
/// </summary>
/// <remarks>
/// The outbox publisher runs out-of-band from request processing to publish messages that were staged during business operations. This ensures:
/// <list type="bullet">
/// <item> Reliable delivery - messages are retried until successfully sent </item>
/// <item> Performance isolation - message publishing doesn't block request processing </item>
/// <item> Scalability - publishing can be scaled independently from business logic </item>
/// <item> Fault tolerance - publishing failures don't affect business operations </item>
/// </list>
/// Publishers can be run as background services, scheduled jobs, or triggered by events.
/// </remarks>
public interface IOutboxPublisher
{
	/// <summary>
	/// Stages a message in the outbox for later delivery.
	/// </summary>
	/// <param name="message">The message to publish.</param>
	/// <param name="destination">The target destination (queue, topic, or endpoint).</param>
	/// <param name="scheduledAt">Optional time when the message should be processed. If null, process immediately.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The staged outbound message for tracking.</returns>
	Task<OutboundMessage> PublishAsync(
		object message,
		string destination,
		DateTimeOffset? scheduledAt,
		CancellationToken cancellationToken);

	/// <summary>
	/// Publishes pending messages from the outbox store.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The result of the publishing operation including success/failure counts. </returns>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	Task<PublishingResult> PublishPendingMessagesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Publishes scheduled messages that are due for delivery.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The result of the publishing operation including success/failure counts. </returns>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	Task<PublishingResult> PublishScheduledMessagesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Retries failed messages that are eligible for retry.
	/// </summary>
	/// <param name="maxRetries"> Maximum number of retry attempts per message. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The result of the retry operation including success/failure counts. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when maxRetries is less than 0. </exception>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	Task<PublishingResult> RetryFailedMessagesAsync(
		int maxRetries,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about the outbox publisher performance.
	/// </summary>
	/// <returns> Publishing statistics including throughput and error rates. </returns>
	PublisherStatistics GetStatistics();
}
