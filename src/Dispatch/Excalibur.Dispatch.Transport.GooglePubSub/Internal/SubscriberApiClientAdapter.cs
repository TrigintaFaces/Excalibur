// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>
/// Default <see cref="ISubscriberApiClientSeam"/> implementation that forwards
/// to a real <see cref="SubscriberServiceApiClient"/>. This adapter is the only
/// place in the receiver and dead letter queue paths that touches the live
/// Pub/Sub SDK client type — tests substitute at the seam, never at the SDK
/// type directly (ADR-142 §D7).
/// </summary>
internal sealed class SubscriberApiClientAdapter : ISubscriberApiClientSeam
{
	private readonly SubscriberServiceApiClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SubscriberApiClientAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying Pub/Sub subscriber service API client.</param>
	public SubscriberApiClientAdapter(SubscriberServiceApiClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public Task<PullResponse> PullAsync(PullRequest request, CancellationToken cancellationToken)
		=> _inner.PullAsync(request, cancellationToken);

	/// <inheritdoc/>
	public Task AcknowledgeAsync(string subscription, IEnumerable<string> ackIds, CancellationToken cancellationToken)
		=> _inner.AcknowledgeAsync(subscription, ackIds, cancellationToken);

	/// <inheritdoc/>
	public Task ModifyAckDeadlineAsync(string subscription, IEnumerable<string> ackIds, int ackDeadlineSeconds, CancellationToken cancellationToken)
		=> _inner.ModifyAckDeadlineAsync(subscription, ackIds, ackDeadlineSeconds, cancellationToken);

	/// <inheritdoc/>
	public Task<Subscription> GetSubscriptionAsync(SubscriptionName subscription, CancellationToken cancellationToken)
		=> _inner.GetSubscriptionAsync(subscription, cancellationToken);

	/// <inheritdoc/>
	public Task<Subscription> UpdateSubscriptionAsync(UpdateSubscriptionRequest request, CancellationToken cancellationToken)
		=> _inner.UpdateSubscriptionAsync(request, cancellationToken);
}
