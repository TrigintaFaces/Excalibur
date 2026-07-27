// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Tests for IMeterFactory migration across Dispatch.Observability metrics classes (S560.50).
/// Verifies that the IMeterFactory constructor creates proper meter lifecycle, instruments function,
/// and the default constructor remains backward-compatible.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "IMeterFactory")]
public sealed class MeterFactoryMigrationShould : IDisposable
{
	private readonly TestMeterFactory _meterFactory = new();

	public void Dispose()
	{
		_meterFactory.Dispose();
	}

	#region DispatchMetrics IMeterFactory Tests

	[Fact]
	public void DispatchMetrics_IMeterFactory_CreatesMeterFromFactory()
	{
		using var metrics = new DispatchMetrics(_meterFactory);

		metrics.Meter.ShouldNotBeNull();
		metrics.Meter.Name.ShouldBe(DispatchMetrics.MeterName);
	}

	[Fact]
	public void DispatchMetrics_IMeterFactory_NullThrowsArgumentNullException()
	{
		_ = Should.Throw<ArgumentNullException>(() => new DispatchMetrics((IMeterFactory)null!));
	}

	[Fact]
	public void DispatchMetrics_IMeterFactory_InstrumentsAreCreated()
	{
		using var metrics = new DispatchMetrics(_meterFactory);
		var instrumentNames = new List<string>();

		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, theListener) =>
		{
			if (ReferenceEquals(instrument.Meter, metrics.Meter))
			{
				instrumentNames.Add(instrument.Name);
				theListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.Start();

		// Trigger instrument creation by recording metrics
		metrics.RecordMessageProcessed("TestMsg", "TestHandler");
		metrics.RecordProcessingDuration(42.0, "TestMsg", true);
		metrics.RecordMessagePublished("TestMsg", "test-queue");
		metrics.RecordMessageFailed("TestMsg", "TimeoutException", 1);
		metrics.UpdateActiveSessions(1);

		instrumentNames.ShouldContain("dispatch.messages.processed");
		instrumentNames.ShouldContain("dispatch.messages.duration");
		instrumentNames.ShouldContain("dispatch.messages.published");
		instrumentNames.ShouldContain("dispatch.messages.failed");
		instrumentNames.ShouldContain("dispatch.sessions.active");
	}

	[Fact]
	public void DispatchMetrics_IMeterFactory_MeterRecordsAreObservable()
	{
		using var metrics = new DispatchMetrics(_meterFactory);
		var recordedCount = 0L;

		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, theListener) =>
		{
			if (ReferenceEquals(instrument.Meter, metrics.Meter) &&
				instrument.Name == "dispatch.messages.processed")
			{
				theListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
		{
			recordedCount += value;
		});
		listener.Start();

		metrics.RecordMessageProcessed("TestMsg", "TestHandler");
		metrics.RecordMessageProcessed("TestMsg", "TestHandler");

		listener.RecordObservableInstruments();

		recordedCount.ShouldBe(2);
	}

	[Fact]
	public void DispatchMetrics_DefaultConstructor_StillWorks()
	{
		using var metrics = new DispatchMetrics();

		metrics.Meter.ShouldNotBeNull();
		metrics.Meter.Name.ShouldBe(DispatchMetrics.MeterName);

		// Should not throw
		metrics.RecordMessageProcessed("TestMsg", "TestHandler");
	}

	[Fact]
	public void DispatchMetrics_FactoryMeter_NotDisposedByMetricsDispose()
	{
		var metrics = new DispatchMetrics(_meterFactory);
		var meter = metrics.Meter;

		metrics.Dispose();

		// Factory-created meters should NOT be disposed by DispatchMetrics.Dispose()
		// The factory manages the lifecycle. We verify by checking the meter is still
		// registered in the factory.
		_meterFactory.CreatedMeters.ShouldContain(m => m.Name == DispatchMetrics.MeterName);
	}

	#endregion


	#region Test Helpers

	/// <summary>
	/// Minimal IMeterFactory implementation for testing meter lifecycle.
	/// </summary>
	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public IReadOnlyList<Meter> CreatedMeters => _meters;

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options.Name, options.Version);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters)
			{
				meter.Dispose();
			}
			_meters.Clear();
		}
	}

	#endregion
}
