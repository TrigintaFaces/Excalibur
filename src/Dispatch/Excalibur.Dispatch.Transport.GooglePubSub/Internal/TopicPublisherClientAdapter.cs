// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>
/// Default <see cref="ITopicPublisherClientSeam"/> implementation that forwards to a real
/// high-level <see cref="PublisherClient"/>. This adapter is the only place in the bus publish path
/// that touches the live Pub/Sub SDK client type — tests substitute at the seam, never at the SDK
/// type directly (ADR-142 §D7).
/// </summary>
internal sealed class TopicPublisherClientAdapter : ITopicPublisherClientSeam
{
	private readonly PublisherClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="TopicPublisherClientAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying high-level Pub/Sub publisher client.</param>
	public TopicPublisherClientAdapter(PublisherClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public Task<string> PublishAsync(PubsubMessage message) => _inner.PublishAsync(message);
}
