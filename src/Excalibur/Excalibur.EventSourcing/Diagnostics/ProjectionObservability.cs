// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.EventSourcing.Diagnostics;

/// <summary>
/// Provides projection observability instruments: lag, errors, rebuild duration,
/// and cursor map position metrics.
/// </summary>
/// <remarks>
/// All instruments are created from the shared <see cref="EventNotificationTelemetry.MeterName"/>
/// Meter via <see cref="IMeterFactory"/>.
/// </remarks>
internal sealed class ProjectionObservability
{
	// Meter lifetime is managed by IMeterFactory (DI singleton).
	// Storing the reference satisfies CA2000 -- disposal is handled by DI.
	private readonly Meter _meter;
	private readonly UpDownCounter<long> _lagCounter;
	private readonly Counter<long> _errorCounter;
	private readonly Histogram<double> _rebuildDurationHistogram;

	public ProjectionObservability(IMeterFactory meterFactory)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);

		_meter = meterFactory.Create(EventNotificationTelemetry.MeterName);

		// R27.46: Async projection lag (events behind global stream head)
		_lagCounter = _meter.CreateUpDownCounter<long>(
			"excalibur.projection.lag.events",
			unit: "{events}",
			description: "Number of events an async projection is behind the global stream head.");

		// R27.47: Projection error counter
		_errorCounter = _meter.CreateCounter<long>(
			"excalibur.projection.error.count",
			unit: "{errors}",
			description: "Total projection processing errors.");

		// R27.48: Rebuild duration histogram
		_rebuildDurationHistogram = _meter.CreateHistogram<double>(
			"excalibur.projection.rebuild.duration",
			unit: "ms",
			description: "Duration of projection rebuild operations.");

		// R27.60: Cursor map position gauge
		_meter.CreateObservableGauge(
			"excalibur.projection.cursor_map.positions",
			observeValues: ObserveCursorMapPositions,
			unit: "{position}",
			description: "Current cursor map position per stream per projection.");
	}

	/// <summary>
	/// Reports projection lag (R27.46).
	/// </summary>
	internal void ReportLag(string projectionName, long lagEvents)
	{
		_lagCounter.Add(lagEvents, new KeyValuePair<string, object?>("projection.name", projectionName));
	}

	/// <summary>
	/// Records a projection error (R27.47).
	/// </summary>
	internal void RecordError(string projectionType, string errorType)
	{
		_errorCounter.Add(1,
			new KeyValuePair<string, object?>("projection.type", projectionType),
			new KeyValuePair<string, object?>("error.type", errorType));
	}

	/// <summary>
	/// Records rebuild duration in milliseconds (R27.48).
	/// </summary>
	internal void RecordRebuildDuration(string projectionType, double durationMs)
	{
		_rebuildDurationHistogram.Record(durationMs,
			new KeyValuePair<string, object?>("projection.type", projectionType));
	}

	// Observable gauge callback -- cursor map positions are tracked externally
	// and reported here. In a real implementation, this would query ICursorMapStore.
	// For now, returns empty -- TestsDeveloper will add functional tests.
	private static IEnumerable<Measurement<long>> ObserveCursorMapPositions()
	{
		return [];
	}
}
