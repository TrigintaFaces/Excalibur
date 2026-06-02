// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>
/// Narrow internal seam over <see cref="SubscriberServiceApiClient"/> used by
/// <see cref="Google.PubSubTransportReceiver"/> and
/// <see cref="Google.PubSubDeadLetterQueueManager"/>. Exposes only the
/// use-case operations needed for pull-based subscribe, acknowledge, and
/// dead letter management so tests can substitute at this boundary without
/// faking the concrete SDK client type (ADR-142 §D7).
/// </summary>
/// <remarks>
/// Follows the COMPASS S798 msg 1712 ruling: flat use-case methods, not
/// SDK topology mirroring. Data-shaped SDK types
/// (<see cref="PullRequest"/>, <see cref="PullResponse"/>,
/// <see cref="Subscription"/>, <see cref="UpdateSubscriptionRequest"/>)
/// cross the seam — they are property bags and are safe to construct directly.
/// </remarks>
internal interface ISubscriberApiClientSeam
{
	/// <summary>
	/// Pulls messages from a subscription.
	/// </summary>
	Task<PullResponse> PullAsync(PullRequest request, CancellationToken cancellationToken);

	/// <summary>
	/// Acknowledges one or more messages by their ack IDs.
	/// </summary>
	Task AcknowledgeAsync(string subscription, IEnumerable<string> ackIds, CancellationToken cancellationToken);

	/// <summary>
	/// Modifies the ack deadline for one or more messages.
	/// Used by the receiver to requeue (deadline=0) or extend processing time.
	/// </summary>
	Task ModifyAckDeadlineAsync(string subscription, IEnumerable<string> ackIds, int ackDeadlineSeconds, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a subscription's configuration. Used by the dead letter queue
	/// manager for policy configuration.
	/// </summary>
	Task<Subscription> GetSubscriptionAsync(SubscriptionName subscription, CancellationToken cancellationToken);

	/// <summary>
	/// Updates a subscription's configuration. Used by the dead letter queue
	/// manager to attach dead letter policies.
	/// </summary>
	Task<Subscription> UpdateSubscriptionAsync(UpdateSubscriptionRequest request, CancellationToken cancellationToken);
}
