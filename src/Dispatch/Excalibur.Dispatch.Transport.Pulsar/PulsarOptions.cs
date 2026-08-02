// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// Configuration options for the Apache Pulsar transport.
/// </summary>
/// <remarks>
/// Mirrors the native configuration surface of the underlying Pulsar client (service URL, topic,
/// subscription identity and type, and receive tuning) so consumers can tune the transport without
/// reaching for a second options object.
/// </remarks>
public sealed class PulsarOptions
{
	/// <summary>
	/// Gets or sets the Pulsar broker service URL.
	/// </summary>
	/// <value>The Pulsar service URL, for example <c>pulsar://localhost:6650</c>.</value>
	[Required]
	public string ServiceUrl { get; set; } = "pulsar://localhost:6650";

	/// <summary>
	/// Gets or sets the Pulsar topic name.
	/// </summary>
	/// <value>The Pulsar topic name.</value>
	public string Topic { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the subscription name — the durable consumer identity shared by competing
	/// consumers (the Pulsar analog of a Kafka consumer group).
	/// </summary>
	/// <value>The subscription name.</value>
	public string SubscriptionName { get; set; } = "dispatch-subscription";

	/// <summary>
	/// Gets or sets the subscription type controlling message distribution across consumers on the
	/// subscription. Defaults to <see cref="PulsarSubscriptionType.Shared"/> (competing consumers).
	/// </summary>
	/// <value>The subscription type.</value>
	public PulsarSubscriptionType SubscriptionType { get; set; } = PulsarSubscriptionType.Shared;

	/// <summary>
	/// Gets or sets where a NEW subscription starts reading. Defaults to
	/// <see cref="PulsarSubscriptionInitialPosition.Latest" />, which is the value already in force —
	/// this makes it stated rather than inherited from the client.
	/// </summary>
	/// <remarks>
	/// Ignored by a subscription that already exists: it resumes from its own cursor. The Kafka
	/// transport expresses the same choice through its offset-reset option; declaring it here means a
	/// reader can answer "where does a new subscriber begin?" from our options rather than from a
	/// third-party library's defaults.
	/// </remarks>
	/// <value>The initial read position for a new subscription.</value>
	public PulsarSubscriptionInitialPosition SubscriptionInitialPosition { get; set; } =
		PulsarSubscriptionInitialPosition.Latest;

	/// <summary>
	/// Gets or sets the receive tuning options for batching and payload limits.
	/// </summary>
	/// <value>The receive tuning options.</value>
	public PulsarReceiveTuningOptions Receive { get; set; } = new();
}

/// <summary>
/// Receive-side tuning options for the Pulsar transport.
/// </summary>
public sealed class PulsarReceiveTuningOptions
{
	/// <summary>
	/// Gets or sets the maximum number of messages to drain per receive call. Caps the per-call batch
	/// even when the caller requests more.
	/// </summary>
	/// <value>The maximum receive batch size. Must be at least 1. Defaults to 10.</value>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum accepted payload size in bytes, or <see langword="null"/> to opt
	/// out of the payload-size limit.
	/// </summary>
	/// <value>
	/// The maximum payload size in bytes, or <see langword="null"/> for no limit. Defaults to
	/// <see cref="PayloadSizeGuard.DefaultMaxPayloadBytes"/> (4 MiB), matching the other transports so the
	/// poison-message guard is active by default.
	/// </value>
	[Range(1, int.MaxValue)]
	public int? MaxPayloadBytes { get; set; } = PayloadSizeGuard.DefaultMaxPayloadBytes;
}
