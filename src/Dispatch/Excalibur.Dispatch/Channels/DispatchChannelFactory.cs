// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Non-generic factory class for creating high-performance channels.
/// </summary>
public static class DispatchChannelFactory
{
	/// <summary>
	/// Creates an unbounded high-performance channel.
	/// </summary>
	/// <typeparam name="T"> The type of items in the channel. </typeparam>
	/// <param name="singleReader"> Whether the channel will have a single reader. </param>
	/// <param name="singleWriter"> Whether the channel will have a single writer. </param>
	/// <returns> A new high-performance dispatch channel. </returns>
	public static DispatchChannel<T> CreateUnbounded<T>(bool singleReader = false, bool singleWriter = false) =>
		DispatchChannel<T>.CreateUnbounded(singleReader, singleWriter);

	/// <summary>
	/// Creates a bounded high-performance channel.
	/// </summary>
	/// <typeparam name="T"> The type of items in the channel. </typeparam>
	/// <param name="capacity"> The channel capacity. </param>
	/// <param name="fullMode"> The behavior when the channel is full. </param>
	/// <param name="singleReader"> Whether the channel will have a single reader. </param>
	/// <param name="singleWriter"> Whether the channel will have a single writer. </param>
	/// <returns> A new high-performance dispatch channel. </returns>
	public static DispatchChannel<T> CreateBounded<T>(
		int capacity,
		BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
		bool singleReader = false,
		bool singleWriter = false) =>
		DispatchChannel<T>.CreateBounded(capacity, fullMode, singleReader, singleWriter);

	/// <summary>
	/// Creates a bounded high-performance channel with options.
	/// </summary>
	/// <typeparam name="T"> The type of items in the channel. </typeparam>
	/// <param name="options"> The channel options. </param>
	/// <returns> A new high-performance dispatch channel. </returns>
	public static DispatchChannel<T> CreateBounded<T>(BoundedDispatchChannelOptions options) => new(options);

	/// <summary>
	/// Creates a high-performance channel optimized for single producer/single consumer scenarios.
	/// </summary>
	/// <typeparam name="T"> The type of items in the channel. </typeparam>
	/// <param name="capacity"> The optional channel capacity. If null, creates an unbounded channel. </param>
	/// <returns> A new high-performance dispatch channel. </returns>
	public static DispatchChannel<T> CreateSingleProducerConsumer<T>(int? capacity = null)
	{
		if (capacity.HasValue)
		{
			return CreateBounded<T>(capacity.Value, BoundedChannelFullMode.Wait, singleReader: true, singleWriter: true);
		}

		return CreateUnbounded<T>(singleReader: true, singleWriter: true);
	}

	/// <summary>
	/// Creates a high-performance channel with custom options.
	/// </summary>
	/// <typeparam name="T"> The type of items in the channel. </typeparam>
	/// <param name="options"> The channel options. </param>
	/// <returns> A new high-performance dispatch channel. </returns>
	public static DispatchChannel<T> CreateCustom<T>(DispatchChannelOptions options) => new(options);

	/// <summary>
	/// Creates a high-performance channel with custom options and spin wait configuration.
	/// </summary>
	/// <typeparam name="T"> The type of items in the channel. </typeparam>
	/// <param name="options"> The channel options. </param>
	/// <param name="spinWaitOptions"> The spin wait options. </param>
	/// <returns> A new high-performance dispatch channel. </returns>
	public static DispatchChannel<T> CreateCustom<T>(DispatchChannelOptions options, SpinWaitOptions? spinWaitOptions)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (spinWaitOptions != null)
		{
			options.WaitStrategy = new HybridWaitStrategy(spinWaitOptions.SpinCount, spinWaitOptions.DelayMilliseconds);
		}

		return new DispatchChannel<T>(options);
	}

	/// <summary>
	/// Creates a high-performance channel from standard bounded channel options.
	/// </summary>
	/// <typeparam name="T"> The type of items in the channel. </typeparam>
	/// <param name="boundedOptions"> The bounded channel options. </param>
	/// <param name="spinWaitOptions"> The spin wait options. </param>
	/// <returns> A new high-performance dispatch channel. </returns>
	public static DispatchChannel<T> CreateCustom<T>(BoundedDispatchChannelOptions boundedOptions, SpinWaitOptions? spinWaitOptions)
	{
		ArgumentNullException.ThrowIfNull(boundedOptions);

		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = boundedOptions.Capacity,
			FullMode = boundedOptions.FullMode,
			SingleReader = boundedOptions.SingleReader,
			SingleWriter = boundedOptions.SingleWriter,
			AllowSynchronousContinuations = boundedOptions.AllowSynchronousContinuations,
		};

		if (spinWaitOptions != null)
		{
			options.WaitStrategy = new HybridWaitStrategy(spinWaitOptions.SpinCount, spinWaitOptions.DelayMilliseconds);
		}

		return new DispatchChannel<T>(options);
	}
}
