// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;

using IBM.WMQ;

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// Default <see cref="IIbmMqConnectionProvider"/>: builds the managed-client connection properties from
/// <see cref="IbmMqOptions"/> and connects a queue manager over TCP.
/// </summary>
internal sealed class IbmMqConnectionProvider : IIbmMqConnectionProvider
{
	private readonly IbmMqOptions _options;

	public IbmMqConnectionProvider(IbmMqOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));

		// Refused here rather than at connect: this provider is the single queue-manager seam both the
		// sender and the receiver route through, and it is constructed when the transport is resolved, so
		// a plaintext registration fails while the host is starting instead of on the first put.
		if (_options.RequireTls && string.IsNullOrWhiteSpace(_options.SslCipherSpec))
		{
			throw new TransportSecurityException(
				"Cannot connect to the IBM MQ queue manager: TLS is required but no CipherSpec is configured, so "
				+ "the user id, password and every message body would cross the wire in the clear. Set "
				+ "IbmMqOptions.SslCipherSpec to the CipherSpec configured on the SVRCONN channel (for example "
				+ "ANY_TLS12_OR_HIGHER), or set IbmMqOptions.RequireTls to false to accept an unencrypted channel.")
			{
				TransportName = "IBM MQ",
				FailureReason = TransportSecurityFailureReason.TlsNotEnabled,
			};
		}
	}

	public MQQueueManager CreateQueueManager()
	{
		var properties = new Hashtable
		{
			{ MQC.HOST_NAME_PROPERTY, _options.Host },
			{ MQC.PORT_PROPERTY, _options.Port },
			{ MQC.CHANNEL_PROPERTY, _options.Channel },
			{ MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED },
		};

		if (!string.IsNullOrWhiteSpace(_options.UserId))
		{
			properties.Add(MQC.USER_ID_PROPERTY, _options.UserId);
		}

		if (!string.IsNullOrWhiteSpace(_options.Password))
		{
			properties.Add(MQC.PASSWORD_PROPERTY, _options.Password);
		}

		// The CipherSpec is what turns the channel into a TLS channel; without it the managed client
		// connects in the clear regardless of the port.
		if (!string.IsNullOrWhiteSpace(_options.SslCipherSpec))
		{
			properties.Add(MQC.SSL_CIPHER_SPEC_PROPERTY, _options.SslCipherSpec);

			if (!string.IsNullOrWhiteSpace(_options.SslPeerName))
			{
				properties.Add(MQC.SSL_PEER_NAME_PROPERTY, _options.SslPeerName);
			}
		}

		return new MQQueueManager(_options.QueueManager, properties);
	}
}
