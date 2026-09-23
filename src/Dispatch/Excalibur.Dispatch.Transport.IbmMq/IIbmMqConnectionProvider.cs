// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// Creates connected queue managers from the configured <see cref="IbmMqOptions"/>. The transport sender
/// and receiver build their get/put operations on the queue managers this provides.
/// </summary>
internal interface IIbmMqConnectionProvider
{
	/// <summary>
	/// Creates and connects a new managed-client queue manager using the configured connection settings.
	/// </summary>
	/// <returns>A connected queue manager. The caller owns its lifetime and must dispose it when done.</returns>
	IIbmMqQueueManager CreateQueueManager();
}
