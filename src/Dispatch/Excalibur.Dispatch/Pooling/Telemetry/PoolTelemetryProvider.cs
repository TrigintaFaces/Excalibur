// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Pooling.Telemetry;

/// <summary>
/// Provides telemetry and metrics collection for pool operations including rent/return tracking and duration measurements.
/// </summary>
public sealed class PoolTelemetryProvider : IDisposable
{
	private readonly Meter _meter;
	private readonly Counter<long> _rentCounter;
	private readonly Counter<long> _returnCounter;
	private readonly Histogram<double> _rentDuration;

	/// <summary>
	/// Initializes a new instance of the <see cref="PoolTelemetryProvider"/> class with the specified meter name.
	/// </summary>
	/// <param name="meterName"> The name of the meter for telemetry collection. Defaults to "Excalibur.Dispatch.Pooling". </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public PoolTelemetryProvider(string meterName = "Excalibur.Dispatch.Pooling")
		: this(meterFactory: null, meterName)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PoolTelemetryProvider"/> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	/// <param name="meterName"> The name of the meter for telemetry collection. Defaults to "Excalibur.Dispatch.Pooling". </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or this class and disposed in Dispose()")]
	public PoolTelemetryProvider(IMeterFactory? meterFactory, string meterName = "Excalibur.Dispatch.Pooling")
	{
		_meter = meterFactory?.Create(meterName) ?? new Meter(meterName, "1.0.0");

		_rentCounter = _meter.CreateCounter<long>(
			"dispatch.pool.rent.count",
			description: "Number of items rented from pools");

		_returnCounter = _meter.CreateCounter<long>(
			"dispatch.pool.return.count",
			description: "Number of items returned to pools");

		_rentDuration = _meter.CreateHistogram<double>(
			"dispatch.pool.rent.duration",
			unit: "ms",
			description: "Duration of rent operations");
	}

	/// <summary>
	/// Records a rent operation for telemetry tracking, including the pool name and operation duration.
	/// </summary>
	/// <param name="poolName"> The name of the pool from which the item was rented. </param>
	/// <param name="durationMs"> The duration of the rent operation in milliseconds. </param>
	public void RecordRent(string poolName, double durationMs)
	{
		_rentCounter.Add(1, new KeyValuePair<string, object?>("pool", poolName));
		_rentDuration.Record(durationMs, new KeyValuePair<string, object?>("pool", poolName));
	}

	/// <summary>
	/// Records a return operation for telemetry tracking with the specified pool name.
	/// </summary>
	/// <param name="poolName"> The name of the pool to which the item was returned. </param>
	public void RecordReturn(string poolName) => _returnCounter.Add(1, new KeyValuePair<string, object?>("pool", poolName));

	/// <summary>
	/// Disposes the telemetry provider, releasing all meters and associated resources.
	/// </summary>
	public void Dispose() => _meter?.Dispose();
}
