// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Channel wrapper that adds instrumentation for performance monitoring.
/// </summary>
/// <typeparam name="T"> The type of items in the channel. </typeparam>
public sealed class InstrumentedChannel<T>
{
	// R0.8: Remove unread private members - field is kept for future use
#pragma warning disable IDE0052
	private readonly string _channelName;
#pragma warning restore IDE0052
	private readonly InternalChannelMetrics _metrics;
	private readonly Channel<T> _innerChannel;

	private InstrumentedChannel(Channel<T> innerChannel, string channelName, int capacity)
	{
		_innerChannel = innerChannel ?? throw new ArgumentNullException(nameof(innerChannel));
		_channelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
		_metrics = new InternalChannelMetrics(channelName, capacity);
		Reader = new InstrumentedChannelReader<T>(_innerChannel.Reader, _metrics);
		Writer = new InstrumentedChannelWriter<T>(_innerChannel.Writer, _metrics);
	}

	/// <summary>
	/// Gets the instrumented channel reader.
	/// </summary>
	/// <value>The current <see cref="Reader"/> value.</value>
	public ChannelReader<T> Reader { get; }

	/// <summary>
	/// Gets the instrumented channel writer.
	/// </summary>
	/// <value>The current <see cref="Writer"/> value.</value>
	public ChannelWriter<T> Writer { get; }

	/// <summary>
	/// Creates an instrumented channel.
	/// </summary>
	public static InstrumentedChannel<T> Create(Channel<T> innerChannel, string channelName, int capacity) =>
		new(innerChannel, channelName, capacity);

	/// <summary>
	/// Gets the channel metrics.
	/// </summary>
	public ChannelMetrics GetMetrics()
	{
		// Return metrics in the format expected by ChannelEventSource
		var totalWrites = _metrics.TotalWrites;
		var messagesPerSecond = totalWrites > 0 ? totalWrites / 1.0 : 0;

		return new ChannelMetrics { MessagesPerSecond = messagesPerSecond, AverageLatencyMs = 0.0, P99LatencyMs = 0.0 };
	}

	/// <summary>
	/// Resets the channel metrics.
	/// </summary>
	public void ResetMetrics() => _metrics.Reset();
}
