// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Defines an adapter for message channel operations.
/// </summary>
/// <typeparam name="TMessage"> The type of message to adapt. </typeparam>
public interface IMessageChannelAdapter<TMessage>
{
	/// <summary>
	/// Gets the channel reader.
	/// </summary>
	/// <value>
	/// The channel reader.
	/// </value>
	ChannelReader<TMessage> Reader { get; }

	/// <summary>
	/// Gets the channel writer.
	/// </summary>
	/// <value>
	/// The channel writer.
	/// </value>
	ChannelWriter<TMessage> Writer { get; }

	/// <summary>
	/// Writes a message to the channel.
	/// </summary>
	/// <param name="message"> The message to write. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the write operation. </returns>
	Task WriteAsync(TMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Reads a message from the channel.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The message read from the channel. </returns>
	Task<TMessage> ReadAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Tries to read a message from the channel without blocking.
	/// </summary>
	/// <param name="message"> The message if read successfully. </param>
	/// <returns> True if a message was read; otherwise, false. </returns>
	bool TryRead(out TMessage message);
}
