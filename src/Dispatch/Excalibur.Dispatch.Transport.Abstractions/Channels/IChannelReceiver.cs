// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

// Note: MessagePump is now directly in Excalibur.Dispatch.Transport.Common located at /Common/MessagePump.cs

/// <summary>Defines a channel receiver for consuming messages.</summary>
public interface IChannelReceiver
{
	/// <summary>
	/// Receives a message asynchronously from the channel.
	/// </summary>
	/// <typeparam name="T">The type of message to receive.</typeparam>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns> A <see cref="Task{TResult}" /> representing the result of the asynchronous operation. </returns>
	Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken);
}
