// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MQTTnet;

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// Creates MQTT clients and their connection options from the configured <see cref="MqttOptions"/>. The
/// transport publisher and subscriber build their publish/subscribe operations on what this provides.
/// </summary>
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
