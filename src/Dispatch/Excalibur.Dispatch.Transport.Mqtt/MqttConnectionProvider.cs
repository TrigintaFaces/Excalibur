// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MQTTnet;

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// Default <see cref="IMqttConnectionProvider"/>: creates MQTTnet clients and builds their connection
/// options (TCP server, client id, optional credentials and TLS) from <see cref="MqttOptions"/>.
/// </summary>
internal sealed class MqttConnectionProvider : IMqttConnectionProvider
{
	private readonly MqttOptions _options;
	private readonly MqttClientFactory _factory = new();

	public MqttConnectionProvider(MqttOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	public IMqttClient CreateClient() => _factory.CreateMqttClient();

	public MqttClientOptions BuildClientOptions(string clientIdSuffix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(clientIdSuffix);

		// MQTT allows one session per client id; the sender and receiver are separate connections and MUST
		// present distinct client ids, or the broker evicts one — dropping the subscription and losing messages.
		// Pin MQTT 5.0: this transport relies on MQTT-5 features (shared subscriptions `$share/…`, response
		// topic, correlation data). Under the MQTTnet default (v3.1.1) a `$share/…` filter is treated as a
		// LITERAL topic name — the shared subscription silently degrades to a normal subscription on a bogus
		// topic — so the protocol version must be pinned or UseSharedSubscription is a worse false-safety.
		var builder = new MqttClientOptionsBuilder()
			.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
			.WithTcpServer(_options.Host, _options.Port)
			.WithClientId($"{_options.ClientId}-{clientIdSuffix}");

		if (!string.IsNullOrWhiteSpace(_options.Username))
		{
			builder = builder.WithCredentials(_options.Username, _options.Password);
		}

		if (_options.UseTls)
		{
			builder = builder.WithTlsOptions(o => o.UseTls());
		}

		return builder.Build();
	}
}
