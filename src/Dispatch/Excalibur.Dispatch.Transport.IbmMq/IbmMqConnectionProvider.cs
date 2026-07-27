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

		return new MQQueueManager(_options.QueueManager, properties);
	}
}
