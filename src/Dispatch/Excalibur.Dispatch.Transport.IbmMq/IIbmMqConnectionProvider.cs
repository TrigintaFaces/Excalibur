// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using IBM.WMQ;

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// Creates connected IBM MQ queue managers from the configured <see cref="IbmMqOptions"/>. The transport
/// sender and receiver build their get/put operations on the queue managers this provides.
/// </summary>
public interface IIbmMqConnectionProvider
{
	/// <summary>
	/// Creates and connects a new managed-client queue manager using the configured connection settings.
	/// </summary>
	/// <returns>A connected <see cref="MQQueueManager"/>. The caller owns its lifetime and must
	/// <see cref="MQQueueManager.Disconnect"/> and close it when done.</returns>
	MQQueueManager CreateQueueManager();
}
