// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Aggregates metrics over time windows.
/// </summary>
public sealed class MetricAggregator : IDisposable
{
	private readonly MetricRegistry _registry;
	private readonly Timer _timer;
	private readonly Action<MetricSnapshot[]> _onWindowComplete;

	/// <summary>
	/// Initializes a new instance of the <see cref="MetricAggregator" /> class.
	/// </summary>
	/// <param name="registry"> The metric registry to aggregate metrics from. </param>
	/// <param name="windowDuration"> The duration of each aggregation window. </param>
	/// <param name="onWindowComplete"> Action to execute when an aggregation window completes. </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="registry" /> or <paramref name="onWindowComplete" /> is null.
	/// </exception>
	public MetricAggregator(
		MetricRegistry registry,
		TimeSpan windowDuration,
		Action<MetricSnapshot[]> onWindowComplete)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_onWindowComplete = onWindowComplete ?? throw new ArgumentNullException(nameof(onWindowComplete));
		_timer = new Timer(CollectAndReset, state: null, windowDuration, windowDuration);
	}

	/// <summary>
	/// Disposes the metric aggregator and stops the aggregation timer.
	/// </summary>
	public void Dispose() => _timer?.Dispose();

	private void CollectAndReset(object? state)
	{
		try
		{
			// Collect snapshots
			var snapshots = _registry.CollectSnapshots();

			// Notify
			_onWindowComplete(snapshots);

			// Reset counters and histograms (but not gauges)
			_registry.ResetAll();
		}
		catch (Exception ex)
		{
			// Log error - in production, use proper logging
			Console.Error.WriteLine($"Error in metric aggregation: {ex}");
		}
	}
}
