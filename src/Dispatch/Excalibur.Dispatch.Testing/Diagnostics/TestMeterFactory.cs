// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Testing.Diagnostics;

/// <summary>
/// A test implementation of <see cref="IMeterFactory"/> that creates real <see cref="Meter"/> instances
/// for use in unit tests where meters need to produce functional instruments (counters, histograms, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Use this factory when your code under test requires an <see cref="IMeterFactory"/> and you need the
/// resulting instruments to actually record measurements (e.g., for assertion with a <see cref="MeterListener"/>).
/// </para>
/// <para>
/// This class implements <see cref="IDisposable"/> and will dispose all meters it created when disposed.
/// Wrap usage in a <c>using</c> statement or dispose in test cleanup.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var meterFactory = new TestMeterFactory();
/// var sut = new MyService(meterFactory);
///
/// // Act
/// sut.DoWork();
///
/// // Assert - use MeterListener to verify metrics were recorded
/// </code>
/// </example>
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
