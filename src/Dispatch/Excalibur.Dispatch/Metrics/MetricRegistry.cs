// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Registry for all metrics in the system.
/// </summary>
public sealed class MetricRegistry
{
	private static readonly Lazy<MetricRegistry> _global = new(static () => new MetricRegistry());

	private readonly ConcurrentDictionary<string, IMetric> _metrics = new(StringComparer.Ordinal);

	private readonly ConcurrentDictionary<string, MetricMetadata> _metadata = new(StringComparer.Ordinal);

	private int _nextMetricId;

	/// <summary>
	/// Gets the global metric registry instance.
	/// </summary>
	/// <value>The current <see cref="Global"/> value.</value>
	public static MetricRegistry Global => _global.Value;

	/// <summary>
	/// Creates or retrieves a counter metric.
	/// </summary>
	public RateCounter Counter(
		string name,
		string description = "",
		string unit = "") =>
		(RateCounter)_metrics.GetOrAdd(
			name,
			(key, state) =>
			{
				var counter = new RateCounter();
				var metricId = Interlocked.Increment(ref state.Self._nextMetricId);
				_ = state.Self.CreateMetadata(metricId, key, state.Description, state.Unit, MetricType.Counter);
				return counter;
			},
			(Self: this, Description: description, Unit: unit));

	/// <summary>
	/// Creates or retrieves a labeled counter metric.
	/// </summary>
	public LabeledCounter LabeledCounter(
		string name,
		string description = "",
		string unit = "",
		params string[] labelNames) =>
		(LabeledCounter)_metrics.GetOrAdd(
			name,
			(key, state) =>
			{
				var metricId = Interlocked.Increment(ref state.Self._nextMetricId);
				var metadata =
					state.Self.CreateMetadata(metricId, key, state.Description, state.Unit, MetricType.Counter, state.LabelNames);
				var labeledCounter = new LabeledCounter(metadata);
				return labeledCounter;
			},
			(Self: this, Description: description, Unit: unit, LabelNames: labelNames));

	/// <summary>
	/// Creates or retrieves a gauge metric.
	/// </summary>
	public ValueGauge Gauge(
		string name,
		string description = "",
		string unit = "") =>
		(ValueGauge)_metrics.GetOrAdd(
			name,
			(key, state) =>
			{
				var gauge = new ValueGauge();
				var metricId = Interlocked.Increment(ref state.Self._nextMetricId);
				_ = state.Self.CreateMetadata(metricId, key, state.Description, state.Unit, MetricType.Gauge);
				return gauge;
			},
			(Self: this, Description: description, Unit: unit));

	/// <summary>
	/// Creates or retrieves a histogram metric.
	/// </summary>
	public ValueHistogram Histogram(
		string name,
		string description = "",
		string unit = "",
		HistogramConfiguration? configuration = null) =>
		(ValueHistogram)_metrics
			.GetOrAdd(
				name,
				(key, state) =>
				{
					var metricId = Interlocked.Increment(ref state.Self._nextMetricId);
					var metadata = state.Self.CreateMetadata(metricId, key, state.Description, state.Unit, MetricType.Histogram);
					var histogram = new ValueHistogram(metadata, state.Configuration ?? HistogramConfiguration.DefaultLatency);
					return histogram;
				},
				(Self: this, Description: description, Unit: unit, Configuration: configuration));

	/// <summary>
	/// Gets all registered metrics.
	/// </summary>
	public IEnumerable<IMetric> GetAllMetrics() => _metrics.Values;

	/// <summary>
	/// Gets metadata for all registered metrics.
	/// </summary>
	public IEnumerable<MetricMetadata> GetAllMetadata() => _metadata.Values;

	/// <summary>
	/// Collects snapshots from all metrics.
	/// </summary>
	public MetricSnapshot[] CollectSnapshots()
	{
		// First pass: count total snapshots needed
		var totalCount = 0;
		foreach (var metric in _metrics.Values)
		{
			switch (metric)
			{
				case RateCounter:
				case ValueGauge:
				case ValueHistogram:
					totalCount++;
					break;

				case LabeledCounter labeledCounter:
					totalCount += labeledCounter.LabelCount;
					break;
				default:
					break;
			}
		}

		// Pre-allocate array
		var snapshots = new MetricSnapshot[totalCount];
		var index = 0;

		// Second pass: populate array
		foreach (var metric in _metrics.Values)
		{
			switch (metric)
			{
				case RateCounter counter:
					var counterSnapshot = counter.GetSnapshot();
					snapshots[index++] = new MetricSnapshot(
						metricId: Interlocked.Increment(ref _nextMetricId),
						type: MetricType.Counter,
						timestampTicks: counterSnapshot.Timestamp.Ticks,
						value: counterSnapshot.Value,
						labelSetId: 0);
					break;

				case LabeledCounter labeledCounter:
					foreach (var snapshot in labeledCounter.GetSnapshots())
					{
						snapshots[index++] = snapshot;
					}

					break;

				case ValueGauge gauge:
					var gaugeSnapshot = gauge.GetSnapshot();
					snapshots[index++] = new MetricSnapshot(
						metricId: Interlocked.Increment(ref _nextMetricId),
						type: MetricType.Gauge,
						timestampTicks: gaugeSnapshot.Timestamp.Ticks,
						value: gaugeSnapshot.Value,
						labelSetId: 0,
						count: gaugeSnapshot.Count,
						sum: gaugeSnapshot.Value * gaugeSnapshot.Count,
						min: gaugeSnapshot.Min,
						max: gaugeSnapshot.Max);
					break;

				case ValueHistogram histogram:
					var histogramSnapshot = histogram.GetSnapshot();
					snapshots[index++] = new MetricSnapshot(
						metricId: Interlocked.Increment(ref _nextMetricId),
						type: MetricType.Histogram,
						timestampTicks: DateTimeOffset.UtcNow.Ticks,
						value: histogramSnapshot.Mean,
						labelSetId: 0,
						count: histogramSnapshot.Count,
						sum: histogramSnapshot.Sum,
						min: histogramSnapshot.Min,
						max: histogramSnapshot.Max,
						buckets: ConvertHistogramBuckets(histogramSnapshot));
					break;
				default:
					break;
			}
		}

		return snapshots;
	}

	/// <summary>
	/// Resets all metrics to their initial state.
	/// </summary>
	public void ResetAll()
	{
		foreach (var metric in _metrics.Values)
		{
			switch (metric)
			{
				case RateCounter counter:
					_ = counter.Reset();
					break;

				case LabeledCounter labeledCounter:
					labeledCounter.Reset();
					break;

				case ValueHistogram histogram:
					histogram.Reset();
					break;
				default:
					break;
			}
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification =
			"Parameter reserved for future histogram bucket conversion implementation when HistogramSnapshot includes bucket data")]
	private static HistogramBucket[]? ConvertHistogramBuckets(HistogramSnapshot snapshot) =>

		// Convert histogram buckets from the snapshot if available. Returns null if no bucket data is available. HistogramSnapshot doesn't
		// contain bucket data, only statistical summaries
		null;

	private MetricMetadata CreateMetadata(
		int metricId,
		string name,
		string description,
		string unit,
		MetricType type,
		string[]? labelNames = null) =>
		_metadata.GetOrAdd(
			name,
			(key, state) => new MetricMetadata(
				state.MetricId,
				key,
				state.Description,
				state.Unit,
				state.Type,
				state.LabelNames),
			(MetricId: metricId, Description: description, Unit: unit, Type: type, LabelNames: labelNames));
}
