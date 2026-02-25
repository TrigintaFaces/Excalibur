// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Domain.Metrics;

/// <summary>
/// OpenTelemetry-based implementation of the IMetrics interface.
/// </summary>
public sealed class OpenTelemetryMetrics : IMetrics
{
	private readonly IMeterFactory _meterFactory;
	private readonly Meter _meter;
	private readonly ConcurrentDictionary<string, object> _instruments;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenTelemetryMetrics" /> class.
	/// </summary>
	/// <param name="meterFactory"> The meter factory used to create meters. </param>
	public OpenTelemetryMetrics(IMeterFactory meterFactory)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);

		_meterFactory = meterFactory;
		_meter = _meterFactory.Create("Excalibur.Metrics", "1.0.0");
		_instruments = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
	}

	/// <inheritdoc />
	public void RecordCounter(string name, long value, params KeyValuePair<string, object>[] tags)
	{
		ArgumentNullException.ThrowIfNull(name);

		if (_instruments.GetOrAdd(name, static (key, meter) =>
				meter.CreateCounter<long>(key, description: $"Counter for {key}"), _meter) is Counter<long> counter)
		{
			if (tags?.Length > 0)
			{
				var tagList = new TagList(tags.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)).ToArray());
				counter.Add(value, tagList);
			}
			else
			{
				counter.Add(value);
			}
		}
	}

	/// <inheritdoc />
	public void RecordGauge(string name, double value, params KeyValuePair<string, object>[] tags)
	{
		ArgumentNullException.ThrowIfNull(name);

		// For gauges, we use ObservableGauge which requires a callback Since we need to record immediate values, we'll use a histogram
		// instead which can record point-in-time measurements
		if (_instruments.GetOrAdd(name + "_gauge", static (_, state) =>
					state.meter.CreateHistogram<double>(state.name, unit: null, description: $"Gauge for {state.name}"),
				(meter: _meter, name)) is Histogram<double> histogram)
		{
			if (tags?.Length > 0)
			{
				var tagList = new TagList(tags.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)).ToArray());
				histogram.Record(value, tagList);
			}
			else
			{
				histogram.Record(value);
			}
		}
	}

	/// <inheritdoc />
	public void RecordHistogram(string name, double value, params KeyValuePair<string, object>[] tags)
	{
		ArgumentNullException.ThrowIfNull(name);

		if (_instruments.GetOrAdd(name, static (key, meter) =>
				meter.CreateHistogram<double>(key, unit: null, description: $"Histogram for {key}"), _meter) is Histogram<double> histogram)
		{
			if (tags?.Length > 0)
			{
				var tagList = new TagList(tags.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)).ToArray());
				histogram.Record(value, tagList);
			}
			else
			{
				histogram.Record(value);
			}
		}
	}
}
