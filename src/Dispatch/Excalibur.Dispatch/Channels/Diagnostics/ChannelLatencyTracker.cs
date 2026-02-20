// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels.Diagnostics;

/// <summary>
/// Latency tracker for performance monitoring.
/// </summary>
public sealed class ChannelLatencyTracker(string channelId, int sampleSize = 1000)
{
	private readonly string _channelId = channelId ?? throw new ArgumentNullException(nameof(channelId));
	private readonly double[] _samples = new double[sampleSize];
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private int _sampleIndex;

	/// <summary>
	/// Records a latency measurement for the channel.
	/// </summary>
	/// <param name="microseconds"> The latency measurement in microseconds. </param>
	public void RecordLatency(double microseconds)
	{
		lock (_lock)
		{
			_samples[_sampleIndex % sampleSize] = microseconds;
			_sampleIndex++;

			// Check for high latency every 100 samples
			if (_sampleIndex % 100 == 0)
			{
				CheckHighLatency();
			}
		}
	}

	/// <summary>
	/// Gets the current latency statistics including average, 95th percentile, and 99th percentile.
	/// </summary>
	/// <returns> A tuple containing the average, P95, and P99 latency values in microseconds. </returns>
	public (double avg, double p95, double p99) GetStatistics()
	{
		lock (_lock)
		{
			var count = Math.Min(_sampleIndex, sampleSize);
			if (count == 0)
			{
				return (0, 0, 0);
			}

			var sorted = new double[count];
			Array.Copy(_samples, sorted, count);
			Array.Sort(sorted);

			var avg = sorted.Average();
			var p95 = sorted[(int)(count * 0.95)];
			var p99 = sorted[(int)(count * 0.99)];

			return (avg, p95, p99);
		}
	}

	private void CheckHighLatency()
	{
		var sorted = new double[Math.Min(_sampleIndex, sampleSize)];
		Array.Copy(_samples, sorted, sorted.Length);
		Array.Sort(sorted);

		if (sorted.Length > 0)
		{
			var p95Index = (int)(sorted.Length * 0.95);
			var p99Index = (int)(sorted.Length * 0.99);

			var p95 = sorted[Math.Min(p95Index, sorted.Length - 1)];
			var p99 = sorted[Math.Min(p99Index, sorted.Length - 1)];

			// Alert if P95 > 1000Î¼s (1ms)
			if (p95 > 1000)
			{
				ChannelEventSource.Log.HighLatencyDetected(_channelId, p95, p99);
			}
		}
	}
}
