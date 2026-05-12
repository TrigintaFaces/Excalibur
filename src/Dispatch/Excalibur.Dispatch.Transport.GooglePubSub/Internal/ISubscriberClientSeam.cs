// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>
/// Narrow internal seam over <see cref="SubscriberClient"/> used by
/// <see cref="Google.PubSubTransportSubscriber"/>. Exposes only the
/// use-case operations needed for subscribe/unsubscribe so tests can
/// substitute at this boundary without faking the concrete SDK client
/// type (ADR-142 §D7).
/// </summary>
/// <remarks>
/// Follows the COMPASS S798 msg 1712 ruling: flat use-case methods, not
/// SDK topology mirroring. The <see cref="SubscriberClient.Reply"/> enum
/// crosses the seam as a data-shaped type.
/// </remarks>
internal interface ISubscriberClientSeam
{
	/// <summary>
	/// Starts the subscriber with the given message handler callback.
	/// </summary>
	Task StartAsync(Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> handler);

	/// <summary>
	/// Stops the subscriber gracefully.
	/// </summary>
	Task StopAsync(CancellationToken cancellationToken);
}
