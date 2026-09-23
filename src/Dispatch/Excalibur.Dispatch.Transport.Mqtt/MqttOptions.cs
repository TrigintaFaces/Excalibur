// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// Configuration options for the MQTT transport.
/// </summary>
/// <remarks>
/// MQTT is a publish/subscribe protocol. Delivery guarantees are governed by <see cref="QualityOfService"/>
/// (QoS 0/1/2). MQTT has no native competing-consumer semantics without broker-supported shared
/// subscriptions (MQTT 5); set <see cref="UseSharedSubscription"/> only against an MQTT-5 broker that
/// supports them. Request/reply uses the MQTT-5 response-topic when <see cref="ResponseTopic"/> is set.
/// </remarks>
public sealed class MqttOptions
{
	/// <summary>Gets or sets the broker host name.</summary>
	/// <value>The host name; required.</value>
	[Required]
	public string Host { get; set; } = string.Empty;

	/// <summary>Gets or sets the broker TCP port.</summary>
	/// <value>The port; must be in 1..65535. Defaults to 1883 (the MQTT default; 8883 for TLS).</value>
	public int Port { get; set; } = 1883;

	/// <summary>Gets or sets the client id presented to the broker.</summary>
	/// <value>The client id; required. Must be unique per connected client.</value>
	[Required]
	public string ClientId { get; set; } = string.Empty;

	/// <summary>Gets or sets the topic to publish to and subscribe from.</summary>
	/// <value>The topic; required.</value>
	[Required]
	public string Topic { get; set; } = string.Empty;

	/// <summary>Gets or sets the quality-of-service level (delivery guarantee).</summary>
	/// <value>The QoS level. Defaults to <see cref="MqttQualityOfService.AtLeastOnce"/> (QoS 1).</value>
	public MqttQualityOfService QualityOfService { get; set; } = MqttQualityOfService.AtLeastOnce;

	/// <summary>Gets or sets a value indicating whether the broker keeps this client's session across disconnects.</summary>
	/// <remarks>
	/// <para>
	/// This is a durability decision with a cost on the broker, and it is the setting that makes rejection with
	/// redelivery either true or false. Rejecting a message withholds its acknowledgement and relies on the broker
	/// redelivering the unacknowledged QoS 1/2 message when the session resumes. Under a clean start the broker
	/// discards the session the moment the connection closes, so there is nothing to resume and the rejected
	/// message is <b>lost</b> rather than redelivered.
	/// </para>
	/// <para>
	/// Defaults to <see langword="true"/> because this transport offers rejection with redelivery. Set it to
	/// <see langword="false"/> only if you do not rely on that: a rejected message is then dropped at the first
	/// disconnect, with no error and no warning.
	/// </para>
	/// <para>
	/// Resuming a session requires the <b>same client id</b>, so <see cref="ClientId"/> must be stable across
	/// restarts. A per-process random id satisfies validation and still cannot resume anything.
	/// </para>
	/// </remarks>
	/// <value><see langword="true"/> to keep the session; otherwise <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool PersistentSession { get; set; } = true;

	/// <summary>Gets or sets how long the broker keeps the session after the connection closes.</summary>
	/// <remarks>
	/// This is the recovery window: a subscriber that reconnects within it resumes its session and receives the
	/// messages it left unacknowledged; one that reconnects after it gets a fresh session, and anything rejected
	/// in the previous session is gone. Sized for reconnects and rolling restarts rather than for outages.
	/// Ignored when <see cref="PersistentSession"/> is <see langword="false"/>. The protocol encodes this as a
	/// whole number of seconds, so sub-second precision is not representable and values are rejected at startup
	/// if they cannot be expressed.
	/// </remarks>
	/// <value>The recovery window; must be positive and a whole number of seconds. Defaults to one hour.</value>
	public TimeSpan SessionExpiryInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>Gets or sets a value indicating whether to connect over TLS.</summary>
	/// <value><see langword="true"/> to use TLS; otherwise <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool UseTls { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether an unencrypted broker connection is refused.
	/// </summary>
	/// <value>
	/// <see langword="true"/> (the default) to refuse the connection unless <see cref="UseTls"/> is set;
	/// <see langword="false"/> to accept a plaintext connection.
	/// </value>
	/// <remarks>
	/// MQTT over plain TCP carries the user name, password and every payload in the clear, so the default
	/// posture refuses it. Set this to <see langword="false"/> only for a broker reached over a channel that
	/// is already encrypted, or for local development.
	/// </remarks>
	public bool RequireTls { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to subscribe using an MQTT-5 shared subscription so multiple
	/// consumers compete for messages. Requires an MQTT-5 broker that supports shared subscriptions.
	/// </summary>
	/// <value><see langword="true"/> to use a shared subscription; otherwise <see langword="false"/> (each
	/// subscriber receives every message). Defaults to <see langword="false"/>.</value>
	public bool UseSharedSubscription { get; set; }

	/// <summary>
	/// Gets or sets the MQTT-5 shared-subscription group name applied when <see cref="UseSharedSubscription"/>
	/// is enabled. All consumers sharing this group compete for messages on <see cref="Topic"/> (the broker
	/// load-balances across them); the receiver subscribes to <c>$share/{group}/{topic}</c>. Must be a shared,
	/// stable name — distinct from the per-instance <see cref="ClientId"/> — or the subscribers do not compete.
	/// </summary>
	/// <value>The shared-subscription group name. Defaults to <c>"dispatch"</c>.</value>
	public string SharedSubscriptionGroup { get; set; } = "dispatch";

	/// <summary>
	/// Gets or sets the MQTT-5 response topic for the request/reply pattern, or <see langword="null"/> when
	/// request/reply is not used.
	/// </summary>
	/// <value>The response topic, or <see langword="null"/>.</value>
	public string? ResponseTopic { get; set; }

	/// <summary>Gets or sets the user name for broker authentication, or <see langword="null"/> for none.</summary>
	/// <value>The user name, or <see langword="null"/>.</value>
	public string? Username { get; set; }

	/// <summary>Gets or sets the password for broker authentication, or <see langword="null"/> for none.</summary>
	/// <value>The password, or <see langword="null"/>. Source from a secret manager; never commit a value.</value>
	public string? Password { get; set; }

	/// <summary>Gets or sets the maximum accepted payload size in bytes, or <see langword="null"/> to opt out.</summary>
	/// <value>The maximum payload size in bytes, or <see langword="null"/> for no limit.</value>
	public int? MaxPayloadBytes { get; set; }
}
