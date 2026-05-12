// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>
/// Default <see cref="ISubscriberClientSeam"/> implementation that forwards
/// to a real <see cref="SubscriberClient"/>. This adapter is the only place
/// in the subscriber path that touches the live Pub/Sub SDK client type —
/// tests substitute at the seam, never at the SDK type directly (ADR-142 §D7).
/// </summary>
internal sealed class SubscriberClientAdapter : ISubscriberClientSeam
{
	private readonly SubscriberClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SubscriberClientAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying Pub/Sub subscriber client.</param>
	public SubscriberClientAdapter(SubscriberClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public Task StartAsync(Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> handler)
		=> _inner.StartAsync(handler);

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken)
		=> _inner.StopAsync(cancellationToken);
}
