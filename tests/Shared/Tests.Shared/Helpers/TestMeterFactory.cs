// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Tests.Shared.Helpers;

/// <summary>
/// A test implementation of <see cref="IMeterFactory"/> that creates real <see cref="Meter"/> instances
/// for use in unit tests where meters need to produce functional instruments (counters, histograms, etc.).
/// </summary>
public sealed class TestMeterFactory : IMeterFactory
{
	private readonly List<Meter> _meters = [];

	/// <inheritdoc />
	public Meter Create(MeterOptions options)
	{
		var meter = new Meter(options.Name, options.Version);
		_meters.Add(meter);
		return meter;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		foreach (var meter in _meters)
		{
			meter.Dispose();
		}

		_meters.Clear();
	}
}
