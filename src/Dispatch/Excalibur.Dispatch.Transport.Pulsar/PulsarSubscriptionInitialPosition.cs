// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// Where a NEW subscription begins reading a topic.
/// </summary>
/// <remarks>
/// <para>
/// This applies only when the subscription does not already exist. An established subscription
/// resumes from its own cursor and ignores this setting entirely, so changing it never rewinds or
/// skips messages for a consumer that has run before.
/// </para>
/// <para>
/// It exists because the underlying client has a default here whether or not we state one, and an
/// inherited default is one nobody chose and nobody can find. The Kafka transport already declares
/// the same semantic through <c>AutoOffsetReset</c>; this is the Pulsar analog, so the two
/// transports answer "where does a new subscriber start?" in the same place rather than one
/// answering it and the other leaving it to a library.
/// </para>
/// </remarks>
public enum PulsarSubscriptionInitialPosition
{
	/// <summary>
	/// Begin at the end of the topic: a new subscription receives only messages published after it
	/// is established. Matches the Kafka transport's <c>AutoOffsetReset</c> default and the
	/// underlying client's own default, so this is the value in force today.
	/// </summary>
	/// <remarks>
	/// Establishing a subscription is not instantaneous. Messages published between the moment a
	/// consumer is constructed and the moment its subscription exists on the broker fall before that
	/// start point and are not delivered — which is what <c>Latest</c> means, not a fault. A producer
	/// that must not lose those messages should either use <see cref="Earliest"/> or confirm the
	/// subscription exists before publishing.
	/// </remarks>
	Latest = 0,

	/// <summary>
	/// Begin at the oldest retained message: a new subscription replays everything still within the
	/// topic's retention window.
	/// </summary>
	/// <remarks>
	/// Appropriate when no message published before the consumer started may be missed. The cost is
	/// that a brand-new subscription against an established topic replays its entire retained
	/// history, which for a competing-consumer workload can be a large and surprising first batch.
	/// </remarks>
	Earliest = 1,
}
