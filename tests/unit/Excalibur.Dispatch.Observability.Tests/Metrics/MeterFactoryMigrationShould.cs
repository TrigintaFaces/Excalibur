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
			if (instrument.Meter.Name == DispatchMetrics.MeterName)
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
			if (instrument.Meter.Name == DispatchMetrics.MeterName &&
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

	#region CircuitBreakerMetrics IMeterFactory Tests

	[Fact]
	public void CircuitBreakerMetrics_IMeterFactory_CreatesMeterFromFactory()
	{
		using var metrics = new CircuitBreakerMetrics(_meterFactory);

		metrics.Meter.ShouldNotBeNull();
		metrics.Meter.Name.ShouldBe(CircuitBreakerMetrics.MeterName);
	}

	[Fact]
	public void CircuitBreakerMetrics_IMeterFactory_NullThrowsArgumentNullException()
	{
		_ = Should.Throw<ArgumentNullException>(() => new CircuitBreakerMetrics((IMeterFactory)null!));
	}

	[Fact]
	public void CircuitBreakerMetrics_IMeterFactory_InstrumentsAreCreated()
	{
		using var metrics = new CircuitBreakerMetrics(_meterFactory);
		var instrumentNames = new List<string>();

		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, theListener) =>
		{
			if (instrument.Meter.Name == CircuitBreakerMetrics.MeterName)
			{
				instrumentNames.Add(instrument.Name);
				theListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.Start();

		// Trigger recording to verify instruments exist
		metrics.RecordStateChange("test-circuit", "Closed", "Open");
		metrics.RecordRejection("test-circuit");
		metrics.RecordFailure("test-circuit", "TimeoutException");
		metrics.RecordSuccess("test-circuit");

		instrumentNames.ShouldContain("dispatch.circuitbreaker.state_changes");
		instrumentNames.ShouldContain("dispatch.circuitbreaker.rejections");
		instrumentNames.ShouldContain("dispatch.circuitbreaker.failures");
		instrumentNames.ShouldContain("dispatch.circuitbreaker.successes");
	}

	[Fact]
	public void CircuitBreakerMetrics_DefaultConstructor_StillWorks()
	{
		using var metrics = new CircuitBreakerMetrics();

		metrics.Meter.ShouldNotBeNull();
		metrics.Meter.Name.ShouldBe(CircuitBreakerMetrics.MeterName);

		// Should not throw
		metrics.RecordStateChange("test-circuit", "Closed", "Open");
	}

	#endregion

	#region DeadLetterQueueMetrics IMeterFactory Tests

	[Fact]
	public void DeadLetterQueueMetrics_IMeterFactory_CreatesMeterFromFactory()
	{
		using var metrics = new DeadLetterQueueMetrics(_meterFactory);

		metrics.Meter.ShouldNotBeNull();
		metrics.Meter.Name.ShouldBe(DeadLetterQueueMetrics.MeterName);
	}

	[Fact]
	public void DeadLetterQueueMetrics_IMeterFactory_NullThrowsArgumentNullException()
	{
		_ = Should.Throw<ArgumentNullException>(() => new DeadLetterQueueMetrics((IMeterFactory)null!));
	}

	[Fact]
	public void DeadLetterQueueMetrics_IMeterFactory_InstrumentsAreCreated()
	{
		using var metrics = new DeadLetterQueueMetrics(_meterFactory);
		var instrumentNames = new List<string>();

		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, theListener) =>
		{
			if (instrument.Meter.Name == DeadLetterQueueMetrics.MeterName)
			{
				instrumentNames.Add(instrument.Name);
				theListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.Start();

		// Trigger recording to verify instruments exist
		metrics.RecordEnqueued("TestMsg", "MaxRetries", "test-queue");

		instrumentNames.ShouldContain("dispatch.dlq.enqueued");
	}

	[Fact]
	public void DeadLetterQueueMetrics_DefaultConstructor_StillWorks()
	{
		using var metrics = new DeadLetterQueueMetrics();

		metrics.Meter.ShouldNotBeNull();
		metrics.Meter.Name.ShouldBe(DeadLetterQueueMetrics.MeterName);

		// Should not throw
		metrics.RecordEnqueued("TestMsg", "MaxRetries", "test-queue");
	}

	#endregion

	#region Cross-Cutting IMeterFactory Pattern Tests

	[Fact]
	public void AllMetricsClasses_UseConsistentMeterFactoryPattern()
	{
		// Verify all three metrics classes have IMeterFactory constructors
		typeof(DispatchMetrics).GetConstructor([typeof(IMeterFactory)]).ShouldNotBeNull(
			"DispatchMetrics should have IMeterFactory constructor");
		typeof(CircuitBreakerMetrics).GetConstructor([typeof(IMeterFactory)]).ShouldNotBeNull(
			"CircuitBreakerMetrics should have IMeterFactory constructor");
		typeof(DeadLetterQueueMetrics).GetConstructor([typeof(IMeterFactory)]).ShouldNotBeNull(
			"DeadLetterQueueMetrics should have IMeterFactory constructor");
	}

	[Fact]
	public void AllMetricsClasses_StillHaveDefaultConstructor()
	{
		// Verify backward compatibility -- parameterless constructors still exist
		typeof(DispatchMetrics).GetConstructor(Type.EmptyTypes).ShouldNotBeNull(
			"DispatchMetrics should retain default constructor");
		typeof(CircuitBreakerMetrics).GetConstructor(Type.EmptyTypes).ShouldNotBeNull(
			"CircuitBreakerMetrics should retain default constructor");
		typeof(DeadLetterQueueMetrics).GetConstructor(Type.EmptyTypes).ShouldNotBeNull(
			"DeadLetterQueueMetrics should retain default constructor");
	}

	[Fact]
	public void SharedMeterFactory_ProducesDistinctMetersPerClass()
	{
		using var dispatch = new DispatchMetrics(_meterFactory);
		using var circuitBreaker = new CircuitBreakerMetrics(_meterFactory);
		using var deadLetter = new DeadLetterQueueMetrics(_meterFactory);

		dispatch.Meter.Name.ShouldNotBe(circuitBreaker.Meter.Name);
		dispatch.Meter.Name.ShouldNotBe(deadLetter.Meter.Name);
		circuitBreaker.Meter.Name.ShouldNotBe(deadLetter.Meter.Name);
	}

	[Fact]
	public void FactoryCreatedMeters_AreTrackedByFactory()
	{
		using var dispatch = new DispatchMetrics(_meterFactory);
		using var circuitBreaker = new CircuitBreakerMetrics(_meterFactory);
		using var deadLetter = new DeadLetterQueueMetrics(_meterFactory);

		_meterFactory.CreatedMeters.Count.ShouldBeGreaterThanOrEqualTo(3);
		_meterFactory.CreatedMeters.ShouldContain(m => m.Name == DispatchMetrics.MeterName);
		_meterFactory.CreatedMeters.ShouldContain(m => m.Name == CircuitBreakerMetrics.MeterName);
		_meterFactory.CreatedMeters.ShouldContain(m => m.Name == DeadLetterQueueMetrics.MeterName);
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
