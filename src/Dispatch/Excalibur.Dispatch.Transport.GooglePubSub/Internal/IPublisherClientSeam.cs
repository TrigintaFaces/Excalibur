// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>
/// Narrow internal seam over <see cref="PublisherServiceApiClient"/> used by
/// <see cref="Google.PubSubTransportSender"/> and
/// <see cref="Google.PubSubDeadLetterQueueManager"/>. Exposes only the
/// publish operation so tests can substitute at this boundary without
/// faking the concrete SDK client type (ADR-142 §D7).
/// </summary>
/// <remarks>
/// Follows the COMPASS S798 msg 1712 ruling: flat use-case methods, not
/// SDK topology mirroring. Data-shaped SDK types
/// (<see cref="PublishRequest"/>, <see cref="PublishResponse"/>) cross the
/// seam — they are property bags and are safe to construct directly.
/// </remarks>
internal interface IPublisherClientSeam
{
	/// <summary>
	/// Publishes messages to a Pub/Sub topic using gRPC call settings.
	/// Used by the transport sender for timeout/cancellation control.
	/// </summary>
	Task<PublishResponse> PublishAsync(PublishRequest request, CallSettings callSettings);

	/// <summary>
	/// Publishes messages to a Pub/Sub topic with cancellation support.
	/// Convenience overload used by the dead letter queue manager.
	/// </summary>
	Task<PublishResponse> PublishAsync(PublishRequest request, CancellationToken cancellationToken);
}
