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
	private readonly Counter<long> _errorCounter;

	public ProjectionObservability(IMeterFactory meterFactory)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);

		_meter = meterFactory.Create(EventNotificationTelemetry.MeterName);
		var meter = _meter;

		// R27.47: Projection error counter
		_errorCounter = meter.CreateCounter<long>(
			"excalibur.projection.error.count",
			unit: "{errors}",
			description: "Total projection processing errors.");
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
}
