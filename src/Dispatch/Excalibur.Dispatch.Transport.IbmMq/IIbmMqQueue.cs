// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using IBM.WMQ;

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// An open queue on a connected queue manager.
/// </summary>
/// <remarks>
/// The message and option types stay concrete: they are constructed in process and carry data rather than
/// a connection, so substituting them would buy nothing and cost a mapping layer on the get/put path.
/// Disposing closes the queue.
/// </remarks>
internal interface IIbmMqQueue : IDisposable
{
	/// <summary>
	/// Gets the next message, filling <paramref name="message"/> in place.
	/// </summary>
	/// <param name="message">The message to fill.</param>
	/// <param name="options">The get options, including the wait interval and syncpoint.</param>
	void Get(MQMessage message, MQGetMessageOptions options);

	/// <summary>
	/// Puts a message on the queue.
	/// </summary>
	/// <param name="message">The message to put.</param>
	/// <param name="options">The put options, including syncpoint.</param>
	void Put(MQMessage message, MQPutMessageOptions options);
}
