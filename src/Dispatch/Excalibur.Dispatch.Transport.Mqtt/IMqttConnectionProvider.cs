// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using MQTTnet;

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// Creates MQTT clients and their connection options from the configured <see cref="MqttOptions"/>. The
/// transport publisher and subscriber build their publish/subscribe operations on what this provides.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a supported extension point, and it is public deliberately.</b> Substitute it to supply
/// connection configuration that <see cref="MqttOptions"/> cannot express — <b>mutual TLS</b>, where the
/// client presents its own certificate, and <b>private-CA validation</b>, where the broker's chain is
/// verified against a root the machine store does not carry. An options surface cannot anticipate every
/// PKI arrangement, so the escape hatch exists rather than growing the option surface to meet each one.
/// Register your implementation against the transport's name before calling <c>AddMqttTransport</c>;
/// that registration wins, and the sender and receiver both build their connections from it.
/// </para>
/// <para>
/// <b>What the transport does with what you return — measured against this version, not aspirational.</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <b>One provider instance serves both the sender and the receiver</b>, which are separate connections.
/// <see cref="CreateClient"/> is therefore called twice on the same instance and MUST return a distinct
/// client each time. Returning a shared or cached client gives both roles one MQTT session, and MQTT
/// permits only one live session per client id, so the broker evicts one of them.
/// </item>
/// <item>
/// <b>The transport disposes each client it is given</b> when the sender or receiver is disposed. It does
/// not check ownership, so a client you keep a reference to is disposed out from under you. Return a
/// fresh instance and do not retain it.
/// </item>
/// <item>
/// <b>The transport never disposes the provider itself.</b> Its lifetime is the container's; a provider
/// holding disposable state should be registered so the container disposes it.
/// </item>
/// <item>
/// <b><see cref="CreateClient"/> is called at most once per role</b>, and never again for the life of
/// that sender or receiver. A dropped connection is re-established on the SAME client instance, so the
/// provider cannot swap the client out on reconnect.
/// </item>
/// <item>
/// <b><see cref="BuildClientOptions"/> is called on every connect, including every reconnect.</b> This is
/// the hook that makes credential and certificate rotation work: material read here is re-read each time
/// the connection is re-established, so a certificate that expires mid-session is replaced on reconnect
/// without restarting the host.
/// </item>
/// </list>
/// </remarks>
public interface IMqttConnectionProvider
{
	/// <summary>Creates a new, unconnected MQTT client.</summary>
	/// <returns>A new <see cref="IMqttClient"/>. The caller owns its lifetime and must dispose it.</returns>
	IMqttClient CreateClient();

	/// <summary>
	/// Builds the client connection options (server, client id, credentials, TLS) from configuration.
	/// </summary>
	/// <param name="clientIdSuffix">
	/// A short role discriminator (e.g. <c>"pub"</c>/<c>"sub"</c>) appended to the configured client id. MQTT
	/// permits only one live session per client id, so the sender and receiver — separate connections — MUST
	/// use distinct client ids or the broker evicts one session (dropping the subscriber and losing messages).
	/// </param>
	/// <returns>The built <see cref="MqttClientOptions"/> with a client id unique to the role.</returns>
	MqttClientOptions BuildClientOptions(string clientIdSuffix);
}
