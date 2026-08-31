// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch;

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
	/// Claims a batch of due messages from the outbox store and publishes it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Implementations MUST win a lease before handing any message to a transport.</b> The store's claim is
	/// an atomic read-decide-write, so concurrent drains receive disjoint batches. Selecting rows with a plain
	/// read and publishing them is check-then-act and admits double delivery, however narrow the read's filter.
	/// </para>
	/// <para>
	/// The claim also decides which rows are due. It admits a message that is unclaimed (or whose lease has
	/// expired), whose scheduled time has arrived, and whose next-attempt floor has elapsed — so one drain
	/// covers immediate, scheduled and retry-eligible messages alike.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The result of the publishing operation including success/failure counts. </returns>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<PublishingResult> PublishPendingMessagesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Runs a drain pass that delivers scheduled messages whose time has arrived.
	/// </summary>
	/// <remarks>
	/// A scheduled message is delivered by the same claim as every other message — the claim admits it once its
	/// scheduled time has passed — so this is a further pass of the drain described on
	/// <see cref="PublishPendingMessagesAsync"/> and carries the same lease requirement. It is not a second,
	/// unleased path: implementations MUST NOT satisfy it with a plain read of scheduled rows. A batch drained
	/// here may therefore also contain immediate or retry-eligible messages, which is deliberate — the claim is
	/// not category-selective, and filtering a claimed batch would strand the rows it discarded under a live
	/// lease.
	/// </remarks>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The result of the publishing operation including success/failure counts. </returns>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<PublishingResult> PublishScheduledMessagesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Runs a drain pass that redelivers failed messages whose backoff floor has elapsed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A failed message becomes deliverable again only once the next-attempt floor written by the failure path
	/// has elapsed, and the claim is what enforces that. Implementations MUST NOT select failed rows with a
	/// read that ignores the floor: doing so republishes a message the same drain has just deferred, on every
	/// poll cycle, which is the zero-backoff retry loop the floor exists to prevent.
	/// </para>
	/// <para>
	/// As with <see cref="PublishScheduledMessagesAsync"/>, this is a further pass of the same claim-backed
	/// drain and may deliver messages of any category. <paramref name="maxRetries"/> is validated but does not
	/// narrow the batch; the retry ceiling is enforced by the dead-letter transition, not by the drain.
	/// </para>
	/// </remarks>
	/// <param name="maxRetries"> Maximum number of retry attempts per message. Must not be negative. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The result of the retry operation including success/failure counts. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when maxRetries is less than 0. </exception>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<PublishingResult> RetryFailedMessagesAsync(
		int maxRetries,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about the outbox publisher performance.
	/// </summary>
	/// <returns> Publishing statistics including throughput and error rates. </returns>
	PublisherStatistics GetStatistics();
}
