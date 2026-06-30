// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.GooglePubSub.Internal;

/// <summary>
/// Narrow internal seam over the high-level <see cref="PublisherClient"/> publish operation used by
/// the Pub/Sub message bus. Exposes only the single-message publish so tests substitute at this
/// boundary without faking the concrete SDK client type (ADR-142 §D7).
/// </summary>
/// <remarks>
/// Distinct from <see cref="IPublisherClientSeam"/> (which wraps the low-level
/// <see cref="PublisherServiceApiClient"/> for the transport sender / dead-letter manager). The bus
/// uses the high-level <see cref="PublisherClient"/> for its automatic batching; this seam preserves
/// that while keeping the publish path testable at an internal boundary. The data-shaped
/// <see cref="PubsubMessage"/> is a property bag and crosses the seam directly.
/// </remarks>
internal interface ITopicPublisherClientSeam
{
	/// <summary>
	/// Publishes a single message to the configured topic, returning the server-assigned message id.
	/// </summary>
	/// <param name="message">The Pub/Sub message to publish.</param>
	/// <returns>The server-assigned message identifier.</returns>
	Task<string> PublishAsync(PubsubMessage message);
}
