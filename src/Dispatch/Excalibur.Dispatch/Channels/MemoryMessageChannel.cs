// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

using Excalibur.Dispatch.Options.Channels;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// A channel that works with memory message envelopes for zero-copy operations.
/// </summary>
public static class MemoryMessageChannel
{
	/// <summary>
	/// Creates a bounded channel for memory message envelopes.
	/// </summary>
	/// <param name="capacity"> The channel capacity. </param>
	/// <param name="options"> Additional channel options. </param>
	/// <returns> A channel for memory message envelopes. </returns>
	public static Channel<MessageEnvelope> CreateBounded(
		int capacity,
		BoundedDispatchChannelOptions? options = null)
	{
		BoundedChannelOptions channelOptions;

		if (options != null)
		{
			// Convert BoundedDispatchChannelOptions to BoundedChannelOptions
			channelOptions = new BoundedChannelOptions(capacity)
			{
				FullMode = options.FullMode,
				SingleReader = options.SingleReader,
				SingleWriter = options.SingleWriter,
				AllowSynchronousContinuations = options.AllowSynchronousContinuations,
			};
		}
		else
		{
			channelOptions = new BoundedChannelOptions(capacity)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = false,
				SingleWriter = false,
			};
		}

		return Channel.CreateBounded<MessageEnvelope>(channelOptions);
	}

	/// <summary>
	/// Creates an unbounded channel for memory message envelopes.
	/// </summary>
	/// <param name="options"> Channel options. </param>
	/// <returns> A channel for memory message envelopes. </returns>
	public static Channel<MessageEnvelope> CreateUnbounded(UnboundedChannelOptions? options = null)
	{
		var channelOptions = options ?? new UnboundedChannelOptions { SingleReader = false, SingleWriter = false };

		return Channel.CreateUnbounded<MessageEnvelope>(channelOptions);
	}
}
