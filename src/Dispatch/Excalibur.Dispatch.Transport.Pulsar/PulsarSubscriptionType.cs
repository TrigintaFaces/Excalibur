// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// The Pulsar subscription type, controlling how consumers sharing a
/// <see cref="PulsarOptions.SubscriptionName"/> divide a topic's messages.
/// </summary>
/// <remarks>
/// Maps one-to-one to the underlying client's subscription-type enum. The default
/// (<see cref="Shared"/>) matches the framework's competing-consumer transport semantics — the
/// Pulsar analog of a Kafka consumer group.
/// </remarks>
public enum PulsarSubscriptionType
{
	/// <summary>
	/// Competing consumers: messages are delivered round-robin across all consumers on the
	/// subscription. This is the default and matches consumer-group transport semantics.
	/// </summary>
	Shared = 0,

	/// <summary>
	/// A single consumer holds the subscription; additional consumers are rejected.
	/// </summary>
	Exclusive = 1,

	/// <summary>
	/// One active consumer receives messages; the others stand by for failover.
	/// </summary>
	Failover = 2,

	/// <summary>
	/// Messages with the same key are delivered to the same consumer, preserving per-key ordering
	/// while distributing keys across consumers.
	/// </summary>
	KeyShared = 3,
}
