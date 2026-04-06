// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>Defines a channel sender for publishing messages.</summary>
public interface IChannelSender
{
	/// <summary>
	/// Sends a message asynchronously through the channel.
	/// </summary>
	/// <typeparam name="T">The type of message to send.</typeparam>
	/// <param name="message">The message to send.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns> A <see cref="Task" /> representing the result of the asynchronous operation. </returns>
	Task SendAsync<T>(T message, CancellationToken cancellationToken);
}
