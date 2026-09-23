// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

		// Refused here rather than at connect: this provider is the single client-options seam both the
		// sender and the receiver route through, and it is constructed when the transport is resolved, so
		// a plaintext registration fails while the host is starting instead of on the first publish.
		if (_options.RequireTls && !_options.UseTls)
		{
			throw new TransportSecurityException(
				"Cannot establish the MQTT connection: TLS is required but MqttOptions.UseTls is false, so the "
				+ "credentials and every payload would cross the wire in the clear. Set MqttOptions.UseTls to true "
				+ "(the TLS listener is normally port 8883), or set MqttOptions.RequireTls to false to accept an "
				+ "unencrypted broker connection.")
			{
				TransportName = "MQTT",
				FailureReason = TransportSecurityFailureReason.TlsNotEnabled,
			};
		}
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
			.WithClientId($"{_options.ClientId}-{clientIdSuffix}")

			// THE SESSION IS WHAT MAKES REJECTION WITH REDELIVERY TRUE.
			// The receiver rejects a message by withholding its acknowledgement, which is a promise that the broker
			// still holds it and will redeliver it when the session resumes. Under MQTT 5 a clean start discards any
			// prior session and a zero expiry interval ends the session the instant the connection closes -- so with
			// the library defaults there was no session to resume and the rejected message was silently LOST. Nothing
			// reported it: the reject call succeeded, the broker was healthy, and the message simply never came back.
			// Both settings are written explicitly on both branches, because the defect was a DEFAULT that contradicted
			// a promise made elsewhere in the package -- leaving either to the library is what allowed that to happen.
			.WithCleanStart(!_options.PersistentSession)
			.WithSessionExpiryInterval(_options.PersistentSession
				? (uint)_options.SessionExpiryInterval.TotalSeconds
				: 0u);

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
