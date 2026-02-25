// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Channels.Diagnostics;

/// <summary>
/// Provides OpenTelemetry-compatible metrics for channel operations using System.Diagnostics.Metrics. This follows .NET best practices by
/// using built-in APIs that OpenTelemetry can collect from, without requiring a direct dependency on OpenTelemetry.
/// </summary>
public static class ChannelMeter
{
	private static readonly Meter Meter = new(DispatchTelemetryConstants.Meters.ChannelTransport, "1.0.0");

	/// <summary>
	/// Counters for message flow.
	/// </summary>
	private static readonly Counter<long> MessagesProducedCounter = Meter.CreateCounter<long>(
		"dispatch.channel.messages.produced",
		unit: "messages",
		description: "Total number of messages produced to channels");

	private static readonly Counter<long> MessagesConsumedCounter = Meter.CreateCounter<long>(
		"dispatch.channel.messages.consumed",
		unit: "messages",
		description: "Total number of messages consumed from channels");

	private static readonly Counter<long> MessagesAcknowledgedCounter = Meter.CreateCounter<long>(
		"dispatch.channel.messages.acknowledged",
		unit: "messages",
		description: "Total number of messages successfully acknowledged");

	private static readonly Counter<long> MessagesRejectedCounter = Meter.CreateCounter<long>(
		"dispatch.channel.messages.rejected",
		unit: "messages",
		description: "Total number of messages rejected");

	private static readonly Counter<long> MessagesFailedCounter = Meter.CreateCounter<long>(
		"dispatch.channel.messages.failed",
		unit: "messages",
		description: "Total number of messages that failed processing");

	/// <summary>
	/// Histograms for performance metrics.
	/// </summary>
	private static readonly Histogram<double> ProcessingDurationHistogram = Meter.CreateHistogram<double>(
		"dispatch.channel.processing.duration",
		unit: "milliseconds",
		description: "Message processing duration");

	private static readonly Histogram<double> EnqueueDurationHistogram = Meter.CreateHistogram<double>(
		"dispatch.channel.enqueue.duration",
		unit: "milliseconds",
		description: "Time taken to enqueue a message");

	private static readonly Histogram<double> DequeueDurationHistogram = Meter.CreateHistogram<double>(
		"dispatch.channel.dequeue.duration",
		unit: "milliseconds",
		description: "Time taken to dequeue a message");

	/// <summary>
	/// Observable gauges for current state.
	/// </summary>
	private static readonly Dictionary<string, ChannelMetricsState> ChannelStates = [];

#if NET9_0_OR_GREATER


	private static readonly Lock StateLock = new();


#else


	private static readonly object StateLock = new();


#endif

	static ChannelMeter()
	{
		// Create observable gauges for queue depth
		_ = Meter.CreateObservableGauge(
			"dispatch.channel.queue.depth",
			observeValues: static () =>
			{
				lock (StateLock)
				{
					return ChannelStates.Select(static kvp => new Measurement<int>(
						kvp.Value.CurrentQueueDepth,
						new KeyValuePair<string, object?>("channel", kvp.Key)));
				}
			},
			unit: "messages",
			description: "Current number of messages in the channel");

		// Create observable gauge for max queue depth
		_ = Meter.CreateObservableGauge(
			"dispatch.channel.queue.depth.max",
			observeValues: static () =>
			{
				lock (StateLock)
				{
					return ChannelStates.Select(static kvp => new Measurement<int>(
						kvp.Value.MaxQueueDepth,
						new KeyValuePair<string, object?>("channel", kvp.Key)));
				}
			},
			unit: "messages",
			description: "Maximum queue depth reached");

		// Create observable gauge for processing rate
		_ = Meter.CreateObservableGauge(
			"dispatch.channel.processing.rate",
			observeValues: static () =>
			{
				lock (StateLock)
				{
					return ChannelStates.Select(static kvp => new Measurement<double>(
						kvp.Value.ProcessingRate,
						new KeyValuePair<string, object?>("channel", kvp.Key)));
				}
			},
			unit: "messages/second",
			description: "Current message processing rate");
	}

	/// <summary>
	/// Records that a message was produced.
	/// </summary>
	public static void RecordMessageProduced(string channelName, string? messageType = null)
	{
		var tags = CreateTags(channelName, messageType);
		MessagesProducedCounter.Add(1, tags);
	}

	/// <summary>
	/// Records that a message was consumed.
	/// </summary>
	public static void RecordMessageConsumed(string channelName, string? messageType = null)
	{
		var tags = CreateTags(channelName, messageType);
		MessagesConsumedCounter.Add(1, tags);
	}

	/// <summary>
	/// Records that a message was acknowledged.
	/// </summary>
	public static void RecordMessageAcknowledged(string channelName, string? messageType = null)
	{
		var tags = CreateTags(channelName, messageType);
		MessagesAcknowledgedCounter.Add(1, tags);
	}

	/// <summary>
	/// Records that a message was rejected.
	/// </summary>
	public static void RecordMessageRejected(string channelName, string? messageType = null)
	{
		var tags = CreateTags(channelName, messageType);
		MessagesRejectedCounter.Add(1, tags);
	}

	/// <summary>
	/// Records that a message failed processing.
	/// </summary>
	public static void RecordMessageFailed(string channelName, string? messageType = null, string? errorType = null)
	{
		var tags = new List<KeyValuePair<string, object?>> { new("channel", channelName) };

		if (!string.IsNullOrEmpty(messageType))
		{
			tags.Add(new("message_type", messageType));
		}

		if (!string.IsNullOrEmpty(errorType))
		{
			tags.Add(new("error_type", errorType));
		}

		MessagesFailedCounter.Add(1, [.. tags]);
	}

	/// <summary>
	/// Records message processing duration.
	/// </summary>
	public static void RecordProcessingDuration(string channelName, double durationMs, string? messageType = null)
	{
		var tags = CreateTags(channelName, messageType);
		ProcessingDurationHistogram.Record(durationMs, tags);
	}

	/// <summary>
	/// Records the time taken to enqueue a message.
	/// </summary>
	public static void RecordEnqueueDuration(string channelName, double durationMs) =>
		EnqueueDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("channel", channelName));

	/// <summary>
	/// Records the time taken to dequeue a message.
	/// </summary>
	public static void RecordDequeueDuration(string channelName, double durationMs) =>
		DequeueDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("channel", channelName));

	/// <summary>
	/// Updates the current state metrics for a channel.
	/// </summary>
	public static void UpdateChannelState(string channelName, int currentQueueDepth, int maxQueueDepth, double processingRate)
	{
		lock (StateLock)
		{
			ChannelStates[channelName] = new ChannelMetricsState
			{
				CurrentQueueDepth = currentQueueDepth,
				MaxQueueDepth = maxQueueDepth,
				ProcessingRate = processingRate,
			};
		}
	}

	/// <summary>
	/// Removes a channel from metric tracking.
	/// </summary>
	public static void RemoveChannel(string channelName)
	{
		lock (StateLock)
		{
			_ = ChannelStates.Remove(channelName);
		}
	}

	private static KeyValuePair<string, object?>[] CreateTags(string channelName, string? messageType)
	{
		var tags = new List<KeyValuePair<string, object?>> { new("channel", channelName) };

		if (!string.IsNullOrEmpty(messageType))
		{
			tags.Add(new("message_type", messageType));
		}

		return [.. tags];
	}

	private sealed class ChannelMetricsState
	{
		public int CurrentQueueDepth { get; init; }

		public int MaxQueueDepth { get; init; }

		public double ProcessingRate { get; init; }
	}
}
