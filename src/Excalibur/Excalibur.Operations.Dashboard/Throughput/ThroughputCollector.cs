// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Excalibur.Operations.Dashboard.Throughput;

/// <summary>
/// Bridges the existing Excalibur OpenTelemetry counter meters (outbox, inbox, saga) into a
/// per-subsystem throughput rate for the operational dashboard.
/// </summary>
/// <remarks>
/// <para>
/// Uses a <see cref="MeterListener"/> — the BCL primitive for in-process metric consumption — to
/// accumulate each subsystem's counter measurements. Throughput is derived on read as the change in
/// cumulative count divided by the elapsed wall-clock since the previous read (a simple rolling rate),
/// so no per-measurement history is retained.
/// </para>
/// <para>
/// The read model surfaces the <em>rate</em>; the per-subsystem state (counts, entries) is served by the
/// dedicated subsystem modules. Subsystems whose meters are not emitting report a zero rate rather than
/// failing.
/// </para>
/// </remarks>
internal sealed class ThroughputCollector : IDisposable
{
	/// <summary>Maps a known subsystem meter name to its stable dashboard subsystem key.</summary>
	private static readonly IReadOnlyDictionary<string, string> MeterToSubsystem =
		new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["Excalibur.Outbox.Store"] = "outbox",
			["Excalibur.Inbox"] = "inbox",
			["Excalibur.Saga"] = "saga",
		};

	private readonly TimeProvider _timeProvider;
	private readonly MeterListener _listener;
	private readonly ConcurrentDictionary<string, Sample> _samples = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>Initializes a new instance of the <see cref="ThroughputCollector"/> class.</summary>
	/// <param name="timeProvider">The time source used to compute elapsed windows (injectable for tests).</param>
	public ThroughputCollector(TimeProvider? timeProvider = null)
	{
		_timeProvider = timeProvider ?? TimeProvider.System;

		_listener = new MeterListener
		{
			InstrumentPublished = (instrument, listener) =>
			{
				if (MeterToSubsystem.ContainsKey(instrument.Meter.Name))
				{
					listener.EnableMeasurementEvents(instrument);
				}
			},
		};

		_listener.SetMeasurementEventCallback<long>(OnMeasurement);
		_listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
			OnMeasurement(instrument, measurement, tags, state));
		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
			OnMeasurement(instrument, (long)measurement, tags, state));
		_listener.Start();
	}

	/// <summary>Gets the stable subsystem keys this collector can report throughput for.</summary>
	public static IReadOnlyCollection<string> KnownSubsystems => (IReadOnlyCollection<string>)MeterToSubsystem.Values;

	/// <summary>Determines whether the given subsystem key is one this collector tracks.</summary>
	/// <param name="subsystem">The subsystem key (for example <c>outbox</c>).</param>
	/// <returns><see langword="true"/> when tracked; otherwise <see langword="false"/>.</returns>
	public static bool IsKnownSubsystem(string subsystem) =>
		MeterToSubsystem.Values.Contains(subsystem, StringComparer.Ordinal);

	/// <summary>
	/// Computes the current throughput for a subsystem as the rate of counter growth since the previous
	/// read, then advances the baseline.
	/// </summary>
	/// <param name="subsystem">The subsystem key.</param>
	/// <returns>
	/// The per-second rate and the window it was measured over. The first read for a subsystem
	/// establishes the baseline and reports a zero rate.
	/// </returns>
	public ThroughputReading Read(string subsystem)
	{
		ArgumentException.ThrowIfNullOrEmpty(subsystem);

		// Guard against unbounded growth: only known subsystem keys allocate a sample. An unknown key
		// (arbitrary caller input) reports a zero rate rather than inflating the sample dictionary.
		if (!IsKnownSubsystem(subsystem))
		{
			return new ThroughputReading(0d, 0d);
		}

		var now = _timeProvider.GetTimestamp();
		var sample = _samples.GetOrAdd(subsystem, _ => new Sample());

		lock (sample.Gate)
		{
			var currentCount = sample.Count;
			if (sample.LastTimestamp == 0)
			{
				sample.LastTimestamp = now;
				sample.LastCount = currentCount;
				return new ThroughputReading(0d, 0d);
			}

			var elapsedSeconds = _timeProvider.GetElapsedTime(sample.LastTimestamp, now).TotalSeconds;
			var delta = currentCount - sample.LastCount;
			sample.LastTimestamp = now;
			sample.LastCount = currentCount;

			var rate = elapsedSeconds > 0d ? delta / elapsedSeconds : 0d;
			return new ThroughputReading(rate < 0d ? 0d : rate, elapsedSeconds);
		}
	}

	private void OnMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
	{
		if (!MeterToSubsystem.TryGetValue(instrument.Meter.Name, out var subsystem))
		{
			return;
		}

		var sample = _samples.GetOrAdd(subsystem, _ => new Sample());
		lock (sample.Gate)
		{
			sample.Count += measurement;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_listener.Dispose();
	}

	private sealed class Sample
	{
		public object Gate { get; } = new();

		public long Count;
		public long LastCount;
		public long LastTimestamp;
	}
}

/// <summary>A single throughput reading: the per-second rate and the window it was measured over.</summary>
/// <param name="RatePerSecond">The measured events-per-second since the previous read.</param>
/// <param name="WindowSeconds">The elapsed window the rate was computed over, in seconds.</param>
internal readonly record struct ThroughputReading(double RatePerSecond, double WindowSeconds);
