// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;
using Excalibur.EventSourcing.Diagnostics;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Unit tests for ProjectionObservability (R27.47).
/// Validates metric instrument creation and recording.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionObservabilityShould : IDisposable
{
	private readonly IMeterFactory _meterFactory;
	private readonly MeterListener _listener;
	private readonly List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> _recordings = [];

	public ProjectionObservabilityShould()
	{
		_meterFactory = new TestMeterFactory();
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == EventNotificationTelemetry.MeterName)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
		{
			_recordings.Add((instrument.Name, measurement, tags.ToArray()));
		});
		_listener.Start();
	}

	public void Dispose()
	{
		_listener.Dispose();
		(_meterFactory as IDisposable)?.Dispose();
	}

	/// <summary>
	/// R27.47: Projection error counter incremented.
	/// </summary>
	[Fact]
	public void EmitErrorCounter()
	{
		// Arrange
		var obs = new ProjectionObservability(_meterFactory);

		// Act
		obs.RecordError("OrderSummary", "InvalidOperationException");
		_listener.RecordObservableInstruments();

		// Assert
		var errorRecording = _recordings.FirstOrDefault(r => r.Name == "excalibur.projection.error.count");
		errorRecording.ShouldNotBe(default);
		((long)errorRecording.Value).ShouldBe(1);
		errorRecording.Tags.ShouldContain(t => t.Key == "projection.type" && (string)t.Value! == "OrderSummary");
		errorRecording.Tags.ShouldContain(t => t.Key == "error.type" && (string)t.Value! == "InvalidOperationException");
	}

	/// <summary>
	/// Validates null constructor argument.
	/// </summary>
	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionObservability(null!));
	}

	/// <summary>
	/// Simple IMeterFactory for tests (creates real Meter instances).
	/// </summary>
	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters)
			{
				meter.Dispose();
			}
		}
	}
}
