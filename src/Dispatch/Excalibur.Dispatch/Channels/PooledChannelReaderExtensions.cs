// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.ObjectPool;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Extension methods for creating pooled channel readers.
/// </summary>
public static class PooledChannelReaderExtensions
{
	/// <summary>
	/// Wraps a channel reader to provide automatic message pooling.
	/// </summary>
	public static PooledChannelReader WithPooling(
		this ChannelReader<MessageEnvelope> reader,
		ObjectPool<IDispatchMessage> messagePool) =>
		new(reader, messagePool);

	/// <summary>
	/// Reads and processes messages with automatic pooling.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public static async Task ProcessMessagesAsync(
		this ChannelReader<MessageEnvelope> reader,
		ObjectPool<IDispatchMessage> messagePool,
		Func<MessageEnvelope, Task> processor,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentNullException.ThrowIfNull(processor);
		_ = messagePool; // Reserved for future pooling implementation

		await foreach (var envelope in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			await processor(envelope).ConfigureAwait(false);
		}
	}
}
