// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// High-performance channel optimized for low-latency scenarios.
/// </summary>
/// <typeparam name="T"> The type of items in the channel. </typeparam>
public sealed class DispatchChannel<T> : IDisposable
{
	/// <summary>
	/// Cached composite format for string formatting performance.
	/// </summary>
	private static readonly CompositeFormat UnsupportedChannelModeFormat = CompositeFormat.Parse(ErrorConstants.UnsupportedChannelMode);

	private readonly IWaitStrategy _waitStrategy;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchChannel{T}"/> class.
	/// </summary>
	/// <param name="options"> The channel options. </param>
	public DispatchChannel(DispatchChannelOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Create the underlying channel based on options
		InnerChannel = options.Mode switch
		{
			ChannelMode.Unbounded => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
			{
				SingleReader = options.SingleReader,
				SingleWriter = options.SingleWriter,
				AllowSynchronousContinuations = options.AllowSynchronousContinuations,
			}),
			ChannelMode.Bounded => Channel.CreateBounded<T>(new BoundedChannelOptions(options.Capacity ?? 1000)
			{
				FullMode = options.FullMode,
				SingleReader = options.SingleReader,
				SingleWriter = options.SingleWriter,
				AllowSynchronousContinuations = options.AllowSynchronousContinuations,
			}),
			_ => throw new ArgumentException(
				string.Format(CultureInfo.InvariantCulture, UnsupportedChannelModeFormat, options.Mode),
				nameof(options)),
		};

		_waitStrategy = options.WaitStrategy ?? new HybridWaitStrategy();

		Reader = new DispatchChannelReader<T>(InnerChannel.Reader, _waitStrategy);
		Writer = new DispatchChannelWriter<T>(InnerChannel.Writer, _waitStrategy);
	}

	/// <summary>
	/// Gets the channel reader.
	/// </summary>
	/// <value>The current <see cref="Reader"/> value.</value>
	public DispatchChannelReader<T> Reader { get; }

	/// <summary>
	/// Gets the channel writer.
	/// </summary>
	/// <value>The current <see cref="Writer"/> value.</value>
	public DispatchChannelWriter<T> Writer { get; }

	/// <summary>
	/// Gets the inner channel.
	/// </summary>
	private Channel<T> InnerChannel { get; }

	/// <summary>
	/// Creates an unbounded high-performance channel.
	/// </summary>
	/// <param name="singleReader"> Whether the channel will have a single reader. </param>
	/// <param name="singleWriter"> Whether the channel will have a single writer. </param>
	/// <returns> A new unbounded dispatch channel. </returns>
	public static DispatchChannel<T> CreateUnbounded(bool singleReader = false, bool singleWriter = false) =>
		new(new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
			SingleReader = singleReader,
			SingleWriter = singleWriter,
			AllowSynchronousContinuations = true,
		});

	/// <summary>
	/// Creates a bounded high-performance channel.
	/// </summary>
	/// <param name="capacity"> The channel capacity. </param>
	/// <param name="fullMode"> The behavior when the channel is full. </param>
	/// <param name="singleReader"> Whether the channel will have a single reader. </param>
	/// <param name="singleWriter"> Whether the channel will have a single writer. </param>
	/// <returns> A new high-performance channel. </returns>
	public static DispatchChannel<T> CreateBounded(
		int capacity,
		BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
		bool singleReader = false,
		bool singleWriter = false) =>
		new(new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = capacity,
			FullMode = fullMode,
			SingleReader = singleReader,
			SingleWriter = singleWriter,
			AllowSynchronousContinuations = true,
		});

	/// <summary>
	/// Returns this channel as a base Channel&lt;T&gt; instance.
	/// </summary>
	/// <returns> The channel as Channel&lt;T&gt;. </returns>
	public Channel<T> AsChannel() => InnerChannel;

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_waitStrategy?.Dispose();
			_disposed = true;
		}
	}
}
