// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Channels.Diagnostics;

/// <summary>
/// ETW event source for channel performance metrics and diagnostics.
/// </summary>
[EventSource(Name = "Excalibur-Dispatch-Channels", Guid = "a3f87db5-0cba-4e4e-b712-439980e59870")]
public sealed class ChannelEventSource : EventSource
{
	/// <summary>
	/// Singleton instance of the event source.
	/// </summary>
	public static readonly ChannelEventSource Log = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ChannelEventSource"/> class.
	/// Prevent external instantiation.
	/// </summary>
	private ChannelEventSource()
		: base(EventSourceSettings.EtwSelfDescribingEventFormat)
	{
	}

	/// <summary>
	/// Gets the channel type name for a generic type.
	/// </summary>
	[NonEvent]
	public static string GetChannelTypeName<T>() => typeof(T).Name;

	/// <summary>
	/// Writes channel metrics to the event source.
	/// </summary>
	[NonEvent]
	public void WriteChannelMetrics(string channelType, ChannelMetrics metrics)
	{
		ArgumentNullException.ThrowIfNull(metrics);

		if (IsEnabled(EventLevel.Informational, Keywords.Performance))
		{
			CustomMetric($"{channelType}.MessagesPerSecond", metrics.MessagesPerSecond, "msg/s");
			CustomMetric($"{channelType}.AverageLatencyMs", metrics.AverageLatencyMs, "ms");
			CustomMetric($"{channelType}.P99LatencyMs", metrics.P99LatencyMs, "ms");
		}
	}

	/// <summary>
	/// Logs spin-wait statistics for a channel.
	/// </summary>
	[Event(60, Level = EventLevel.Verbose, Keywords = Keywords.Performance | Keywords.Diagnostics)]
	public void SpinWaitStatistics(string channelId, int spinIterations, double spinTimeoutMs, bool aggressiveSpinning)
	{
		if (IsEnabled())
		{
			WriteEvent(60, channelId, spinIterations, spinTimeoutMs, aggressiveSpinning);
		}
	}

	/// <summary>
	/// Keywords for filtering events.
	/// </summary>
	internal static class Keywords
	{
		public const EventKeywords Read = (EventKeywords)0x0001;
		public const EventKeywords Write = (EventKeywords)0x0002;
		public const EventKeywords Channel = (EventKeywords)0x0004;
		public const EventKeywords Performance = (EventKeywords)0x0008;
		public const EventKeywords Diagnostics = (EventKeywords)0x0010;
		public const EventKeywords Errors = (EventKeywords)0x0020;
	}

	/// <summary>
	/// Event tasks for grouping related events.
	/// </summary>
	internal static class EventTasks
	{
		public const EventTask ChannelOperation = (EventTask)1;
		public const EventTask MessageFlow = (EventTask)2;
		public const EventTask Performance = (EventTask)3;
	}


	/// <summary>
	/// Logs when a channel is created.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="capacity"> The capacity of the channel. </param>
	/// <param name="channelType"> The type of the channel. </param>
	/// <param name="waitStrategy"> The wait strategy used by the channel. </param>
	[Event(1, Level = EventLevel.Informational, Keywords = Keywords.Channel, Task = EventTasks.ChannelOperation)]
	public void ChannelCreated(string channelId, int capacity, string channelType, string waitStrategy)
	{
		if (IsEnabled())
		{
			WriteEvent(1, channelId, capacity, channelType, waitStrategy);
		}
	}

	/// <summary>
	/// Logs when a channel is completed.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="totalMessagesProcessed"> Total number of messages processed. </param>
	/// <param name="lifetimeSeconds"> Lifetime of the channel in seconds. </param>
	[Event(2, Level = EventLevel.Informational, Keywords = Keywords.Channel, Task = EventTasks.ChannelOperation)]
	public void ChannelCompleted(string channelId, long totalMessagesProcessed, double lifetimeSeconds)
	{
		if (IsEnabled())
		{
			WriteEvent(2, channelId, totalMessagesProcessed, lifetimeSeconds);
		}
	}

	/// <summary>
	/// Logs when a channel encounters a fault or error.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="errorMessage"> The error message describing the fault. </param>
	[Event(3, Level = EventLevel.Warning, Keywords = Keywords.Channel | Keywords.Errors, Task = EventTasks.ChannelOperation)]
	public void ChannelFaulted(string channelId, string errorMessage)
	{
		if (IsEnabled())
		{
			WriteEvent(3, channelId, errorMessage);
		}
	}



	/// <summary>
	/// Logs when a write operation starts.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="messageSize"> The size of the message being written. </param>
	[Event(10, Level = EventLevel.Verbose, Keywords = Keywords.Write, Task = EventTasks.MessageFlow)]
	public void WriteStarted(string channelId, int messageSize)
	{
		if (IsEnabled(EventLevel.Verbose, Keywords.Write))
		{
			WriteEvent(10, channelId, messageSize);
		}
	}

	/// <summary>
	/// Logs when a write operation completes.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="elapsedMicroseconds"> Time elapsed for the write operation in microseconds. </param>
	[Event(11, Level = EventLevel.Verbose, Keywords = Keywords.Write, Task = EventTasks.MessageFlow)]
	public void WriteCompleted(string channelId, double elapsedMicroseconds)
	{
		if (IsEnabled(EventLevel.Verbose, Keywords.Write))
		{
			WriteEvent(11, channelId, elapsedMicroseconds);
		}
	}

	/// <summary>
	/// Logs when a write operation is blocked due to channel capacity.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="currentCount"> Current number of items in the channel. </param>
	/// <param name="capacity"> Maximum capacity of the channel. </param>
	[Event(12, Level = EventLevel.Warning, Keywords = Keywords.Write | Keywords.Performance, Task = EventTasks.MessageFlow)]
	public void WriteBlocked(string channelId, int currentCount, int capacity)
	{
		if (IsEnabled())
		{
			WriteEvent(12, channelId, currentCount, capacity);
		}
	}

	/// <summary>
	/// Logs when a write operation fails.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="reason"> The reason for the write failure. </param>
	[Event(13, Level = EventLevel.Error, Keywords = Keywords.Write | Keywords.Errors, Task = EventTasks.MessageFlow)]
	public void WriteFailed(string channelId, string reason)
	{
		if (IsEnabled())
		{
			WriteEvent(13, channelId, reason);
		}
	}

	/// <summary>
	/// Records write operation metrics including message size and elapsed time.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="messageSize"> The size of the message being written. </param>
	/// <param name="elapsedMicroseconds"> Time elapsed for the write operation in microseconds. </param>
	[NonEvent]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteMetrics(string channelId, int messageSize, double elapsedMicroseconds)
	{
		if (IsEnabled(EventLevel.Verbose, Keywords.Write))
		{
			WriteStarted(channelId, messageSize);
			WriteCompleted(channelId, elapsedMicroseconds);
		}
	}



	/// <summary>
	/// Logs when a read operation starts.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	[Event(20, Level = EventLevel.Verbose, Keywords = Keywords.Read, Task = EventTasks.MessageFlow)]
	public void ReadStarted(string channelId)
	{
		if (IsEnabled(EventLevel.Verbose, Keywords.Read))
		{
			WriteEvent(20, channelId);
		}
	}

	/// <summary>
	/// Logs when a read operation completes.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="messageSize"> The size of the message that was read. </param>
	/// <param name="elapsedMicroseconds"> Time elapsed for the read operation in microseconds. </param>
	[Event(21, Level = EventLevel.Verbose, Keywords = Keywords.Read, Task = EventTasks.MessageFlow)]
	public void ReadCompleted(string channelId, int messageSize, double elapsedMicroseconds)
	{
		if (IsEnabled(EventLevel.Verbose, Keywords.Read))
		{
			WriteEvent(21, channelId, messageSize, elapsedMicroseconds);
		}
	}

	/// <summary>
	/// Logs when readers are waiting for messages.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="waitingReaders"> The number of readers waiting for messages. </param>
	[Event(22, Level = EventLevel.Informational, Keywords = Keywords.Read | Keywords.Performance, Task = EventTasks.MessageFlow)]
	public void ReadWaiting(string channelId, int waitingReaders)
	{
		if (IsEnabled())
		{
			WriteEvent(22, channelId, waitingReaders);
		}
	}

	/// <summary>
	/// Logs when a read operation fails.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="reason"> The reason for the read failure. </param>
	[Event(23, Level = EventLevel.Error, Keywords = Keywords.Read | Keywords.Errors, Task = EventTasks.MessageFlow)]
	public void ReadFailed(string channelId, string reason)
	{
		if (IsEnabled())
		{
			WriteEvent(23, channelId, reason);
		}
	}

	/// <summary>
	/// Records read operation metrics including message size and elapsed time.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="messageSize"> The size of the message that was read. </param>
	/// <param name="elapsedMicroseconds"> Time elapsed for the read operation in microseconds. </param>
	[NonEvent]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ReadMetrics(string channelId, int messageSize, double elapsedMicroseconds)
	{
		if (IsEnabled(EventLevel.Verbose, Keywords.Read))
		{
			ReadStarted(channelId);
			ReadCompleted(channelId, messageSize, elapsedMicroseconds);
		}
	}



	/// <summary>
	/// Logs a performance snapshot for the channel.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="messagesPerSecond"> Current throughput in messages per second. </param>
	/// <param name="avgLatencyMicroseconds"> Average latency in microseconds. </param>
	[Event(30, Level = EventLevel.Informational, Keywords = Keywords.Performance, Task = EventTasks.Performance,
		Message = "Channel {0} throughput: {1} msgs/sec, avg latency: {2} Î¼s")]
	public void PerformanceSnapshot(string channelId, double messagesPerSecond, double avgLatencyMicroseconds)
	{
		if (IsEnabled())
		{
			WriteEvent(30, channelId, messagesPerSecond, avgLatencyMicroseconds);
		}
	}

	/// <summary>
	/// Logs when high latency is detected on the channel.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="p95LatencyMicroseconds"> 95th percentile latency in microseconds. </param>
	/// <param name="p99LatencyMicroseconds"> 99th percentile latency in microseconds. </param>
	[Event(31, Level = EventLevel.Warning, Keywords = Keywords.Performance, Task = EventTasks.Performance,
		Message = "Channel {0} experiencing high latency: P95={1}Î¼s, P99={2}Î¼s")]
	public void HighLatencyDetected(string channelId, double p95LatencyMicroseconds, double p99LatencyMicroseconds)
	{
		if (IsEnabled())
		{
			WriteEvent(31, channelId, p95LatencyMicroseconds, p99LatencyMicroseconds);
		}
	}

	/// <summary>
	/// Logs comprehensive channel statistics.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="totalWrites"> Total number of write operations performed. </param>
	/// <param name="totalReads"> Total number of read operations performed. </param>
	/// <param name="failedWrites"> Total number of failed write operations. </param>
	/// <param name="failedReads"> Total number of failed read operations. </param>
	/// <param name="currentCount"> Current number of items in the channel. </param>
	/// <param name="peakCount"> Peak number of items ever held in the channel. </param>
	[Event(32, Level = EventLevel.Informational, Keywords = Keywords.Performance, Task = EventTasks.Performance)]
	public void ChannelStatistics(
		string channelId,
		long totalWrites,
		long totalReads,
		long failedWrites,
		long failedReads,
		int currentCount,
		int peakCount)
	{
		if (IsEnabled())
		{
			WriteEvent(32, channelId, totalWrites, totalReads, failedWrites, failedReads, currentCount, peakCount);
		}
	}

	/// <summary>
	/// Logs when a channel is near capacity.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="percentageFull"> The percentage of capacity that is currently full. </param>
	[Event(33, Level = EventLevel.Warning, Keywords = Keywords.Performance, Task = EventTasks.Performance,
		Message = "Channel {0} is {1}% full")]
	public void ChannelNearCapacity(string channelId, int percentageFull)
	{
		if (IsEnabled())
		{
			WriteEvent(33, channelId, percentageFull);
		}
	}



	/// <summary>
	/// Logs when a wait strategy enters spinning mode.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="strategyType"> The type of wait strategy being used. </param>
	/// <param name="spinCount"> The number of spins performed. </param>
	[Event(40, Level = EventLevel.Verbose, Keywords = Keywords.Performance | Keywords.Diagnostics)]
	public void WaitStrategySpinning(string channelId, string strategyType, int spinCount)
	{
		if (IsEnabled(EventLevel.Verbose, Keywords.Performance | Keywords.Diagnostics))
		{
			WriteEvent(40, channelId, strategyType, spinCount);
		}
	}

	/// <summary>
	/// Logs when a wait strategy yields control.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="strategyType"> The type of wait strategy being used. </param>
	[Event(41, Level = EventLevel.Verbose, Keywords = Keywords.Performance | Keywords.Diagnostics)]
	public void WaitStrategyYielding(string channelId, string strategyType)
	{
		if (IsEnabled(EventLevel.Verbose, Keywords.Performance | Keywords.Diagnostics))
		{
			WriteEvent(41, channelId, strategyType);
		}
	}

	/// <summary>
	/// Logs when wait strategy contention is detected.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="strategyType"> The type of wait strategy being used. </param>
	/// <param name="contentionLevel"> The level of contention detected. </param>
	[Event(42, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Diagnostics)]
	public void WaitStrategyContention(string channelId, string strategyType, int contentionLevel)
	{
		if (IsEnabled())
		{
			WriteEvent(42, channelId, strategyType, contentionLevel);
		}
	}



	/// <summary>
	/// Logs when a batch write operation completes.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="batchSize"> The size of the batch that was written. </param>
	/// <param name="totalElapsedMicroseconds"> Total time elapsed for the batch write operation in microseconds. </param>
	[Event(50, Level = EventLevel.Informational, Keywords = Keywords.Write | Keywords.Performance, Task = EventTasks.MessageFlow)]
	public void BatchWriteCompleted(string channelId, int batchSize, double totalElapsedMicroseconds)
	{
		if (IsEnabled())
		{
			WriteEvent(50, channelId, batchSize, totalElapsedMicroseconds);
		}
	}

	/// <summary>
	/// Logs when a batch read operation completes.
	/// </summary>
	/// <param name="channelId"> The unique identifier for the channel. </param>
	/// <param name="batchSize"> The size of the batch that was read. </param>
	/// <param name="totalElapsedMicroseconds"> Total time elapsed for the batch read operation in microseconds. </param>
	[Event(51, Level = EventLevel.Informational, Keywords = Keywords.Read | Keywords.Performance, Task = EventTasks.MessageFlow)]
	public void BatchReadCompleted(string channelId, int batchSize, double totalElapsedMicroseconds)
	{
		if (IsEnabled())
		{
			WriteEvent(51, channelId, batchSize, totalElapsedMicroseconds);
		}
	}



	/// <summary>
	/// Writes a custom event with arbitrary data.
	/// </summary>
	[Event(100, Level = EventLevel.Verbose, Keywords = Keywords.Diagnostics)]
	public void CustomMetric(string metricName, double value, string unit, string? channelId = "")
	{
		if (IsEnabled(EventLevel.Verbose, Keywords.Diagnostics))
		{
			WriteEvent(100, metricName, value, unit, channelId ?? string.Empty);
		}
	}

	/// <summary>
	/// Helper to measure operation duration and write event.
	/// </summary>
	[NonEvent]
	public IDisposable MeasureOperation(string channelId, string operationType) => new OperationMeasurement(this, channelId, operationType);

	private sealed class OperationMeasurement(ChannelEventSource eventSource, string channelId, string operationType) : IDisposable
	{
		private readonly long _startTicks = Stopwatch.GetTimestamp();

		public void Dispose()
		{
			var elapsedTicks = Stopwatch.GetTimestamp() - _startTicks;
			var elapsedMs = elapsedTicks * 1000 / Stopwatch.Frequency;
			eventSource.CustomMetric($"{operationType}.Duration", elapsedMs, "ms", channelId);
		}
	}
}
