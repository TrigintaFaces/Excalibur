// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Pooling;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Factory for creating pooled channel infrastructure.
/// </summary>
public static class PooledChannelFactory
{
	/// <summary>
	/// Creates a channel with built-in message pooling support.
	/// </summary>
	/// <typeparam name="TMessage"> The type of messages in the channel. </typeparam>
	/// <param name="options"> The bounded channel options. </param>
	/// <param name="messagePoolManager"> The message pool manager to use. </param>
	/// <returns> A tuple containing the writer and pooled reader. </returns>
	public static (ChannelWriter<MessageEnvelope> Writer, PooledChannelReader Reader)
			CreatePooledChannel<
					[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
	TMessage>(BoundedChannelOptions options, MessagePoolService messagePoolManager)
			where TMessage : class, IDispatchMessage, new()
	{
		var channel = Channel.CreateBounded<MessageEnvelope>(options);

		// Create an adapter pool that converts between the two IDispatchMessage types
		var adapterPool = new CrossNamespaceMessagePool<TMessage>(messagePoolManager);
		var pooledReader = new PooledChannelReader(channel.Reader, adapterPool);

		return (channel.Writer, pooledReader);
	}

	/// <summary>
	/// Creates an unbounded channel with built-in message pooling support.
	/// </summary>
	public static (ChannelWriter<MessageEnvelope> Writer, PooledChannelReader Reader)
			CreateUnboundedPooledChannel<
					[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
	TMessage>(
					MessagePoolService messagePoolManager,
					bool singleReader = false,
					bool singleWriter = false)
			where TMessage : class, IDispatchMessage, new()
	{
		var options = new UnboundedChannelOptions
		{
			SingleReader = singleReader,
			SingleWriter = singleWriter,
			AllowSynchronousContinuations = false,
		};

		var channel = Channel.CreateUnbounded<MessageEnvelope>(options);

		// Create an adapter pool that converts between the two IDispatchMessage types
		var adapterPool = new CrossNamespaceMessagePool<TMessage>(messagePoolManager);
		var pooledReader = new PooledChannelReader(channel.Reader, adapterPool);

		return (channel.Writer, pooledReader);
	}

	/// <summary>
	/// Creates a dedicated pooled channel optimized for single producer/consumer scenarios.
	/// </summary>
	public static (ChannelWriter<MessageEnvelope> Writer, PooledChannelReader Reader)
			CreateDedicatedPooledChannel<
					[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
	TMessage>(int capacity, MessagePoolService messagePoolManager)
			where TMessage : class, IDispatchMessage, new()
	{
		var options = new BoundedChannelOptions(capacity)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true,
			SingleWriter = true,
			AllowSynchronousContinuations = true, // Enable for lowest latency
		};

		return CreatePooledChannel<TMessage>(options, messagePoolManager);
	}
}
