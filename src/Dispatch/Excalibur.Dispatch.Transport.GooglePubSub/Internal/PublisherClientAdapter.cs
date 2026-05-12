// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>
/// Default <see cref="IPublisherClientSeam"/> implementation that forwards
/// to a real <see cref="PublisherServiceApiClient"/>. This adapter is the
/// only place in the publish path that touches the live Pub/Sub SDK client
/// type — tests substitute at the seam, never at the SDK type directly
/// (ADR-142 §D7).
/// </summary>
internal sealed class PublisherClientAdapter : IPublisherClientSeam
{
	private readonly PublisherServiceApiClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="PublisherClientAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying Pub/Sub publisher service API client.</param>
	public PublisherClientAdapter(PublisherServiceApiClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public Task<PublishResponse> PublishAsync(PublishRequest request, CallSettings callSettings)
		=> _inner.PublishAsync(request, callSettings);

	/// <inheritdoc/>
	public Task<PublishResponse> PublishAsync(PublishRequest request, CancellationToken cancellationToken)
		=> _inner.PublishAsync(request, cancellationToken);
}
